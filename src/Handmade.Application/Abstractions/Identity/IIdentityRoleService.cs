namespace Handmade.Application.Abstractions.Identity;

/// <summary>
/// Trusted internal role operations. Callers own the unit of work; this service does not save.
/// </summary>
public interface IIdentityRoleService
{
    Task AssignRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    Task RemoveRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);

    Task<bool> HasRoleAsync(Guid userId, string roleName, CancellationToken cancellationToken = default);
}
