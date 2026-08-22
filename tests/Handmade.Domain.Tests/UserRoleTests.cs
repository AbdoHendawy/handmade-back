using Handmade.Domain.Identity;

namespace Handmade.Domain.Tests;

public sealed class UserRoleTests
{
    [Fact]
    public void RemoveRole_RemovesAssignedRole()
    {
        User user = User.RegisterLocal("a@b.com", "hash", "A", "B");
        Role seller = Role.Create(RoleNames.Seller);
        user.AssignRole(seller);

        Assert.Contains(user.UserRoles, ur => ur.RoleId == seller.Id);

        user.RemoveRole(seller);

        Assert.DoesNotContain(user.UserRoles, ur => ur.RoleId == seller.Id);
    }

    [Fact]
    public void RemoveRole_WhenMissing_IsIdempotent()
    {
        User user = User.RegisterLocal("a@b.com", "hash", "A", "B");
        Role seller = Role.Create(RoleNames.Seller);

        user.RemoveRole(seller);

        Assert.Empty(user.UserRoles);
    }
}
