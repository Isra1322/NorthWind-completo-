using Microsoft.AspNetCore.Components.Forms;

namespace NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration;

public class UserRegistrationViewModel(MembershipGateway gateway, IModelValidatorHub<UserRegistrationViewModel> validator)
{
    private EditContext? formContext;
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PasswordConfirmation { get; set; } = string.Empty;
    public string InformationMessage { get; private set; } = string.Empty;
    public bool IsRegistering { get; private set; }
    public EditContext FormContext => formContext ??= CreateFormContext();
    public IModelValidatorHub<UserRegistrationViewModel> Validator => validator;
    public ModelValidator<UserRegistrationViewModel>? ModelValidatorComponentReference { get; set; }

    private EditContext CreateFormContext()
    {
        var context = new EditContext(this);
        context.OnFieldChanged += (_, _) => ClearInformationMessage();
        return context;
    }

    public void ClearInformationMessage() => InformationMessage = string.Empty;

    private void ResetForm()
    {
        FirstName = LastName = Email = Password = PasswordConfirmation = string.Empty;
        formContext = CreateFormContext();
        ModelValidatorComponentReference = null;
    }

    public async Task Register()
    {
        if (IsRegistering) return;
        IsRegistering = true;
        InformationMessage = string.Empty;
        try
        {
            await gateway.RegisterAsync((UserRegistrationDto)this);
            ResetForm();
            InformationMessage = UserRegistrationMessages.RegisteredMessage;
        }
        catch (HttpRequestException ex) when (ex.Data["Errors"] is IEnumerable<ValidationError> && ModelValidatorComponentReference is not null)
        {
            ModelValidatorComponentReference.AddErrors((IEnumerable<ValidationError>)ex.Data["Errors"]!);
        }
        finally
        {
            IsRegistering = false;
        }
    }

    public static explicit operator UserRegistrationDto(UserRegistrationViewModel model) =>
        new(model.Email, model.Password, model.FirstName, model.LastName);
}
