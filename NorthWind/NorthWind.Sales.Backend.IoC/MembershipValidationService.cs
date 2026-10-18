using NorthWind.Membership.Backend.Core.Interfaces.Common;
using NorthWind.Membership.Backend.Core.Dtos;
using NorthWind.Membership.Entities.Dtos.UserLogin;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Result.Entities;
using NorthWind.Validation.Entities.ValueObjects;

namespace NorthWind.Sales.Backend.IoC;

internal sealed class MembershipValidationService(IMembershipService inner) : IMembershipService
{
    public Task<UserDto?> GetUserByCredentials(UserCredentialsDto userData) => inner.GetUserByCredentials(userData);
    public async Task<Result<IEnumerable<ValidationError>>> Register(UserRegistrationDto userData)
    {
        var result = await inner.Register(userData);
        return result.HasError
            ? new(result.ErrorValue.DistinctBy(error => (error.PropertyName, error.Message)).ToArray())
            : result;
    }
}
