namespace Handmade.Domain.Identity;

public static class RoleNames
{
    public const string Customer = "Customer";
    public const string Seller = "Seller";
    public const string Admin = "Admin";

    public static readonly IReadOnlyList<string> All = [Customer, Seller, Admin];
}
