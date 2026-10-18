using NorthWind.DomainLogs.Entities.Interfaces;
using NorthWind.DomainLogs.Entities.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddDomainLogsServices(this IServiceCollection services)
    {
        services.AddScoped<IDomainLogger, DomainLogger>();
        return services;
    }
}
