using NorthWind.Membership.Backend.Core.Resources;
namespace NorthWind.Membership.Backend.Core.UseCases.UserLogin;
internal class UserLoginInteractor(IMembershipService membershipService, IUserLoginOutputPort presenter,
    IModelValidatorHub<UserCredentialsDto> validationService) : IUserLoginInputPort
{
    public async Task Handle(UserCredentialsDto userData)
    {
        Result<UserDto, IEnumerable<ValidationError>> result;
        if (!await validationService.Validate(userData)) result = new(validationService.Errors);
        else
        {
            var user = await membershipService.GetUserByCredentials(userData);
            result = user is null
                ? new(new[] { new ValidationError(nameof(userData.Password), UserLoginMessages.InvalidUserCredentialsErrorMessage) })
                : new(user);
        }
        await presenter.Handle(result);
    }
}
