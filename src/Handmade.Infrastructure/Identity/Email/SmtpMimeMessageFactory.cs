using Handmade.Application.Abstractions.Email;
using MimeKit;

namespace Handmade.Infrastructure.Identity.Email;

/// <summary>
/// Builds a MimeKit message from application <see cref="EmailMessage"/> and SMTP options.
/// Exposed for adapter tests without contacting a real SMTP server.
/// </summary>
public static class SmtpMimeMessageFactory
{
    public static MimeMessage Create(EmailMessage message, EmailOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);

        MimeMessage mime = new();
        mime.From.Add(new MailboxAddress(
            string.IsNullOrWhiteSpace(options.FromName) ? options.FromAddress : options.FromName,
            options.FromAddress));
        mime.To.Add(MailboxAddress.Parse(message.To));
        mime.Subject = message.Subject;

        BodyBuilder body = new()
        {
            HtmlBody = message.HtmlBody
        };

        if (!string.IsNullOrWhiteSpace(message.TextBody))
        {
            body.TextBody = message.TextBody;
        }

        mime.Body = body.ToMessageBody();
        return mime;
    }
}
