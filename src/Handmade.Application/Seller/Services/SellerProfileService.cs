using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Behaviors;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Seller.Services;

public interface ISellerProfileService
{
    Task<SellerProfileResponse> GetMineAsync(CancellationToken cancellationToken = default);

    Task<SellerProfileResponse> UpdateMineAsync(
        UpdateSellerProfileRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class SellerProfileService : ISellerProfileService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IValidator<UpdateSellerProfileRequest> _updateValidator;

    public SellerProfileService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IValidator<UpdateSellerProfileRequest> updateValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _updateValidator = updateValidator;
    }

    public async Task<SellerProfileResponse> GetMineAsync(CancellationToken cancellationToken = default)
    {
        Guid userId = SellerMapping.RequireUserId(_currentUser);
        SellerProfile profile = await LoadOwnProfileAsync(userId, tracking: false, cancellationToken);
        return SellerMapping.ToResponse(profile);
    }

    public async Task<SellerProfileResponse> UpdateMineAsync(
        UpdateSellerProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid userId = SellerMapping.RequireUserId(_currentUser);
        await ValidationBehavior.ValidateAndThrowAsync(request, [_updateValidator], cancellationToken);

        SellerProfile profile = await LoadOwnProfileAsync(userId, tracking: true, cancellationToken);
        profile.UpdateProfile(request.BusinessName, request.Description, request.Phone);
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        return SellerMapping.ToResponse(profile);
    }

    private async Task<SellerProfile> LoadOwnProfileAsync(
        Guid userId,
        bool tracking,
        CancellationToken cancellationToken)
    {
        IQueryable<SellerProfile> query = _db.SellerProfiles;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken)
               ?? throw new NotFoundException("Seller profile was not found.");
    }
}
