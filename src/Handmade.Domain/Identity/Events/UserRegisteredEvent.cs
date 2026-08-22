using Handmade.Domain.Common;

namespace Handmade.Domain.Identity.Events;

public sealed class UserRegisteredEvent : IDomainEvent
{
    public UserRegisteredEvent(Guid userId, string email, string firstName)
    {
        UserId = userId;
        Email = email;
        FirstName = firstName;
        OccurredAt = DateTimeOffset.UtcNow;
    }

    public Guid UserId { get; }

    public string Email { get; }

    public string FirstName { get; }

    public DateTimeOffset OccurredAt { get; }
}
