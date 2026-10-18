namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddViewsServices(this IServiceCollection services)
    {
        services.AddScoped<CreateOrderViewModel>();
        services.AddScoped<NorthWind.Validation.Entities.Interfaces.IModelValidatorHub<NorthWind.Membership.Frontend.RazorViews.ViewModels.UserRegistration.UserRegistrationViewModel>,
            NorthWind.Sales.Frontend.Views.ViewModels.RegistrationValidationHub>();
        services.AddModelValidator<CreateOrderViewModel, CreateOrderViewModelValidator>();
        services.AddModelValidator<CreateOrderViewModel, CreateOrderStockValidator>();
        return services;
    }
}
