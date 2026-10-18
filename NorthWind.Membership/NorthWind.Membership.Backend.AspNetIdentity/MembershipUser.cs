using Microsoft.AspNetCore.Identity;
namespace NorthWind.Membership.Backend.AspNetIdentity;
public class MembershipUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
}
