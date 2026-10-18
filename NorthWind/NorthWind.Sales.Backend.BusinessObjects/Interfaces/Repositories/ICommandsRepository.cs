namespace NorthWind.Sales.Backend.BusinessObjects.Interfaces.Repositories;

public interface ICommandsRepository : IUnitOfWork
{
    Task CreateOrder(OrderAggregate order);
    Task<int?> FindOrderId(Guid requestId, bool lockRequest = false);
}
