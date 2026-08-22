using FluentValidation;
using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Abstractions.Time;
using Handmade.Application.Behaviors;
using Handmade.Application.Common;
using Handmade.Application.Seller.DTOs;
using Handmade.Domain.Exceptions;
using Handmade.Domain.Identity;
using Handmade.Domain.Seller;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Application.Seller.Services;

public interface IAdminSellerService
{
    Task<PagedResult<SellerApplicationResponse>> ListApplicationsAsync(
        string? status,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<SellerApplicationResponse> GetApplicationAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task<SellerApplicationResponse> ApproveAsync(Guid applicationId, CancellationToken cancellationToken = default);

    Task<SellerApplicationResponse> RejectAsync(
        Guid applicationId,
        RejectSellerApplicationRequest request,
        CancellationToken cancellationToken = default);

    Task<PagedResult<SellerProfileResponse>> ListSellersAsync(
        string? status,
        PagingQuery paging,
        CancellationToken cancellationToken = default);

    Task<SellerProfileResponse> GetSellerAsync(Guid sellerId, CancellationToken cancellationToken = default);

    Task<SellerProfileResponse> SuspendAsync(
        Guid sellerId,
        SuspendSellerRequest request,
        CancellationToken cancellationToken = default);

    Task<SellerProfileResponse> ReactivateAsync(Guid sellerId, CancellationToken cancellationToken = default);
}

public sealed class AdminSellerService : IAdminSellerService
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;
    private readonly IIdentityRoleService _identityRoleService;
    private readonly ISellerNotificationService _notifications;
    private readonly IValidator<RejectSellerApplicationRequest> _rejectValidator;
    private readonly IValidator<SuspendSellerRequest> _suspendValidator;

    public AdminSellerService(
        IApplicationDbContext db,
        ICurrentUser currentUser,
        IClock clock,
        IIdentityRoleService identityRoleService,
        ISellerNotificationService notifications,
        IValidator<RejectSellerApplicationRequest> rejectValidator,
        IValidator<SuspendSellerRequest> suspendValidator)
    {
        _db = db;
        _currentUser = currentUser;
        _clock = clock;
        _identityRoleService = identityRoleService;
        _notifications = notifications;
        _rejectValidator = rejectValidator;
        _suspendValidator = suspendValidator;
    }

    public async Task<PagedResult<SellerApplicationResponse>> ListApplicationsAsync(
        string? status,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        SellerMapping.RequireUserId(_currentUser);

        IQueryable<SellerApplication> query = _db.SellerApplications.AsNoTracking();
        SellerApplicationStatus? parsed = ParseApplicationStatus(status);
        if (parsed is not null)
        {
            query = query.Where(a => a.Status == parsed.Value);
        }

        int total = await query.CountAsync(cancellationToken);
        List<SellerApplication> items = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SellerApplicationResponse>(
            items.Select(SellerMapping.ToResponse).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<SellerApplicationResponse> GetApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        SellerMapping.RequireUserId(_currentUser);
        SellerApplication application = await _db.SellerApplications
                                            .AsNoTracking()
                                            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
                                        ?? throw new NotFoundException("SellerApplication", applicationId);

        return SellerMapping.ToResponse(application);
    }

    public async Task<SellerApplicationResponse> ApproveAsync(
        Guid applicationId,
        CancellationToken cancellationToken = default)
    {
        Guid adminId = SellerMapping.RequireUserId(_currentUser);

        SellerApplication application = await _db.SellerApplications
                                            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
                                        ?? throw new NotFoundException("SellerApplication", applicationId);

        bool profileExists = await _db.SellerProfiles.AnyAsync(
            p => p.UserId == application.UserId,
            cancellationToken);
        if (profileExists)
        {
            throw new ConflictException("This user already has a seller profile.")
            {
                Code = SellerErrorCodes.AlreadySeller
            };
        }

        DateTimeOffset now = _clock.UtcNow;
        application.Approve(adminId, now);
        SellerProfile profile = SellerProfile.CreateFromApproval(application, now);
        _db.SellerProfiles.Add(profile);
        await _identityRoleService.AssignRoleAsync(application.UserId, RoleNames.Seller, cancellationToken);
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        await _notifications.NotifyApplicationApprovedAsync(
            application.UserId,
            application.Id,
            profile.Id,
            cancellationToken);

        return SellerMapping.ToResponse(application);
    }

    public async Task<SellerApplicationResponse> RejectAsync(
        Guid applicationId,
        RejectSellerApplicationRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid adminId = SellerMapping.RequireUserId(_currentUser);
        await ValidationBehavior.ValidateAndThrowAsync(request, [_rejectValidator], cancellationToken);

        SellerApplication application = await _db.SellerApplications
                                            .FirstOrDefaultAsync(a => a.Id == applicationId, cancellationToken)
                                        ?? throw new NotFoundException("SellerApplication", applicationId);

        application.Reject(adminId, request.Reason, _clock.UtcNow);
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        await _notifications.NotifyApplicationRejectedAsync(
            application.UserId,
            application.Id,
            request.Reason,
            cancellationToken);

        return SellerMapping.ToResponse(application);
    }

    public async Task<PagedResult<SellerProfileResponse>> ListSellersAsync(
        string? status,
        PagingQuery paging,
        CancellationToken cancellationToken = default)
    {
        SellerMapping.RequireUserId(_currentUser);

        IQueryable<SellerProfile> query = _db.SellerProfiles.AsNoTracking();
        SellerProfileStatus? parsed = ParseProfileStatus(status);
        if (parsed is not null)
        {
            query = query.Where(p => p.Status == parsed.Value);
        }

        int total = await query.CountAsync(cancellationToken);
        List<SellerProfile> items = await query
            .OrderByDescending(p => p.CreatedAt)
            .Skip(paging.Skip)
            .Take(paging.NormalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SellerProfileResponse>(
            items.Select(SellerMapping.ToResponse).ToList(),
            paging.NormalizedPage,
            paging.NormalizedPageSize,
            total);
    }

    public async Task<SellerProfileResponse> GetSellerAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        SellerMapping.RequireUserId(_currentUser);
        SellerProfile profile = await _db.SellerProfiles
                                      .AsNoTracking()
                                      .FirstOrDefaultAsync(p => p.Id == sellerId, cancellationToken)
                                  ?? throw new NotFoundException("SellerProfile", sellerId);

        return SellerMapping.ToResponse(profile);
    }

    public async Task<SellerProfileResponse> SuspendAsync(
        Guid sellerId,
        SuspendSellerRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid adminId = SellerMapping.RequireUserId(_currentUser);
        await ValidationBehavior.ValidateAndThrowAsync(request, [_suspendValidator], cancellationToken);

        SellerProfile profile = await LoadTrackedProfileAsync(sellerId, cancellationToken);
        profile.Suspend(adminId, request.Reason, _clock.UtcNow);
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        await _notifications.NotifySellerSuspendedAsync(
            profile.UserId,
            profile.Id,
            request.Reason,
            cancellationToken);

        return SellerMapping.ToResponse(profile);
    }

    public async Task<SellerProfileResponse> ReactivateAsync(
        Guid sellerId,
        CancellationToken cancellationToken = default)
    {
        SellerMapping.RequireUserId(_currentUser);

        SellerProfile profile = await LoadTrackedProfileAsync(sellerId, cancellationToken);
        profile.Reactivate(_clock.UtcNow);
        await SellerPersistence.SaveChangesAsync(_db, cancellationToken);
        await _notifications.NotifySellerReactivatedAsync(profile.UserId, profile.Id, cancellationToken);

        return SellerMapping.ToResponse(profile);
    }

    private async Task<SellerProfile> LoadTrackedProfileAsync(Guid sellerId, CancellationToken cancellationToken)
    {
        return await _db.SellerProfiles.FirstOrDefaultAsync(p => p.Id == sellerId, cancellationToken)
               ?? throw new NotFoundException("SellerProfile", sellerId);
    }

    private static SellerApplicationStatus? ParseApplicationStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse(status, ignoreCase: true, out SellerApplicationStatus parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new DomainException($"Unknown application status '{status}'.") { Code = "invalid_status" };
    }

    private static SellerProfileStatus? ParseProfileStatus(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (Enum.TryParse(status, ignoreCase: true, out SellerProfileStatus parsed)
            && Enum.IsDefined(parsed))
        {
            return parsed;
        }

        throw new DomainException($"Unknown seller status '{status}'.") { Code = "invalid_status" };
    }
}
