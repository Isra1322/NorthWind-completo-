// Acceso tipado a los recursos de CreateOrderMessages.resx.
namespace NorthWind.Sales.Frontend.Views.Resources;

internal static class CreateOrderMessages
{
    private static readonly System.Resources.ResourceManager ResourceManager =
        new("NorthWind.Sales.Frontend.Views.Resources.CreateOrderMessages", typeof(CreateOrderMessages).Assembly);
    public static string CustomerIdLabel => ResourceManager.GetString(nameof(CustomerIdLabel))!;
    public static string ShipAddressLabel => ResourceManager.GetString(nameof(ShipAddressLabel))!;
    public static string ShipCityLabel => ResourceManager.GetString(nameof(ShipCityLabel))!;
    public static string ShipCountryLabel => ResourceManager.GetString(nameof(ShipCountryLabel))!;
    public static string ShipPostalCodeLabel => ResourceManager.GetString(nameof(ShipPostalCodeLabel))!;
    public static string ProductIdLabel => ResourceManager.GetString(nameof(ProductIdLabel))!;
    public static string UnitPriceLabel => ResourceManager.GetString(nameof(UnitPriceLabel))!;
    public static string QuantityLabel => ResourceManager.GetString(nameof(QuantityLabel))!;
    public static string AddProductButtonText => ResourceManager.GetString(nameof(AddProductButtonText))!;
    public static string SendButtonText => ResourceManager.GetString(nameof(SendButtonText))!;
    public static string CreatedOrderTemplate => ResourceManager.GetString(nameof(CreatedOrderTemplate))!;
    public static string CreateOrderMenuItemTitle => ResourceManager.GetString(nameof(CreateOrderMenuItemTitle))!;
    public static string CreateOrderPageTitle => ResourceManager.GetString(nameof(CreateOrderPageTitle))!;
}