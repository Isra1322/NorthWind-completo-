namespace NorthWind.Membership.Frontend.RazorViews.ViewModels.UserLogin;
public class UserLoginViewModel(MembershipGateway gateway, IModelValidatorHub<UserLoginViewModel> validator,
    NorthWind.Membership.Frontend.RazorViews.AuthenticationStateProviders.JWTAuthenticationStateProvider authenticationStateProvider)
{
    public IModelValidatorHub<UserLoginViewModel> Validator => validator;
    public ModelValidator<UserLoginViewModel>? ModelValidatorComponentReference { get; set; }
    public event Action? OnLogin;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public async Task Login()
    {
        try
        {
            TokensDto tokens = await gateway.LoginAsync((UserCredentialsDto)this);
            if (string.IsNullOrWhiteSpace(tokens.AccessToken))
                throw new InvalidOperationException("No se recibio el token de acceso.");
            var onLogin = OnLogin;
            await authenticationStateProvider.LoginAsync(tokens);
            onLogin?.Invoke();
        }
        catch (HttpRequestException ex) when (ex.Data["Errors"] is IEnumerable<ValidationError> && ModelValidatorComponentReference is not null)
        {
            ModelValidatorComponentReference.AddErrors((IEnumerable<ValidationError>)ex.Data["Errors"]!);
        }
    }
    public static explicit operator UserCredentialsDto(UserLoginViewModel model) => new(model.Email, model.Password);
}
