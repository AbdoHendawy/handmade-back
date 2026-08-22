using FluentValidation;
using Handmade.Application.Abstractions.Authentication;
using Handmade.Application.Abstractions.Email;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Security;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Identity.DTOs;
using Handmade.Application.Identity.Email;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Handmade.Application.Identity.Services;

public sealed class JwtSettings
{
    public const string SectionName = "Jwt";

    public string SecretKey { get; set; } = string.Empty;

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public int AccessTokenExpirationMinutes { get; set; } = 15;

    public int RefreshTokenExpirationDays { get; set; } = 14;
}

public interface IAuthenticationService
{
    Task<AuthenticationResponse> RegisterAsync(RegisterRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthenticationResponse> LoginAsync(LoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthenticationResponse> GoogleLoginAsync(GoogleLoginRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<AuthenticationResponse> RefreshAsync(RefreshTokenRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task LogoutAsync(LogoutRequest request, string? ipAddress, CancellationToken cancellationToken = default);

    Task<UserResponse> GetMeAsync(CancellationToken cancellationToken = default);

    Task RevokeAllSessionsAsync(Guid userId, string? ipAddress, CancellationToken cancellationToken = default);
}

public sealed class AuthenticationService : IAuthenticationService
{
    private const string InvalidCredentialsMessage = "Invalid email or password.";

    private readonly IApplicationDbContext _db;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEnumerable<IExternalAuthProvider> _externalAuthProviders;
    private readonly IEmailSender _emailSender;
    private readonly IIdentityNotificationService _identityNotifications;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly JwtSettings _jwtSettings;
    private readonly IValidator<RegisterRequest> _registerValidator;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<GoogleLoginRequest> _googleValidator;
    private readonly IValidator<RefreshTokenRequest> _refreshValidator;
    private readonly IValidator<LogoutRequest> _logoutValidator;
    private readonly ILogger<AuthenticationService> _logger;

    public AuthenticationService(
        IApplicationDbContext db,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IEnumerable<IExternalAuthProvider> externalAuthProviders,
        IEmailSender emailSender,
        IIdentityNotificationService identityNotifications,
        ICurrentUser currentUser,
        IClock clock,
        IOptions<JwtSettings> jwtSettings,
        IValidator<RegisterRequest> registerValidator,
        IValidator<LoginRequest> loginValidator,
        IValidator<GoogleLoginRequest> googleValidator,
        IValidator<RefreshTokenRequest> refreshValidator,
        IValidator<LogoutRequest> logoutValidator,
        ILogger<AuthenticationService> logger)
    {
        _db = db;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _externalAuthProviders = externalAuthProviders;
        _emailSender = emailSender;
        _identityNotifications = identityNotifications;
        _currentUser = currentUser;
        _clock = clock;
        _jwtSettings = jwtSettings.Value;
        _registerValidator = registerValidator;
        _loginValidator = loginValidator;
        _googleValidator = googleValidator;
        _refreshValidator = refreshValidator;
        _logoutValidator = logoutValidator;
        _logger = logger;
    }

    public async Task<AuthenticationResponse> RegisterAsync(
        RegisterRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_registerValidator], cancellationToken);

        string email = User.NormalizeEmail(request.Email);
        bool exists = await _db.Users.AnyAsync(u => u.Email == email, cancellationToken);
        if (exists)
        {
            throw new ConflictException("An account with this email already exists.");
        }

        string passwordHash = _passwordHasher.HashPassword(request.Password);
        User user = User.RegisterLocal(email, passwordHash, request.FirstName, request.LastName);
        Role customerRole = await GetRequiredRoleAsync(RoleNames.Customer, cancellationToken);
        user.AssignRole(customerRole);

        _db.Users.Add(user);
        await _db.SaveChangesAsync(cancellationToken);

        await _identityNotifications.NotifyWelcomeAsync(user.Id, cancellationToken);
        await TrySendWelcomeEmailAsync(user, cancellationToken);

        return await IssueTokensAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthenticationResponse> LoginAsync(
        LoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_loginValidator], cancellationToken);

        string email = User.NormalizeEmail(request.Email);
        User? user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        if (user is null || string.IsNullOrWhiteSpace(user.PasswordHash) ||
            !_passwordHasher.VerifyPassword(request.Password, user.PasswordHash))
        {
            throw new DomainException(InvalidCredentialsMessage) { Code = AuthErrorCodes.InvalidCredentials };
        }

        user.EnsureCanAuthenticate();
        user.RecordLogin(_clock.UtcNow);
        await _db.SaveChangesAsync(cancellationToken);

        return await IssueTokensAsync(user, ipAddress, cancellationToken);
    }

    public async Task<AuthenticationResponse> GoogleLoginAsync(
        GoogleLoginRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_googleValidator], cancellationToken);

        IExternalAuthProvider provider = _externalAuthProviders.FirstOrDefault(p => p.ProviderName == AuthProviders.Google)
            ?? throw new DomainException("Google authentication is not configured.") { Code = AuthErrorCodes.GoogleNotConfigured };

        ExternalUserIdentity identity = await provider.ValidateAsync(request.IdToken, cancellationToken);

        if (!identity.EmailVerified)
        {
            throw new DomainException("Google email is not verified.") { Code = AuthErrorCodes.GoogleEmailUnverified };
        }

        string email = User.NormalizeEmail(identity.Email);

        ExternalLogin? existingLogin = await _db.ExternalLogins
            .Include(x => x.User)!
            .ThenInclude(u => u!.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(
                x => x.Provider == identity.Provider && x.ProviderUserId == identity.ProviderUserId,
                cancellationToken);

        if (existingLogin?.User is not null)
        {
            User existingUser = existingLogin.User;
            existingUser.EnsureCanAuthenticate();
            existingUser.RecordLogin(_clock.UtcNow);
            await _db.SaveChangesAsync(cancellationToken);
            return await IssueTokensAsync(existingUser, ipAddress, cancellationToken);
        }

        User? userByEmail = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .Include(u => u.ExternalLogins)
            .FirstOrDefaultAsync(u => u.Email == email, cancellationToken);

        bool isNewRegistration = false;

        if (userByEmail is null)
        {
            string firstName = string.IsNullOrWhiteSpace(identity.FirstName) ? "Handmade" : identity.FirstName!;
            string lastName = string.IsNullOrWhiteSpace(identity.LastName) ? "User" : identity.LastName!;
            userByEmail = User.RegisterExternal(email, firstName, lastName, isEmailVerified: true);
            Role customerRole = await GetRequiredRoleAsync(RoleNames.Customer, cancellationToken);
            userByEmail.AssignRole(customerRole);
            _db.Users.Add(userByEmail);
            isNewRegistration = true;
        }
        else
        {
            userByEmail.EnsureCanAuthenticate();
        }

        ExternalLogin link = ExternalLogin.Create(
            userByEmail.Id,
            identity.Provider,
            identity.ProviderUserId,
            identity.Email,
            _clock.UtcNow);

        userByEmail.LinkExternalLogin(link);
        userByEmail.RecordLogin(_clock.UtcNow);

        await _db.SaveChangesAsync(cancellationToken);

        if (isNewRegistration)
        {
            await _identityNotifications.NotifyWelcomeAsync(userByEmail.Id, cancellationToken);
            await TrySendWelcomeEmailAsync(userByEmail, cancellationToken);
        }

        return await IssueTokensAsync(userByEmail, ipAddress, cancellationToken);
    }

    public async Task<AuthenticationResponse> RefreshAsync(
        RefreshTokenRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_refreshValidator], cancellationToken);

        string tokenHash = RefreshTokenHashing.Hash(request.RefreshToken);
        RefreshToken? stored = await _db.RefreshTokens
            .Include(t => t.User)!
            .ThenInclude(u => u!.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null)
        {
            throw new DomainException("Invalid refresh token.") { Code = AuthErrorCodes.InvalidRefreshToken };
        }

        if (stored.RevokedAt is not null)
        {
            throw new DomainException("Refresh token has been revoked.") { Code = AuthErrorCodes.RevokedRefreshToken };
        }

        if (stored.ExpiresAt <= _clock.UtcNow)
        {
            throw new DomainException("Refresh token has expired.") { Code = AuthErrorCodes.ExpiredRefreshToken };
        }

        User user = stored.User ?? throw new NotFoundException("User", stored.UserId);
        user.EnsureCanAuthenticate();

        string newRaw = RefreshTokenHashing.CreateOpaqueToken();
        RefreshToken replacement = RefreshToken.Create(
            user.Id,
            RefreshTokenHashing.Hash(newRaw),
            _clock.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            _clock.UtcNow,
            ipAddress);

        stored.Revoke(_clock.UtcNow, ipAddress, replacement.Id);
        user.AddRefreshToken(replacement);
        _db.RefreshTokens.Add(replacement);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, newRaw, replacement.ExpiresAt, cancellationToken);
    }

    public async Task LogoutAsync(
        LogoutRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        await ValidationBehavior.ValidateAndThrowAsync(request, [_logoutValidator], cancellationToken);

        string tokenHash = RefreshTokenHashing.Hash(request.RefreshToken);
        RefreshToken? stored = await _db.RefreshTokens
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);

        if (stored is null || stored.RevokedAt is not null)
        {
            return;
        }

        stored.Revoke(_clock.UtcNow, ipAddress);
        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserResponse> GetMeAsync(CancellationToken cancellationToken = default)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            throw new UnauthorizedAccessException("Authentication is required.");
        }

        User user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == _currentUser.UserId.Value, cancellationToken)
            ?? throw new NotFoundException("User", _currentUser.UserId.Value);

        return MapUser(user);
    }

    public async Task RevokeAllSessionsAsync(
        Guid userId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        User user = await _db.Users
            .Include(u => u.RefreshTokens)
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException("User", userId);

        DateTimeOffset now = _clock.UtcNow;
        foreach (RefreshToken token in user.RefreshTokens.Where(t => t.RevokedAt is null))
        {
            token.Revoke(now, ipAddress);
        }

        user.IncrementSecurityStamp();
        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<AuthenticationResponse> IssueTokensAsync(
        User user,
        string? ipAddress,
        CancellationToken cancellationToken)
    {
        if (user.UserRoles.Count == 0 || user.UserRoles.Any(ur => ur.Role is null))
        {
            user = await _db.Users
                .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
                .FirstAsync(u => u.Id == user.Id, cancellationToken);
        }

        string rawRefresh = RefreshTokenHashing.CreateOpaqueToken();
        RefreshToken refreshToken = RefreshToken.Create(
            user.Id,
            RefreshTokenHashing.Hash(rawRefresh),
            _clock.UtcNow.AddDays(_jwtSettings.RefreshTokenExpirationDays),
            _clock.UtcNow,
            ipAddress);

        user.AddRefreshToken(refreshToken);
        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(cancellationToken);

        return await BuildAuthResponseAsync(user, rawRefresh, refreshToken.ExpiresAt, cancellationToken);
    }

    private Task<AuthenticationResponse> BuildAuthResponseAsync(
        User user,
        string rawRefreshToken,
        DateTimeOffset refreshExpiresAt,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<string> roles = user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        AccessTokenResult access = _jwtTokenGenerator.GenerateAccessToken(
            user.Id,
            user.Email,
            roles,
            user.SecurityStamp);

        AuthenticationResponse response = new(
            access.Token,
            access.ExpiresAt,
            rawRefreshToken,
            refreshExpiresAt,
            MapUser(user, roles));

        return Task.FromResult(response);
    }

    private async Task TrySendWelcomeEmailAsync(User user, CancellationToken cancellationToken)
    {
        if (user.WelcomeEmailSent)
        {
            return;
        }

        try
        {
            await _emailSender.SendAsync(WelcomeEmailTemplate.Create(user.Email, user.FirstName), cancellationToken);
            user.MarkWelcomeEmailSent();
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Welcome email failed for user {UserId}. Account remains active.", user.Id);
        }
    }

    private async Task<Role> GetRequiredRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        return await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
            ?? throw new DomainException($"Required role '{roleName}' is not seeded.") { Code = AuthErrorCodes.RoleMissing };
    }

    private static UserResponse MapUser(User user, IReadOnlyList<string>? roles = null)
    {
        roles ??= user.UserRoles
            .Select(ur => ur.Role?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        return new UserResponse(user.Id, user.Email, user.FirstName, user.LastName, roles);
    }
}
