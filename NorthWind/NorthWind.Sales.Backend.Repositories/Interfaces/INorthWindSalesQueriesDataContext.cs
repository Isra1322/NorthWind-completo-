namespace NorthWind.Sales.Backend.Repositories.Interfaces;

public interface INorthWindSalesQueriesDataContext
{
    Task<Entities.Customer?> GetCustomerAsync(string customerId);
    Task<IEnumerable<Entities.Product>> GetProductsAsync(IEnumerable<int> productIds);
}