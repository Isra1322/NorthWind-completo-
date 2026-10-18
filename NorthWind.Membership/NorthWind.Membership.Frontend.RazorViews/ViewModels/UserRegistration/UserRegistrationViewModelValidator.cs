namespace NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
internal class UserRegistrationViewModelValidator : AbstractModelValidator<UserRegistrationViewModel>
{
    public UserRegistrationViewModelValidator(IValidationService<UserRegistrationViewModel> service) : base(service)
    {
        AddRuleFor(c => c.PasswordConfirmation).StopOnFirstFailure()
            .NotEmpty(UserRegistrationMessages.ConfirmPasswordRequiredErrorMessage).Equal(c => c.Password, UserRegistrationMessages.ConfirmPasswordErrorMessage);
    }
}
