namespace NorthWind.Membership.Frontend.RazorViews.Pages;
public partial class UserLoginPage : IDisposable
{
    private const string RouteTemplate = "/user/login";
    [Inject] public UserLoginViewModel ViewModel { get; set; } = null!;
    [Inject] public NavigationManager NavigationManager { get; set; } = null!;
    private ErrorBoundary? ErrorBoundaryRef;
    private void Recover() => ErrorBoundaryRef?.Recover();
    protected override void OnInitialized() => ViewModel.OnLogin += OnLogin;
    private void OnLogin()
    {
        var path = new Uri(NavigationManager.Uri).AbsolutePath.TrimEnd('/');
        if (path.EndsWith(RouteTemplate, StringComparison.OrdinalIgnoreCase)) NavigationManager.NavigateTo("");
        else NavigationManager.NavigateTo(NavigationManager.Uri);
    }
    public void Dispose() => ViewModel.OnLogin -= OnLogin;
}
