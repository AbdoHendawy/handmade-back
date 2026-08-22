using Handmade.Application.Abstractions.Email;
using Handmade.Application.Abstractions.Persistence;
using Handmade.Application.Seller.Email;
using Handmade.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Handmade.Application.Seller.Services;

public interface ISellerNotificationService
{
    Task NotifyApplicationSubmittedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task NotifyApplicationApprovedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task NotifyApplicationRejectedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task NotifySellerSuspendedAsync(Guid userId, CancellationToken cancellationToken = default);

    Task NotifySellerReactivatedAsync(Guid userId, CancellationToken cancellationToken = default);
}

public sealed class SellerNotificationService : ISellerNotificationService
{
    private readonly IApplicationDbContext _db;
    private readonly IEmailSender _emailSender;
    private readonly ILogger<SellerNotificationService> _logger;

    public SellerNotificationService(
        IApplicationDbContext db,
        IEmailSender emailSender,
        ILogger<SellerNotificationService> logger)
    {
        _db = db;
        _emailSender = emailSender;
        _logger = logger;
    }

    public Task NotifyApplicationSubmittedAsync(Guid userId, CancellationToken cancellationToken = default)
        => TrySendAsync(userId, SellerEmailTemplates.ApplicationSubmitted, "submitted", cancellationToken);

    public Task NotifyApplicationApprovedAsync(Guid userId, CancellationToken cancellationToken = default)
        => TrySendAsync(userId, SellerEmailTemplates.ApplicationApproved, "approved", cancellationToken);

    public Task NotifyApplicationRejectedAsync(Guid userId, CancellationToken cancellationToken = default)
        => TrySendAsync(userId, SellerEmailTemplates.ApplicationRejected, "rejected", cancellationToken);

    public Task NotifySellerSuspendedAsync(Guid userId, CancellationToken cancellationToken = default)
        => TrySendAsync(userId, SellerEmailTemplates.SellerSuspended, "suspended", cancellationToken);

    public Task NotifySellerReactivatedAsync(Guid userId, CancellationToken cancellationToken = default)
        => TrySendAsync(userId, SellerEmailTemplates.SellerReactivated, "reactivated", cancellationToken);

    private async Task TrySendAsync(
        Guid userId,
        Func<string, string, EmailMessage> factory,
        string notification,
        CancellationToken cancellationToken)
    {
        User? user = await _db.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken);

        if (user is null)
        {
            _logger.LogWarning("Skipping seller {Notification} email; user {UserId} was not found.", notification, userId);
            return;
        }

        try
        {
            await _emailSender.SendAsync(factory(user.Email, user.FirstName), cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Seller {Notification} email failed for user {UserId}. Business state is unchanged.",
                notification,
                userId);
        }
    }
}
