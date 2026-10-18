namespace NorthWind.Sales.Backend.UseCases.Resources;

internal static class CreateOrderMessages
{
    private static readonly System.Resources.ResourceManager ResourceManager =
        new("NorthWind.Sales.Backend.UseCases.Resources.CreateOrderMessages", typeof(CreateOrderMessages).Assembly);
    public static string OrderCreationCancelledTemplate => ResourceManager.GetString(nameof(OrderCreationCancelledTemplate))!;
    public static string StartingPurchaseOrderCreation => ResourceManager.GetString(nameof(StartingPurchaseOrderCreation))!;
    public static string PurchaseOrderCreatedTemplate => ResourceManager.GetString(nameof(PurchaseOrderCreatedTemplate))!;
    public static string SendEmailSubject => ResourceManager.GetString(nameof(SendEmailSubject))!;
    public static string SendEmailBodyTemplate => ResourceManager.GetString(nameof(SendEmailBodyTemplate))!;
    public static string CustomerIdNotFoundError => ResourceManager.GetString(nameof(CustomerIdNotFoundError))!;
    public static string CustomerWithBalanceErrorTemplate => ResourceManager.GetString(nameof(CustomerWithBalanceErrorTemplate))!;
    public static string ProductIdNotFoundErrorTemplate => ResourceManager.GetString(nameof(ProductIdNotFoundErrorTemplate))!;
    public static string UnitsInStockLessThanQuantityErrorTemplate => ResourceManager.GetString(nameof(UnitsInStockLessThanQuantityErrorTemplate))!;
}