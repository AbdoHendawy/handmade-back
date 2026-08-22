using Handmade.Domain.Catalog;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Orders.Services;

public enum CheckoutConcurrencyAction
{
    Retry,
    OrdersConflict,
    Rethrow
}

public static class CheckoutConcurrency
{
    public const int MaxAttempts = 2;

    public static CheckoutConcurrencyAction Decide(
        int attemptIndex,
        DbUpdateConcurrencyException exception,
        IReadOnlySet<Guid> mutatedProductIds,
        IReadOnlySet<Guid> mutatedVariantIds)
    {
        return Decide(
            attemptIndex,
            exception.Entries.Select(entry => entry.Entity).ToList(),
            mutatedProductIds,
            mutatedVariantIds);
    }

    public static CheckoutConcurrencyAction Decide(
        int attemptIndex,
        IReadOnlyList<object> conflictingEntities,
        IReadOnlySet<Guid> mutatedProductIds,
        IReadOnlySet<Guid> mutatedVariantIds)
    {
        if (!IsExpectedInventoryConflict(conflictingEntities, mutatedProductIds, mutatedVariantIds))
        {
            return CheckoutConcurrencyAction.Rethrow;
        }

        if (attemptIndex < MaxAttempts - 1)
        {
            return CheckoutConcurrencyAction.Retry;
        }

        return CheckoutConcurrencyAction.OrdersConflict;
    }

    public static bool IsExpectedInventoryConflict(
        IReadOnlyList<object> conflictingEntities,
        IReadOnlySet<Guid> mutatedProductIds,
        IReadOnlySet<Guid> mutatedVariantIds)
    {
        if (conflictingEntities.Count == 0)
        {
            return false;
        }

        foreach (object entity in conflictingEntities)
        {
            switch (entity)
            {
                case Product product when mutatedProductIds.Contains(product.Id):
                    continue;
                case ProductVariant variant when mutatedVariantIds.Contains(variant.Id):
                    continue;
                default:
                    return false;
            }
        }

        return true;
    }
}
