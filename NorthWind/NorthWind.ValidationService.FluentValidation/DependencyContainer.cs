using NorthWind.ValidationService.FluentValidation;

namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddValidationService(this IServiceCollection services)
    {
        // Each validator owns its rule builder; sharing it mixes rules from different validators.
        services.AddTransient(typeof(IValidationService<>), typeof(FluentValidationService<>));
        return services;
    }
}
