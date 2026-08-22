using Handmade.Application.Abstractions.Email;

namespace Handmade.Api.Tests;

public sealed class RecordingEmailSender : IEmailSender
{
    private readonly object _gate = new();
    private readonly List<EmailMessage> _sent = [];

    public bool ThrowOnSend { get; set; }

    public IReadOnlyList<EmailMessage> Sent
    {
        get
        {
            lock (_gate)
            {
                return [.. _sent];
            }
        }
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("Simulated email failure.");
        }

        lock (_gate)
        {
            _sent.Add(message);
        }

        return Task.CompletedTask;
    }
}
