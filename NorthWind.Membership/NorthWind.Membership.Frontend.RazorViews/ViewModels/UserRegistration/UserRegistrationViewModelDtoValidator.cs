namespace NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
internal class UserRegistrationViewModelDtoValidator(IModelValidatorHub<UserRegistrationDto> validator)
    : AbstractViewModelValidator<UserRegistrationDto, UserRegistrationViewModel>(validator)
{
    protected override UserRegistrationDto ToModel(UserRegistrationViewModel viewModel) => (UserRegistrationDto)viewModel;
}
