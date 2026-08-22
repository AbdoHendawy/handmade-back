using Handmade.Domain.Exceptions;
using Handmade.Domain.Notifications;

namespace Handmade.Domain.Tests;

public sealed class NotificationTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();

    [Fact]
    public void CreateForUser_SetsPendingUnread_AndTrimsFields()
    {
        Notification notification = Notification.CreateForUser(
            UserId,
            "  seller.application.approved  ",
            "  Approved  ",
            "  You are in.  ",
            "  seller.application.approved:abc  ",
            "  {\"id\":1}  ");

        Assert.Equal(UserId, notification.UserId);
        Assert.Equal("seller.application.approved", notification.Type);
        Assert.Equal("Approved", notification.Title);
        Assert.Equal("You are in.", notification.Body);
        Assert.Equal("{\"id\":1}", notification.DataJson);
        Assert.Equal("seller.application.approved:abc", notification.IdempotencyKey);
        Assert.False(notification.IsRead);
        Assert.Null(notification.ReadAt);
        Assert.Equal(NotificationDeliveryStatus.Pending, notification.DeliveryStatus);
        Assert.True(notification.CanDeliver);
        Assert.Equal(0, notification.AttemptCount);
    }

    [Fact]
    public void CreateForUser_EmptyUser_Throws()
    {
        DomainException exception = Assert.Throws<DomainException>(() =>
            Notification.CreateForUser(Guid.Empty, "type", "title", "body", "key"));

        Assert.Equal(NotificationErrorCodes.InvalidUser, exception.Code);
    }

    [Fact]
    public void MarkRead_IsIdempotent()
    {
        Notification notification = Notification.CreateForUser(UserId, "type", "title", "body", "key");
        DateTimeOffset first = DateTimeOffset.UtcNow;
        notification.MarkRead(first);
        notification.MarkRead(first.AddMinutes(1));

        Assert.True(notification.IsRead);
        Assert.Equal(first, notification.ReadAt);
    }

    [Fact]
    public void RegisterFailedAttempt_MarksFailed_AfterMaxAttempts()
    {
        Notification notification = Notification.CreateForUser(UserId, "type", "title", "body", "key");

        for (int i = 0; i < Notification.MaxDeliveryAttempts - 1; i++)
        {
            notification.RegisterFailedAttempt("timeout");
            Assert.Equal(NotificationDeliveryStatus.Pending, notification.DeliveryStatus);
            Assert.True(notification.CanDeliver);
        }

        notification.RegisterFailedAttempt("timeout");
        Assert.Equal(NotificationDeliveryStatus.Failed, notification.DeliveryStatus);
        Assert.False(notification.CanDeliver);
        Assert.Equal(Notification.MaxDeliveryAttempts, notification.AttemptCount);
        Assert.Equal("timeout", notification.LastError);
    }

    [Fact]
    public void MarkDelivered_ClearsLastError()
    {
        Notification notification = Notification.CreateForUser(UserId, "type", "title", "body", "key");
        notification.RegisterFailedAttempt("boom");
        notification.MarkDelivered();

        Assert.Equal(NotificationDeliveryStatus.Delivered, notification.DeliveryStatus);
        Assert.Null(notification.LastError);
        Assert.False(notification.CanDeliver);
    }
}
