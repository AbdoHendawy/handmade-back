using Handmade.Domain.Common;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity.Events;

namespace Handmade.Domain.Identity;

public sealed class User : AggregateRoot, IAuditable
{
    private readonly List<UserRole> _userRoles = [];
    private readonly List<ExternalLogin> _externalLogins = [];
    private readonly List<RefreshToken> _refreshTokens = [];

    private User()
    {
    }

    private User(
        Guid id,
        string email,
        string? passwordHash,
        string firstName,
        string lastName,
        bool isEmailVerified)
        : base(id)
    {
        Email = email;
        PasswordHash = passwordHash;
        FirstName = firstName;
        LastName = lastName;
        IsActive = true;
        IsEmailVerified = isEmailVerified;
        SecurityStamp = 0;
        WelcomeEmailSent = false;
    }

    public string Email { get; private set; } = string.Empty;

    public string? PasswordHash { get; private set; }

    public string FirstName { get; private set; } = string.Empty;

    public string LastName { get; private set; } = string.Empty;

    public bool IsActive { get; private set; }

    public bool IsEmailVerified { get; private set; }

    public DateTimeOffset? LastLoginAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool WelcomeEmailSent { get; private set; }

    public int SecurityStamp { get; private set; }

    public IReadOnlyCollection<UserRole> UserRoles => _userRoles.AsReadOnly();

    public IReadOnlyCollection<ExternalLogin> ExternalLogins => _externalLogins.AsReadOnly();

    public IReadOnlyCollection<RefreshToken> RefreshTokens => _refreshTokens.AsReadOnly();

    public static string NormalizeEmail(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            throw new DomainException("Email is required.") { Code = "invalid_email" };
        }

        return email.Trim().ToLowerInvariant();
    }

    public static User RegisterLocal(
        string email,
        string passwordHash,
        string firstName,
        string lastName)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new DomainException("Password hash is required.") { Code = "invalid_password" };
        }

        User user = CreateCore(email, passwordHash, firstName, lastName, isEmailVerified: false);
        user.Raise(new UserRegisteredEvent(user.Id, user.Email, user.FirstName));
        return user;
    }

    public static User RegisterExternal(
        string email,
        string firstName,
        string lastName,
        bool isEmailVerified)
    {
        User user = CreateCore(email, passwordHash: null, firstName, lastName, isEmailVerified);
        user.Raise(new UserRegisteredEvent(user.Id, user.Email, user.FirstName));
        return user;
    }

    private static User CreateCore(
        string email,
        string? passwordHash,
        string firstName,
        string lastName,
        bool isEmailVerified)
    {
        string normalizedEmail = NormalizeEmail(email);

        if (string.IsNullOrWhiteSpace(firstName))
        {
            throw new DomainException("First name is required.") { Code = "invalid_name" };
        }

        if (string.IsNullOrWhiteSpace(lastName))
        {
            throw new DomainException("Last name is required.") { Code = "invalid_name" };
        }

        return new User(
            CreateId(),
            normalizedEmail,
            passwordHash,
            firstName.Trim(),
            lastName.Trim(),
            isEmailVerified);
    }

    public void AssignRole(Role role)
    {
        ArgumentNullException.ThrowIfNull(role);

        if (_userRoles.Any(ur => ur.RoleId == role.Id))
        {
            return;
        }

        _userRoles.Add(new UserRole(Id, role.Id));
    }

    public void LinkExternalLogin(ExternalLogin externalLogin)
    {
        ArgumentNullException.ThrowIfNull(externalLogin);

        if (_externalLogins.Any(x =>
                x.Provider == externalLogin.Provider &&
                x.ProviderUserId == externalLogin.ProviderUserId))
        {
            throw new ConflictException("External login is already linked.");
        }

        _externalLogins.Add(externalLogin);

        if (!IsEmailVerified && !string.IsNullOrWhiteSpace(externalLogin.ProviderEmail))
        {
            IsEmailVerified = true;
        }
    }

    public void AddRefreshToken(RefreshToken refreshToken)
    {
        ArgumentNullException.ThrowIfNull(refreshToken);
        _refreshTokens.Add(refreshToken);
    }

    public void RecordLogin(DateTimeOffset at)
    {
        LastLoginAt = at;
    }

    public void MarkWelcomeEmailSent()
    {
        WelcomeEmailSent = true;
    }

    public void IncrementSecurityStamp()
    {
        SecurityStamp++;
    }

    public void Deactivate()
    {
        IsActive = false;
        IncrementSecurityStamp();
    }

    public void EnsureCanAuthenticate()
    {
        if (!IsActive)
        {
            throw new DomainException("Account is inactive.") { Code = "inactive_account" };
        }
    }
}
