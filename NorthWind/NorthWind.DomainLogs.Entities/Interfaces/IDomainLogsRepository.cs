using NorthWind.Entities.Interfaces;
using NorthWind.DomainLogs.Entities.ValueObjects;

namespace NorthWind.DomainLogs.Entities.Interfaces;

public interface IDomainLogsRepository : IUnitOfWork
{
    Task Add(DomainLog log);
}
