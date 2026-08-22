using System.Security.Claims;
using System.Text;
using Handmade.Api.Authorization;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Identity;
using Handmade.Application.Identity.Services;
using Handmade.Application.Notifications;
using Handmade.Application.Seller;
using Handmade.Domain.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Handmade.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddHandmadeAuthentication(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, Identity.CurrentUser>();

        JwtSettings jwt = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.SecretKey)),
                    ClockSkew = TimeSpan.FromMinutes(1),
                    RoleClaimType = ClaimTypes.Role,
                    NameClaimType = AuthClaimTypes.Subject
                };

                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        string? accessToken = context.Request.Query["access_token"];
                        PathString path = context.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(accessToken) &&
                            path.StartsWithSegments(NotificationHubRoutes.Notifications))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    },
                    OnTokenValidated = async context =>
                    {
                        ClaimsPrincipal principal = context.Principal
                            ?? throw new SecurityTokenException("Missing principal.");

                        string? userIdValue = principal.FindFirstValue(AuthClaimTypes.Subject);
                        string? stampValue = principal.FindFirstValue(AuthClaimTypes.SecurityStamp);

                        if (!Guid.TryParse(userIdValue, out Guid userId) ||
                            !int.TryParse(stampValue, out int stamp))
                        {
                            context.Fail("Invalid token claims.");
                            return;
                        }

                        IApplicationDbContext db =
                            context.HttpContext.RequestServices.GetRequiredService<IApplicationDbContext>();

                        User? user = await db.Users
                            .AsNoTracking()
                            .FirstOrDefaultAsync(u => u.Id == userId, context.HttpContext.RequestAborted);

                        if (user is null || !user.IsActive || user.SecurityStamp != stamp)
                        {
                            context.Fail("Token is no longer valid.");
                        }
                    }
                };
            });

        services.AddScoped<IAuthorizationHandler, SellerActiveHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.SellerActive, policy =>
            {
                policy.RequireAuthenticatedUser();
                policy.Requirements.Add(new SellerActiveRequirement());
            });
        });
        return services;
    }
}
