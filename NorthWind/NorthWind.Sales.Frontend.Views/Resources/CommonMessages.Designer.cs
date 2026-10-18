// Acceso tipado a los recursos de CommonMessages.resx.
namespace NorthWind.Sales.Frontend.Views.Resources;

internal static class CommonMessages
{
    private static readonly System.Resources.ResourceManager ResourceManager =
        new("NorthWind.Sales.Frontend.Views.Resources.CommonMessages", typeof(CommonMessages).Assembly);
    public static string OK => ResourceManager.GetString(nameof(OK))!;
    public static string NavBarBrandTitle => ResourceManager.GetString(nameof(NavBarBrandTitle))!;
    public static string HomeMenuItemTitle => ResourceManager.GetString(nameof(HomeMenuItemTitle))!;
    public static string AboutTitle => ResourceManager.GetString(nameof(AboutTitle))!;
}