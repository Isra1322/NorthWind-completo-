namespace NorthWind.Sales.Backend.Repositories.Repositories;

internal class CommandsRepository(
    INorthWindSalesCommandsDataContext context) :
    ICommandsRepository
{
    public Task<int?> FindOrderId(Guid requestId, bool lockRequest = false) =>
        context.FindOrderId(requestId, lockRequest);

    public async Task CreateOrder(OrderAggregate order)
    {
        await context.PrepareSaleAsync(order);
        await context.AddOrderAsync(order);

        await context.AddOrderDetailsAsync(
            order.OrderDetails
            .Select(d => new Entities.OrderDetail
            {
                Order = order,
                ProductId = d.ProductId,
                Quantity = d.Quantity,
                UnitPrice = d.UnitPrice
            }).ToArray());
    }

    public async Task SaveChanges() =>
        await context.SaveChangesAsync();
}
