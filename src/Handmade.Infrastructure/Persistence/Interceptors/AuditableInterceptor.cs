using Handmade.Application.Abstractions.Time;
using Handmade.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Handmade.Infrastructure.Persistence.Interceptors;

public sealed class AuditableInterceptor : SaveChangesInterceptor
{
    private readonly IClock _clock;

    public AuditableInterceptor(IClock clock)
    {
        _clock = clock;
    }

    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        ApplyAuditTimestamps(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ApplyAuditTimestamps(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAuditTimestamps(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        DateTimeOffset utcNow = _clock.UtcNow;

        foreach (Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry<IAuditable> entry in
                 context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Property(nameof(IAuditable.CreatedAt)).CurrentValue = utcNow;
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = utcNow;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Property(nameof(IAuditable.UpdatedAt)).CurrentValue = utcNow;
            }
        }
    }
}
