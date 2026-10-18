using NorthWind.Sales.Backend.BusinessObjects.ValueObjects;

namespace NorthWind.Sales.Backend.Repositories.Repositories;

internal class QueriesRepository(INorthWindSalesQueriesDataContext context) : IQueriesRepository
{
    public async Task<decimal?> GetCustomerCurrentBalance(string customerId) =>
        (await context.GetCustomerAsync(customerId))?.CurrentBalance;

    public async Task<IEnumerable<ProductUnitsInStock>> GetProductsUnitsInStock(IEnumerable<int> productIds) =>
        (await context.GetProductsAsync(productIds))
            .Select(p => new ProductUnitsInStock(p.Id, p.UnitsInStock)).ToArray();
}