namespace NorthWind.Sales.Backend.BusinessObjects.POCOEntities;

public class Order
{
    public int Id { get; set; }
    public Guid? RequestId { get; set; }
    public decimal? Subtotal { get; set; }
    public decimal? TaxRate { get; set; }
    public decimal? TaxAmount { get; set; }
    public decimal? Total { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public string ShipAddress { get; set; } = string.Empty;
    public string ShipCity { get; set; } = string.Empty;
    public string ShipCountry { get; set; } = string.Empty;
    public string ShipPostalCode { get; set; } = string.Empty;

    public ShippingType ShippingType { get; set; } = ShippingType.Road;
    public DiscountType DiscountType { get; set; } = DiscountType.Percentage;
    public decimal Discount { get; set; } = NorthWind.Sales.Entities.Dtos.OrderAmounts.DefaultDiscountRate * 100m;
    public DateTime OrderDate { get; set; } = DateTime.Now;
}
