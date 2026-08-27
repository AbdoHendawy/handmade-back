using Handmade.Infrastructure.Identity.Email;

namespace Handmade.Application.Tests;

public sealed class EmailOptionsTests
{
    [Fact]
    public void EmptyProvider_DoesNotRequireSmtpSettings()
    {
        new EmailOptions().EnsureValidWhenSmtp();
    }

    [Fact]
    public void ConsoleProvider_DoesNotRequireSmtpSettings()
    {
        new EmailOptions { Provider = "Console" }.EnsureValidWhenSmtp();
    }

    [Fact]
    public void Smtp_ValidSettings_Pass()
    {
        ValidSmtp().EnsureValidWhenSmtp();
    }

    [Fact]
    public void Smtp_MissingHost_Fails()
    {
        EmailOptions options = ValidSmtp();
        options.Host = " ";
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenSmtp);
    }

    [Fact]
    public void Smtp_InvalidPort_Fails()
    {
        EmailOptions options = ValidSmtp();
        options.Port = 0;
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenSmtp);

        options.Port = 70000;
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenSmtp);
    }

    [Fact]
    public void Smtp_InvalidFromAddress_Fails()
    {
        EmailOptions options = ValidSmtp();
        options.FromAddress = "not-an-email";
        Assert.Throws<InvalidOperationException>(options.EnsureValidWhenSmtp);
    }

    [Fact]
    public void Smtp_UsernameWithoutPassword_Fails()
    {
        EmailOptions options = ValidSmtp();
        options.Password = string.Empty;
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(options.EnsureValidWhenSmtp);
        Assert.DoesNotContain("secret-password", ex.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(options.Username, ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Smtp_PasswordWithoutUsername_Fails_WithoutLeakingPassword()
    {
        EmailOptions options = ValidSmtp();
        options.Username = string.Empty;
        options.Password = "super-secret-smtp-password";
        InvalidOperationException ex = Assert.Throws<InvalidOperationException>(options.EnsureValidWhenSmtp);
        Assert.DoesNotContain("super-secret-smtp-password", ex.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Smtp_NoAuth_DoesNotRequireCredentials()
    {
        EmailOptions options = ValidSmtp();
        options.Username = string.Empty;
        options.Password = string.Empty;
        options.EnsureValidWhenSmtp();
    }

    private static EmailOptions ValidSmtp()
    {
        return new EmailOptions
        {
            Provider = "SMTP",
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
