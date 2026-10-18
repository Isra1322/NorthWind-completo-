namespace NorthWind.Sales.Validators.Entities.Resources;

internal static class CreateOrderMessages
{
    private static readonly System.Resources.ResourceManager ResourceManager =
        new("NorthWind.Sales.Validators.Entities.Resources.CreateOrderMessages", typeof(CreateOrderMessages).Assembly);
    public static string ProductIdGreaterThanZero => ResourceManager.GetString(nameof(ProductIdGreaterThanZero))!;
    public static string QuantityGreaterThanZero => ResourceManager.GetString(nameof(QuantityGreaterThanZero))!;
    public static string UnitPriceGreaterThanZero => ResourceManager.GetString(nameof(UnitPriceGreaterThanZero))!;
    public static string CustomerIdRequired => ResourceManager.GetString(nameof(CustomerIdRequired))!;
    public static string CustomerIdRequiredLength => ResourceManager.GetString(nameof(CustomerIdRequiredLength))!;
    public static string ShipAddressRequired => ResourceManager.GetString(nameof(ShipAddressRequired))!;
    public static string ShipAddressMaximumLength => ResourceManager.GetString(nameof(ShipAddressMaximumLength))!;
    public static string ShipCityRequired => ResourceManager.GetString(nameof(ShipCityRequired))!;
    public static string ShipCityMinimumLength => ResourceManager.GetString(nameof(ShipCityMinimumLength))!;
    public static string ShipCityMaximumLength => ResourceManager.GetString(nameof(ShipCityMaximumLength))!;
    public static string ShipCountryRequired => ResourceManager.GetString(nameof(ShipCountryRequired))!;
    public static string ShipCountryMinimumLength => ResourceManager.GetString(nameof(ShipCountryMinimumLength))!;
    public static string ShipCountryMaximumLength => ResourceManager.GetString(nameof(ShipCountryMaximumLength))!;
    public static string ShipPostalCodeMaximumLength => ResourceManager.GetString(nameof(ShipPostalCodeMaximumLength))!;
    public static string OrderDetailsRequired => ResourceManager.GetString(nameof(OrderDetailsRequired))!;
    public static string OrderDetailsNotEmpty => ResourceManager.GetString(nameof(OrderDetailsNotEmpty))!;
}