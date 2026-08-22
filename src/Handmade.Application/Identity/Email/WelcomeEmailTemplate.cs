using Handmade.Application.Abstractions.Email;

namespace Handmade.Application.Identity.Email;

public static class WelcomeEmailTemplate
{
    public static EmailMessage Create(string toEmail, string firstName)
    {
        string safeName = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName.Trim();
        string subject = "Welcome to Handmade";
        string text =
            $"Hi {safeName},\n\nWelcome to Handmade!\n\nYour account has been successfully created.\nYou can now start exploring Handmade.\n";
        string html =
            $"<p>Hi {System.Net.WebUtility.HtmlEncode(safeName)},</p>" +
            "<p>Welcome to Handmade!</p>" +
            "<p>Your account has been successfully created.</p>" +
            "<p>You can now start exploring Handmade.</p>" +
            "<p><strong>Open Handmade</strong></p>";

        return new EmailMessage(toEmail, subject, html, text);
    }
}
