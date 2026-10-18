namespace NorthWind.Membership.Frontend.RazorViews.Components;
public partial class UserLoginComponent
{
    [Parameter, EditorRequired] public UserLoginViewModel ViewModel { get; set; } = null!;
}
