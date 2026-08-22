using Handmade.Application.Abstractions.Persistence;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Seller.Services;

internal static class SellerPersistence
{
    public static async Task SaveChangesAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConflictException("The resource was modified by another operation.")
            {
                Code = SellerErrorCodes.ConcurrencyConflict
            };
        }
    }
}
