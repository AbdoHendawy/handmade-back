using Handmade.Application.Abstractions.Email;
using Microsoft.Extensions.Logging;

namespace Handmade.Infrastructure.Identity.Email;

public sealed class ConsoleEmailSender : IEmailSender
{
    private readonly ILogger<ConsoleEmailSender> _logger;

    public ConsoleEmailSender(ILogger<ConsoleEmailSender> logger)
    {
        _logger = logger;
    }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "EMAIL to={To} subject={Subject} bodyLength={BodyLength}",
            message.To,
            message.Subject,
            message.HtmlBody.Length);

        return Task.CompletedTask;
    }
}
