namespace NorthWind.Sales.Backend.BusinessObjects.Aggregates;

public class OrderAggregate : Order
{
    readonly List<OrderDetail> OrderDetailsField = [];

    public IReadOnlyCollection<OrderDetail> OrderDetails =>
        OrderDetailsField;

    public void CalculateTotals(decimal taxRate)
    {
        if (taxRate is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(taxRate));
        var amounts = NorthWind.Sales.Entities.Dtos.OrderAmounts.Calculate(
            OrderDetails.Select(d => d.UnitPrice * d.Quantity), taxRate,
            DiscountType == DiscountType.Percentage ? Discount / 100m : 0,
            DiscountType == DiscountType.Flat ? Discount : 0);
        Subtotal = amounts.Subtotal;
        TaxRate = taxRate;
        TaxAmount = amounts.TaxAmount;
        Total = amounts.Total;
    }

    public void AddDetail(int productId, decimal unitPrice, short quantity)
    {
        var ExistingOrderDetail =
            OrderDetailsField
            .FirstOrDefault(o => o.ProductId == productId);

        if (ExistingOrderDetail != default)
        {
            throw new InvalidOperationException("Un producto solo puede aparecer una vez en la orden.");
        }

        OrderDetailsField.Add(
            new OrderDetail(productId, unitPrice, quantity));
    }

    public static OrderAggregate From(CreateOrderDto orderDto)
    {
        OrderAggregate OrderAggregate = new OrderAggregate
        {
            RequestId = orderDto.RequestId,
            CustomerId = orderDto.CustomerId,
            ShipAddress = orderDto.ShipAddress,
            ShipCity = orderDto.ShipCity,
            ShipCountry = orderDto.ShipCountry,
            ShipPostalCode = orderDto.ShipPostalCode
        };

        foreach (var Item in orderDto.OrderDetails)
        {
            OrderAggregate.AddDetail(
                Item.ProductId,
                Item.UnitPrice,
                Item.Quantity);
        }

        return OrderAggregate;
    }
}
