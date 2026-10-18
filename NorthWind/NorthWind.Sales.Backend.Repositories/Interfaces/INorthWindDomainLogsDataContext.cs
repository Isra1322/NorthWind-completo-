namespace NorthWind.Sales.Backend.Repositories.Interfaces;

public interface INorthWindDomainLogsDataContext
{
    Task AddLogAsync(Entities.DomainLog log);
    Task SaveChangesAsync();
}
