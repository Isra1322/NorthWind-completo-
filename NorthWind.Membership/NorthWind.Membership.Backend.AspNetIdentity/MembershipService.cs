using Microsoft.AspNetCore.Identity;
using NorthWind.Membership.Entities.Dtos.UserLogin;
using NorthWind.Membership.Backend.Core.Dtos;
using NorthWind.Membership.Backend.Core.Interfaces.Common;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Result.Entities;
using NorthWind.Validation.Entities.ValueObjects;
namespace NorthWind.Membership.Backend.AspNetIdentity;
internal class MembershipService(UserManager<MembershipUser> userManager) : IMembershipService
{
    public async Task<UserDto?> GetUserByCredentials(UserCredentialsDto userData)
    {
        var user = await userManager.FindByNameAsync(userData.Email);
        if (user is not null && await userManager.CheckPasswordAsync(user, userData.Password))
            return new UserDto(user.UserName!, user.FirstName, user.LastName);
        return null;
    }

    public async Task<Result<IEnumerable<ValidationError>>> Register(UserRegistrationDto userData)
    {
        MembershipUser user = new()
        {
            UserName = userData.Email, Email = userData.Email,
            FirstName = userData.FirstName, LastName = userData.LastName
        };
        var result = await userManager.CreateAsync(user, userData.Password);
        if (result.Succeeded) return new();
        return new(result.Errors.Select(e => new ValidationError(
            e.Code is "FirstName" or "LastName" ? e.Code :
            e.Code.StartsWith("Password", StringComparison.Ordinal) ? "Password" :
            e.Code.Contains("Email", StringComparison.Ordinal) || e.Code.Contains("UserName", StringComparison.Ordinal) ? "Email" : string.Empty,
            e.Description)).ToArray());
    }
}
