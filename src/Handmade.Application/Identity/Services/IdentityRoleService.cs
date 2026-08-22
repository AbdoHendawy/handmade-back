using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Identity;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Identity.Services;

public sealed class IdentityRoleService : IIdentityRoleService
{
    private readonly IApplicationDbContext _db;

    public IdentityRoleService(IApplicationDbContext db)
    {
        _db = db;
    }

    public async Task AssignRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        User user = await LoadUserWithRolesAsync(userId, cancellationToken);
        Role role = await GetRequiredRoleAsync(roleName, cancellationToken);
        user.AssignRole(role);
    }

    public async Task RemoveRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        User user = await LoadUserWithRolesAsync(userId, cancellationToken);
        Role role = await GetRequiredRoleAsync(roleName, cancellationToken);
        user.RemoveRole(role);
    }

    public async Task<bool> HasRoleAsync(
        Guid userId,
        string roleName,
        CancellationToken cancellationToken = default)
    {
        Role? role = await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken);
        if (role is null)
        {
            return false;
        }

        return await _db.UserRoles.AnyAsync(
            ur => ur.UserId == userId && ur.RoleId == role.Id,
            cancellationToken);
    }

    private async Task<User> LoadUserWithRolesAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await _db.Users
                   .Include(u => u.UserRoles)
                   .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
               ?? throw new NotFoundException("User", userId);
    }

    private async Task<Role> GetRequiredRoleAsync(string roleName, CancellationToken cancellationToken)
    {
        return await _db.Roles.FirstOrDefaultAsync(r => r.Name == roleName, cancellationToken)
               ?? throw new DomainException($"Required role '{roleName}' is not seeded.")
               {
                   Code = AuthErrorCodes.RoleMissing
               };
    }
}
