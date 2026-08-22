using Handmade.Application.Abstractions.Identity;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Seller;
using Handmade.Domain.Seller;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace Handmade.Api.Authorization;

public sealed class SellerActiveRequirement : IAuthorizationRequirement;

public sealed class SellerActiveHandler : AuthorizationHandler<SellerActiveRequirement>
{
    private readonly IApplicationDbContext _db;
    private readonly ICurrentUser _currentUser;

    public SellerActiveHandler(IApplicationDbContext db, ICurrentUser currentUser)
    {
        _db = db;
        _currentUser = currentUser;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        SellerActiveRequirement requirement)
    {
        if (!_currentUser.IsAuthenticated || _currentUser.UserId is null)
        {
            return;
        }

        Guid userId = _currentUser.UserId.Value;
        bool isActive = await _db.SellerProfiles
            .AsNoTracking()
            .AnyAsync(
                p => p.UserId == userId && p.Status == SellerProfileStatus.Active,
                context.Resource is HttpContext httpContext
                    ? httpContext.RequestAborted
                    : CancellationToken.None);

        if (isActive)
        {
            context.Succeed(requirement);
        }
    }
}
