using Microsoft.EntityFrameworkCore.Design;

namespace NorthWind.Sales.Backend.DataContexts.EFCore.DataContexts;

internal class NorthWindDomainLogsContextFactory : IDesignTimeDbContextFactory<NorthWindDomainLogsContext>
{
    public NorthWindDomainLogsContext CreateDbContext(string[] args)
    {
        IOptions<DBOptions> dbOptions = Microsoft.Extensions.Options.Options.Create(
            new DBOptions
            {
                DomainLogsConnectionString = "Server=(localdb)\\mssqllocaldb;Database=NorthWindLogsDB"
            });
        return new NorthWindDomainLogsContext(dbOptions);
    }
}
