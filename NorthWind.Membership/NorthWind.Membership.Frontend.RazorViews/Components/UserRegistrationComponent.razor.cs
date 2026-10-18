namespace NorthWind.Membership.Frontend.RazorViews.Components;
public partial class UserRegistrationComponent
{
    private static readonly IReadOnlyDictionary<string, string[]> ValidationDependencies =
        new Dictionary<string, string[]> { [nameof(UserRegistrationViewModel.Password)] = [nameof(UserRegistrationViewModel.PasswordConfirmation)] };
    [Parameter, EditorRequired] public UserRegistrationViewModel ViewModel { get; set; } = null!;
}
