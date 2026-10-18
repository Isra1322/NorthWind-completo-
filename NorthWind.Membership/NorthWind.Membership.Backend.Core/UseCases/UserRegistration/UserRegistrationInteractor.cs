namespace NorthWind.Membership.Backend.Core.UseCases.UserRegistration;
internal class UserRegistrationInteractor(IMembershipService membershipService, IUserRegistrationOutputPort presenter,
    IModelValidatorHub<UserRegistrationDto> validationService) : IUserRegistrationInputPort
{
    public async Task Handle(UserRegistrationDto userData)
    {
        Result<IEnumerable<ValidationError>> result = !await validationService.Validate(userData)
            ? new(validationService.Errors) : await membershipService.Register(userData);
        await presenter.Handle(result);
    }
}
