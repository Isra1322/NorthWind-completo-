using NorthWind.Membership.Frontend.RazorViews.AuthenticationStateProviders;
namespace NorthWind.Membership.Frontend.RazorViews.Pages;
public partial class LogoutPage
{
    [Inject] public JWTAuthenticationStateProvider AuthenticationStateProvider { get; set; } = null!;
    [Inject] public NavigationManager NavigationManager { get; set; } = null!;
    private async ValueTask CloseSession()
    {
        await AuthenticationStateProvider.LogoutAsync();
        NavigationManager.NavigateTo("/");
    }
}
