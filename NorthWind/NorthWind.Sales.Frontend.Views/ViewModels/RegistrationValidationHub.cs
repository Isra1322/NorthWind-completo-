using NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;
using NorthWind.Validation.Entities;
using NorthWind.Validation.Entities.Interfaces;

namespace NorthWind.Sales.Frontend.Views.ViewModels;

public sealed class RegistrationValidationHub(IEnumerable<IModelValidator<UserRegistrationViewModel>> validators)
    : ModelValidatorHub<UserRegistrationViewModel>(validators), IDeferredFieldValidation
{
    public bool DeferUntilBlur(string propertyName) => propertyName == nameof(UserRegistrationViewModel.Email);
}
