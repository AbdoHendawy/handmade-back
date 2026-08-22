using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Cart;
using Handmade.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Cart.Services;

internal static class CartPersistence
{
    public static async Task SaveChangesAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict();
        }
        catch (DbUpdateException exception) when (IsUniqueViolation(exception))
        {
            throw Conflict();
        }
    }

    private static ConflictException Conflict()
    {
        return new ConflictException("The cart was modified by another request.")
        {
            Code = CartErrorCodes.ConcurrencyConflict
        };
    }

    private static bool IsUniqueViolation(DbUpdateException exception)
    {
        for (Exception? current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current.GetType().Name != "PostgresException")
            {
                continue;
            }

            object? sqlState = current.GetType().GetProperty("SqlState")?.GetValue(current);
            return sqlState is string state && state == "23505";
        }

        return false;
    }
}
