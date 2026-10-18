namespace NorthWind.Membership.Frontend.RazorViews.Pages;
public partial class UserRegistrationPage
{
    [Inject] public UserRegistrationViewModel ViewModel { get; set; } = null!;
    private ErrorBoundary? ErrorBoundaryRef;
    private void Recover() => ErrorBoundaryRef?.Recover();
}
