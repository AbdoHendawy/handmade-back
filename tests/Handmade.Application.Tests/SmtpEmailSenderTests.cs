using Handmade.Application.Abstractions.Email;
using Handmade.Infrastructure.Identity.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Handmade.Application.Tests;

public sealed class SmtpEmailSenderTests
{
    [Fact]
    public void MimeMessage_MapsFromAddressNameToSubjectAndBodies()
    {
        EmailOptions options = ValidSmtp();
        EmailMessage message = new(
            "buyer@example.local",
            "Welcome to Handmade",
            "<p>Hello <b>Ada</b></p>",
            "Hello Ada");

        MimeMessage mime = SmtpMimeMessageFactory.Create(message, options);

        Assert.Equal("Handmade", mime.From.Mailboxes.Single().Name);
        Assert.Equal("noreply@example.local", mime.From.Mailboxes.Single().Address);
        Assert.Equal("buyer@example.local", mime.To.Mailboxes.Single().Address);
        Assert.Equal("Welcome to Handmade", mime.Subject);
        Assert.Contains("Hello Ada", mime.TextBody, StringComparison.Ordinal);
        Assert.Contains("<p>Hello <b>Ada</b></p>", mime.HtmlBody, StringComparison.Ordinal);
    }

    [Fact]
    public void MimeMessage_UsesFromAddressAsDisplayNameWhenFromNameEmpty()
    {
        EmailOptions options = ValidSmtp();
        options.FromName = string.Empty;

        MimeMessage mime = SmtpMimeMessageFactory.Create(
            new EmailMessage("a@b.c", "S", "<p>x</p>", "x"),
            options);

        Assert.Equal("noreply@example.local", mime.From.Mailboxes.Single().Name);
    }

    [Fact]
    public async Task SendAsync_UsesTransport_WithMappedMessage()
    {
        EmailOptions options = ValidSmtp();
        MimeMessage? sent = null;
        EmailOptions? usedOptions = null;

        SmtpEmailSender sender = new(
            Options.Create(options),
            NullLogger<SmtpEmailSender>.Instance,
            (mime, opts, _) =>
            {
                sent = mime;
                usedOptions = opts;
                return Task.CompletedTask;
            });

        EmailMessage message = new(
            "seller@example.local",
            "Your Seller Application Was Received",
            "<p>Thanks</p>",
            "Thanks");

        await sender.SendAsync(message);

        Assert.NotNull(sent);
        Assert.Same(options, usedOptions);
        Assert.Equal("seller@example.local", sent!.To.Mailboxes.Single().Address);
        Assert.Equal("Your Seller Application Was Received", sent.Subject);
        Assert.Equal("noreply@example.local", sent.From.Mailboxes.Single().Address);
        Assert.Contains("<p>Thanks</p>", sent.HtmlBody, StringComparison.Ordinal);
        Assert.Contains("Thanks", sent.TextBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SendAsync_TransportFailure_DoesNotLeakPasswordInExceptionOrLogMessage()
    {
        EmailOptions options = ValidSmtp();
        options.Password = "leaked-smtp-secret-value";

        SmtpEmailSender sender = new(
            Options.Create(options),
            NullLogger<SmtpEmailSender>.Instance,
            (_, _, _) => throw new InvalidOperationException("SMTP connection refused"));

        InvalidOperationException ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => sender.SendAsync(new EmailMessage("a@b.c", "S", "<p>x</p>")));

        Assert.DoesNotContain("leaked-smtp-secret-value", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("leaked-smtp-secret-value", ex.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void EnsureValidWhenSmtp_CanConstructSenderForValidConfig()
    {
        EmailOptions options = ValidSmtp();
        options.EnsureValidWhenSmtp();

        SmtpEmailSender sender = new(Options.Create(options), NullLogger<SmtpEmailSender>.Instance);
        Assert.NotNull(sender);
    }

    private static EmailOptions ValidSmtp()
    {
        return new EmailOptions
        {
            Provider = EmailOptions.SmtpProvider,
            Host = "smtp.example.local",
            Port = 587,
            Username = "smtp-user",
            Password = "secret-password",
            FromAddress = "noreply@example.local",
            FromName = "Handmade",
            EnableSsl = true
        };
    }
}
