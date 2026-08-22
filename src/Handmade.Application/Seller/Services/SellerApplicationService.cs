using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Behaviors;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Seller.Services;

public interface ISellerApplicationService
{
    Task<SellerApplicationResponse> SubmitAsync(
        SubmitSellerApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SellerApplicationResponse>> GetMineAsync(CancellationToken cancellationToken = default);

    Task<SellerApplicationResponse> CancelAsync(Guid applicationId, CancellationToken cancellationToken = default);
}

public sealed class SellerApplicationService : ISellerApplicationService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly ISellerNotificationService _notifications;
    private readonly IValidator<SubmitSellerApplicationRequest> _submitValidator;

    public SellerApplicationService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        ISellerNotificationService notifications,
        IValidator<SubmitSellerApplicationRequest> submitValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _notifications = notifications;
        _submitValidator = submitValidator;
    }

    public async Task<SellerApplicationResponse> SubmitAsync(
        SubmitSellerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid userId = SellerMapping.RequireUserId(_currentUser);
        await ValidationBehavior.ValidateAndThrowAsync(request, [_submitValidator], cancellationToken);

        bool alreadySeller = await _db.SellerProfiles.AnyAsync(p => p.UserId == userId, cancellationToken);
        if (alreadySeller)
        {
            throw new ConflictException("An approved seller cannot submit another application.")
            {
                Code = SellerErrorCodes.AlreadySeller
            };
        }

        bool pendingExists = await _db.SellerApplications.AnyAsync(
            a => a.UserId == userId && a.Status == SellerApplicationStatus.Pending,
            cancellationToken);
        if (pendingExists)
        {
            throw new ConflictException("A pending seller application already exists.")
            {
                Code = SellerErrorCodes.PendingApplicationExists
            };
        }

        SellerApplication application = SellerApplication.Submit(
            userId,
            request.BusinessName,
            request.Description,
            request.Phone);

        _db.SellerApplications.Add(application);
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        await _notifications.NotifyApplicationSubmittedAsync(userId, cancellationToken);

        return SellerMapping.ToResponse(application);
    }

    public async Task<IReadOnlyList<SellerApplicationResponse>> GetMineAsync(
        CancellationToken cancellationToken = default)
    {
        Guid userId = SellerMapping.RequireUserId(_currentUser);

        List<SellerApplication> applications = await _db.SellerApplications
            .AsNoTracking()
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.CreatedAt)
            .ToListAsync(cancellationToken);

        return applications.Select(SellerMapping.ToResponse).ToList();
    }

    public async Task<SellerApplicationResponse> CancelAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        Guid userId = SellerMapping.RequireUserId(_currentUser);

        SellerApplication application = await _db.SellerApplications
                                          .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
                                      ?? throw new NotFoundException("SellerApplication", applicationId);

        if (application.UserId != userId)
        {
            throw new NotFoundException("SellerApplication", applicationId);
        }

        application.Cancel();
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        return SellerMapping.ToResponse(application);
    }
}
