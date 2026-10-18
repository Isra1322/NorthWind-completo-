using NorthWind.Events.Entities.Interfaces;
using NorthWind.Events.Entities.Services;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddEventServices(this IServiceCollection services)
    {
        services.AddScoped(typeof(IDomainEventHub<>), typeof(DomainEventHub<>));
        return services;
    }
}
