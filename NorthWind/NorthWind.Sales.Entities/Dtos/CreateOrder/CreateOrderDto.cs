namespace NorthWind.Sales.Entities.Dtos.CreateOrder;

public class CreateOrderDto(
    string customerId,
    string shipAddress,
    string shipCity,
    string shipCountry,
    string shipPostalCode,
    IEnumerable<CreateOrderDetailDto> orderDetails)
{
    public Guid? RequestId { get; init; }
    public string CustomerId => customerId;
    public string ShipAddress => shipAddress;
    public string ShipCity => shipCity;
    public string ShipCountry => shipCountry;
    public string ShipPostalCode => shipPostalCode;
    public IEnumerable<CreateOrderDetailDto> OrderDetails => orderDetails;
}
