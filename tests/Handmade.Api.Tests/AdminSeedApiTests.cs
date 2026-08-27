using System.Net.Http.Json;
using System.Text.Json;
using Handmade.Application.Identity.DTOs;
using Handmade.Domain.Identity;
using Handmade.Infrastructure.Persistence;
using Handmade.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Handmade.Api.Tests;

[Collection(nameof(ApiCollection))]
public sealed class AdminSeedApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly HandmadeApiFactory _factory;

    public AdminSeedApiTests(HandmadeApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SeededAdmin_HasAdminRole_AndCanLogin()
    {
        HttpClient client = _factory.CreateMigratedClient();

        using IServiceScope scope = _factory.Services.CreateScope();
        HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        string email = User.NormalizeEmail(HandmadeApiFactory.SeededAdminEmail);
        User admin = await db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleAsync(u => u.Email == email);

        Assert.True(admin.IsActive);
        Assert.Contains(admin.UserRoles, ur => ur.Role!.Name == RoleNames.Admin);

        AuthenticationResponse login = (await (await client.PostAsJsonAsync(
            "/api/v1/auth/login",
            new LoginRequest(HandmadeApiFactory.SeededAdminEmail, HandmadeApiFactory.SeededAdminPassword))).Content
            .ReadFromJsonAsync<AuthenticationResponse>(JsonOptions))!;

        Assert.Equal(email, login.User.Email);
        Assert.Contains(RoleNames.Admin, login.User.Roles);
    }

    [Fact]
    public async Task RepeatedSeeding_DoesNotCreateDuplicates_OrResetPassword()
    {
        _factory.CreateMigratedClient();
        string email = User.NormalizeEmail(HandmadeApiFactory.SeededAdminEmail);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
            User before = await db.Users.SingleAsync(u => u.Email == email);
            string hash = before.PasswordHash!;

            await IdentitySeed.SeedAdminAsync(_factory.Services);
            await IdentitySeed.SeedAdminAsync(_factory.Services);

            int count = await db.Users.CountAsync(u => u.Email == email);
            Assert.Equal(1, count);
            User after = await db.Users.AsNoTracking().SingleAsync(u => u.Email == email);
            Assert.Equal(hash, after.PasswordHash);
        }
    }

    [Fact]
    public async Task RepeatedSeeding_RestoresMissingAdminRole()
    {
        _factory.CreateMigratedClient();
        string email = User.NormalizeEmail(HandmadeApiFactory.SeededAdminEmail);

        using (IServiceScope scope = _factory.Services.CreateScope())
        {
            HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
            User admin = await db.Users.Include(u => u.UserRoles).SingleAsync(u => u.Email == email);
            Role adminRole = await db.Roles.SingleAsync(r => r.Name == RoleNames.Admin);
            UserRole link = admin.UserRoles.Single(ur => ur.RoleId == adminRole.Id);
            db.UserRoles.Remove(link);
            await db.SaveChangesAsync();
        }

        await IdentitySeed.SeedAdminAsync(_factory.Services);

        using IServiceScope verify = _factory.Services.CreateScope();
        HandmadeDbContext verifyDb = verify.ServiceProvider.GetRequiredService<HandmadeDbContext>();
        User restored = await verifyDb.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .SingleAsync(u => u.Email == email);
        Assert.Contains(restored.UserRoles, ur => ur.Role!.Name == RoleNames.Admin);
    }
}
