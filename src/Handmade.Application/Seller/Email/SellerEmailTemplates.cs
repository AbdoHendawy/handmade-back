using Handmade.Application.Abstractions.Email;

namespace Handmade.Application.Seller.Email;

public static class SellerEmailTemplates
{
    public static EmailMessage ApplicationSubmitted(string toEmail, string firstName)
    {
        return Create(
            toEmail,
            firstName,
            "Your Seller Application Was Received",
            "We received your seller application. Our team will review it and contact you with an update.");
    }

    public static EmailMessage ApplicationApproved(string toEmail, string firstName)
    {
        return Create(
            toEmail,
            firstName,
            "Congratulations! Your Seller Account Is Approved",
            "Your seller application has been approved. You can now manage your seller profile. Refresh your session to pick up the Seller role.");
    }

    public static EmailMessage ApplicationRejected(string toEmail, string firstName)
    {
        return Create(
            toEmail,
            firstName,
            "Update About Your Seller Application",
            "Your seller application was not approved. You can review the reason in the app and submit a new application.");
    }

    public static EmailMessage SellerSuspended(string toEmail, string firstName)
    {
        return Create(
            toEmail,
            firstName,
            "Your Seller Account Has Been Suspended",
            "Your seller account has been suspended and seller-only actions are currently unavailable.");
    }

    public static EmailMessage SellerReactivated(string toEmail, string firstName)
    {
        return Create(
            toEmail,
            firstName,
            "Your Seller Account Has Been Reactivated",
            "Your seller account is active again. You can resume seller activity.");
    }

    private static EmailMessage Create(string toEmail, string firstName, string subject, string body)
    {
        string safeName = string.IsNullOrWhiteSpace(firstName) ? "there" : firstName.Trim();
        string encoded = System.Net.WebUtility.HtmlEncode(safeName);
        string text = $"Hi {safeName},\n\n{body}\n";
        string html = $"<p>Hi {encoded},</p><p>{System.Net.WebUtility.HtmlEncode(body)}</p>";
        return new EmailMessage(toEmail, subject, html, text);
    }
}
