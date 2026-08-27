using Handmade.Application.Abstractions.Email;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Handmade.Infrastructure.Identity.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;
    private readonly Func<MimeMessage, EmailOptions, CancellationToken, Task> _transport;

    public SmtpEmailSender(IOptions<EmailOptions> options, ILogger<SmtpEmailSender> logger)
        : this(options, logger, SendWithMailKitAsync)
    {
    }

    internal SmtpEmailSender(
        IOptions<EmailOptions> options,
        ILogger<SmtpEmailSender> logger,
        Func<MimeMessage, EmailOptions, CancellationToken, Task> transport)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentNullException.ThrowIfNull(transport);

        _options = options.Value;
        _logger = logger;
        _transport = transport;
    }

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        MimeMessage mime = SmtpMimeMessageFactory.Create(message, _options);

        try
        {
            await _transport(mime, _options, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "SMTP send failed to={To} subject={Subject} host={Host} port={Port}",
                message.To,
                message.Subject,
                _options.Host,
                _options.Port);
            throw;
        }
    }

    private static async Task SendWithMailKitAsync(
        MimeMessage message,
        EmailOptions options,
        CancellationToken cancellationToken)
    {
        using SmtpClient client = new();
        SecureSocketOptions socketOptions = options.EnableSsl
            ? SecureSocketOptions.StartTlsWhenAvailable
            : SecureSocketOptions.None;

        await client.ConnectAsync(options.Host, options.Port, socketOptions, cancellationToken);

        if (options.RequiresAuthentication)
        {
            await client.AuthenticateAsync(options.Username, options.Password, cancellationToken);
        }

        await client.SendAsync(message, cancellationToken);
        await client.DisconnectAsync(true, cancellationToken);
    }
}
