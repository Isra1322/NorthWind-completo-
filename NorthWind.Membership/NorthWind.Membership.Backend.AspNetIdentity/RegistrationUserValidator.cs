using Microsoft.AspNetCore.Identity;
using NorthWind.Membership.Entities.Dtos.UserRegistration;

namespace NorthWind.Membership.Backend.AspNetIdentity;

// Runs before persistence, including callers that use UserManager directly.
internal sealed class RegistrationUserValidator : IUserValidator<MembershipUser>
{
    public Task<IdentityResult> ValidateAsync(UserManager<MembershipUser> manager, MembershipUser user)
    {
        var errors = new List<IdentityError>();
        void Add(string field, string message) => errors.Add(new() { Code = field, Description = message });
        if (string.IsNullOrWhiteSpace(user.FirstName))
            Add(nameof(user.FirstName), "Debe especificar el nombre.");
        else if (user.FirstName.Length > RegistrationLimits.FirstName)
            Add(nameof(user.FirstName), RegistrationLimits.FirstNameMessage);
        if (string.IsNullOrWhiteSpace(user.LastName))
            Add(nameof(user.LastName), "Debe especificar los apellidos.");
        else if (user.LastName.Length > RegistrationLimits.LastName)
            Add(nameof(user.LastName), RegistrationLimits.LastNameMessage);
        if (user.Email?.Length > RegistrationLimits.Email ||
            manager.NormalizeEmail(user.Email)?.Length > RegistrationLimits.Email)
            Add(nameof(user.Email), RegistrationLimits.EmailMessage);
        return Task.FromResult(errors.Count == 0 ? IdentityResult.Success : IdentityResult.Failed(errors.ToArray()));
    }
}
