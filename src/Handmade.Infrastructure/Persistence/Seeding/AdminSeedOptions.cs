using System.Net.Mail;
using Handmade.Application.Identity;

namespace Handmade.Infrastructure.Persistence.Seeding;

public sealed class AdminSeedOptions
{
    public const string SectionName = "AdminSeed";

    public bool Enabled { get; set; }

    public string Email { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public void EnsureValidWhenEnabled()
    {
        if (!Enabled)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Email) || !IsEmail(Email))
        {
            throw new InvalidOperationException(
                "AdminSeed:Email must be a valid email when AdminSeed:Enabled is true.");
        }

        if (string.IsNullOrWhiteSpace(Password)
            || Password.Length < IdentityLimits.PasswordMinLength
            || Password.Length > IdentityLimits.PasswordMaxLength
            || !Password.Any(char.IsUpper)
            || !Password.Any(char.IsLower)
            || !Password.Any(char.IsDigit)
            || Password.All(char.IsLetterOrDigit))
        {
            throw new InvalidOperationException(
                "AdminSeed:Password must meet identity password rules when AdminSeed:Enabled is true.");
        }
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
