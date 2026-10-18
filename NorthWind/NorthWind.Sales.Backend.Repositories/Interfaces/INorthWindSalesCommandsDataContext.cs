namespace NorthWind.Sales.Backend.Repositories.Interfaces;

public interface INorthWindSalesCommandsDataContext
{
    Task AddOrderAsync(Order order);
    Task AddOrderDetailsAsync(
        IEnumerable<Entities.OrderDetail> orderDetails);
    Task SaveChangesAsync();
    Task PrepareSaleAsync(OrderAggregate order);
    Task<int?> FindOrderId(Guid requestId, bool lockRequest = false);
}
