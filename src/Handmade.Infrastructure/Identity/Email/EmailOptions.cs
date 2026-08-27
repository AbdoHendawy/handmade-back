using System.Net.Mail;

namespace Handmade.Infrastructure.Identity.Email;

public sealed class EmailOptions
{
    public const string SectionName = "Email";

    public const string ConsoleProvider = "Console";

    public const string SmtpProvider = "SMTP";

    public string Provider { get; set; } = string.Empty;

    public string Host { get; set; } = string.Empty;

    public int Port { get; set; }

    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = string.Empty;

    public bool EnableSsl { get; set; } = true;

    public bool IsSmtp => string.Equals(Provider, SmtpProvider, StringComparison.OrdinalIgnoreCase);

    public bool IsConsole =>
        string.IsNullOrWhiteSpace(Provider)
        || string.Equals(Provider, ConsoleProvider, StringComparison.OrdinalIgnoreCase);

    public bool RequiresAuthentication =>
        !string.IsNullOrWhiteSpace(Username) || !string.IsNullOrWhiteSpace(Password);

    public void EnsureValidWhenSmtp()
    {
        if (!IsSmtp)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new InvalidOperationException("Email:Host is required when Email:Provider is SMTP.");
        }

        if (Port is < 1 or > 65535)
        {
            throw new InvalidOperationException("Email:Port must be between 1 and 65535 when Email:Provider is SMTP.");
        }

        if (string.IsNullOrWhiteSpace(FromAddress) || !IsEmail(FromAddress))
        {
            throw new InvalidOperationException(
                "Email:FromAddress must be a valid email when Email:Provider is SMTP.");
        }

        if (RequiresAuthentication
            && (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password)))
        {
            throw new InvalidOperationException(
                "Email:Username and Email:Password are both required when SMTP authentication is configured.");
        }
    }

    /// <summary>
    /// Outside Development, Console/empty provider is not allowed (ADR-016).
    /// </summary>
    public void EnsureAllowedForEnvironment(bool isDevelopment)
    {
        if (isDevelopment)
        {
            return;
        }

        if (IsConsole)
        {
            throw new InvalidOperationException(
                "Email:Provider must be SMTP outside Development. Console email is Development-only.");
        }

        EnsureValidWhenSmtp();
    }

    private static bool IsEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
