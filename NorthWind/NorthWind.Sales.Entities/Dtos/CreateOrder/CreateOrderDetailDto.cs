namespace NorthWind.Sales.Entities.Dtos.CreateOrder;

public class CreateOrderDetailDto(
    int productId,
    decimal unitPrice,
    short quantity)
{
    public int ProductId => productId;
    public decimal UnitPrice => unitPrice;
    public short Quantity => quantity;
}
