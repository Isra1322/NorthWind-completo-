namespace NorthWind.Sales.Backend.DataContexts.EFCore.Services;

internal class NorthWindSalesQueriesDataContext(IOptions<DBOptions> dbOptions)
    : NorthWindSalesContext(dbOptions), INorthWindSalesQueriesDataContext
{
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    }

    public Task<Repositories.Entities.Customer?> GetCustomerAsync(string customerId) =>
        Customers.FirstOrDefaultAsync(c => c.Id == customerId);

    public async Task<IEnumerable<Repositories.Entities.Product>> GetProductsAsync(IEnumerable<int> productIds)
    {
        int[] ids = productIds.Distinct().ToArray();
        return await Products.Where(p => ids.Contains(p.Id)).ToArrayAsync();
    }
}