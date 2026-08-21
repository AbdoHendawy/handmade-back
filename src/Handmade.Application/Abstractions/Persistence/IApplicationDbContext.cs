namespace Handmade.Application.Abstractions.Persistence;

/// <summary>
/// Application-facing persistence boundary. Implemented by EF Core in Infrastructure.
/// DbSets are added when business entities are introduced.
/// </summary>
public interface IApplicationDbContext
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
