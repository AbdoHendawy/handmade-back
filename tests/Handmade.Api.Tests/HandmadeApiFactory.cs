using Handmade.Application.Abstractions.Email;
using Handmade.Domain.Identity;
using Handmade.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Handmade.Api.Tests;

public sealed class HandmadeApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16-alpine")
        .WithDatabase("handmade_test")
        .WithUsername("handmade")
        .WithPassword("handmade")
        .Build();

    private bool _migrated;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.UseSetting("ConnectionStrings:Default", _postgres.GetConnectionString());
        builder.UseSetting("Cors:AllowedOrigins:0", "http://localhost:4200");
        builder.UseSetting("Jwt:SecretKey", "TEST_SECRET_KEY_AT_LEAST_32_CHARS_LONG!!");
        builder.UseSetting("Jwt:Issuer", "Handmade");
        builder.UseSetting("Jwt:Audience", "Handmade");
        builder.UseSetting("Jwt:AccessTokenExpirationMinutes", "60");
        builder.UseSetting("Jwt:RefreshTokenExpirationDays", "14");
        builder.UseSetting("GoogleAuth:ClientId", string.Empty);
        builder.UseSetting("Hangfire:Enabled", "false");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<RecordingEmailSender>();
            services.AddSingleton<IEmailSender>(sp => sp.GetRequiredService<RecordingEmailSender>());
        });
    }

    public RecordingEmailSender Emails
    {
        get
        {
            EnsureMigrated();
            return Services.GetRequiredService<RecordingEmailSender>();
        }
    }

    public HttpClient CreateMigratedClient()
    {
        HttpClient client = CreateClient();
        EnsureMigrated();
        return client;
    }

    public void EnsureMigrated()
    {
        if (_migrated)
        {
            return;
        }

        using IServiceScope scope = Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        db.Database.Migrate();
        _migrated = true;
    }

    public async Task AssignRoleAsync(Guid userId, string roleName)
    {
        EnsureMigrated();
        using IServiceScope scope = Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        Role role = await db.Roles.SingleAsync(r => r.Name == roleName);
        bool exists = await db.UserRoles.AnyAsync(ur => ur.UserId == userId && ur.RoleId == role.Id);
        if (!exists)
        {
            db.UserRoles.Add(new UserRole(userId, role.Id));
            await db.SaveChangesAsync();
        }
    }
}

[CollectionDefinition(nameof(ApiCollection))]
public sealed class ApiCollection : ICollectionFixture<HandmadeApiFactory>;
