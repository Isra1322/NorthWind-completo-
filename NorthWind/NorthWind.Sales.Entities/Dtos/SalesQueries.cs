namespace NorthWind.Sales.Entities.Dtos;

public record LookupItem(string Id, string Name, decimal UnitPrice = 0, short UnitsInStock = 0,
    string? Identification = null, string? Address = null, string? Email = null, string? Phone = null,
    string? City = null, string? Country = null, string? PostalCode = null);
public record SearchPage(IReadOnlyList<LookupItem> Items, int Page, int TotalPages);
public record InvoiceSummary(int Id, string CustomerName, string CustomerId, DateTime OrderDate, decimal Total,
    string? CustomerIdentification = null);
public record InvoicePage(IReadOnlyList<InvoiceSummary> Items, int Page, int TotalPages, int TotalCount, DateOnly? CurrentDate = null);
public record TaxSettings(bool Enabled, decimal Rate, decimal DiscountRate = OrderAmounts.DefaultDiscountRate);
public record InvoiceLine(int ProductId, string ProductName, decimal UnitPrice, short Quantity)
{
    public decimal Amount => decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
}
public record InvoiceDto(int Id, string CustomerId, DateTime OrderDate,
    string ShipAddress, string ShipCity, string ShipCountry, string? ShipPostalCode,
    decimal Subtotal, decimal TaxRate, decimal TaxAmount, decimal Total,
    IReadOnlyList<InvoiceLine> Lines, bool TaxRecorded,
    string? CustomerName = null, string? CustomerIdentification = null,
    string? CustomerAddress = null, string? CustomerEmail = null, string? CustomerPhone = null,
    bool TaxRateRecorded = true, int VersionNumber = 1, DateTime? ModifiedAt = null)
{
    public string CustomerDisplayName => string.IsNullOrWhiteSpace(CustomerName) ? CustomerId : CustomerName;
    // Use recorded totals, never today's discount or tax configuration for historical invoices.
    public decimal DiscountAmount => TaxRecorded ? Math.Max(0, OrderAmounts.Round(Subtotal + TaxAmount - Total)) : 0;
    public decimal TaxableBase => Subtotal - DiscountAmount;
    public string TaxLabel => TaxRecorded && TaxRateRecorded
        ? $"IVA ({(TaxRate * 100).ToString("0.######", System.Globalization.CultureInfo.GetCultureInfo("es-ES"))} %)"
        : "IVA";
    public string? HistoricalNote => !TaxRecorded ? "IVA histórico no disponible. Se conservan los importes históricos disponibles."
        : DiscountAmount > 0 ? $"El total conserva un descuento histórico de {DiscountAmount.ToString("N2", System.Globalization.CultureInfo.GetCultureInfo("es-ES"))} sobre el subtotal."
        : null;
}

public sealed record UpdateInvoiceDto(string ShipAddress, string ShipCity, string ShipCountry,
    string? ShipPostalCode, IReadOnlyList<UpdateInvoiceLineDto> Lines);
public sealed record UpdateInvoiceLineDto(int ProductId, short Quantity);
public sealed record InvoiceVersionInfo(int VersionNumber, DateTime ModifiedAt, string ModifiedBy);
public sealed record InvoiceVersionDto(InvoiceVersionInfo Version, InvoiceDto Invoice, byte[] PdfContent);
