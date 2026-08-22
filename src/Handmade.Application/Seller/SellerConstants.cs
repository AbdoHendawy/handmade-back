namespace Handmade.Application.Seller;

public static class SellerLimits
{
    public const int BusinessNameMinLength = 2;
    public const int BusinessNameMaxLength = 200;
    public const int DescriptionMinLength = 20;
    public const int DescriptionMaxLength = 2000;
    public const int PhoneMaxLength = 20;
    public const int ReasonMinLength = 10;
    public const int ReasonMaxLength = 1000;
}

public static class AuthorizationPolicies
{
    public const string SellerActive = "SellerActive";
}
