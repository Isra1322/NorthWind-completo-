namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddPresenters(
        this IServiceCollection services)
    {
        services.AddScoped<ICreateOrderOutputPort,
            CreateOrderPresenter>();
        services.AddScoped<NorthWind.Sales.Backend.BusinessObjects.Interfaces.IInvoicePdfPresenter,
            NorthWind.Sales.Backend.Presenters.InvoicePdfPresenter>();

        return services;
    }
}
