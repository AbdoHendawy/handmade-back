using Handmade.Application.Abstractions.Time;

namespace Handmade.Infrastructure.Services;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
