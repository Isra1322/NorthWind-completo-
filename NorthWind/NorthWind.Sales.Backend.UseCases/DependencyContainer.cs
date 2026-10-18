namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddUseCasesServices(
        this IServiceCollection services)
    {
        services.AddScoped<ICreateOrderInputPort,
            CreateOrderInteractor>();
        services.AddScoped<NorthWind.Sales.Backend.BusinessObjects.Interfaces.ISalesReadInputPort,
            NorthWind.Sales.Backend.UseCases.SalesReadInteractor>();
        services.AddModelValidator<CreateOrderDto, CreateOrderCustomerValidator>();
        services.AddModelValidator<CreateOrderDto, CreateOrderProductValidator>();

        services.AddScoped<IDomainEventHandler<SpecialOrderCreatedEvent>,
            SendEMailWhenSpecialOrderCreatedEventHandler>();

        return services;
    }
}
