namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyContainer
{
    public static IServiceCollection AddDataContexts(
        this IServiceCollection services,
        Action<DBOptions> configureDBOptions)
    {
        services.Configure(configureDBOptions);
        services.AddOptions<NorthWind.Sales.Backend.BusinessObjects.ValueObjects.SalesTaxOptions>()
            .BindConfiguration("SalesTax")
            .Validate(o => !o.Enabled || o.Rate is > 0 and <= 1 && decimal.Round(o.Rate.Value, 6) == o.Rate.Value,
                "SalesTax:Rate debe ser mayor que 0 y hasta 1, con hasta seis decimales, cuando IVA está habilitado.");
        services.AddScoped<ISalesReadDataContext, SalesReadDataContext>();
        services.AddScoped<NorthWind.Sales.Backend.BusinessObjects.Interfaces.IInvoiceEditingService, InvoiceEditingService>();
        services.AddScoped<NorthWind.Sales.Backend.BusinessObjects.Interfaces.IInvoiceEditingService, InvoiceEditingService>();
        services.AddScoped<INorthWindSalesCommandsDataContext,
            NorthWindSalesCommandsDataContext>();
        services.AddScoped<INorthWindSalesQueriesDataContext,
            NorthWindSalesQueriesDataContext>();
        services.AddScoped<INorthWindDomainLogsDataContext,
            NorthWindDomainLogsDataContext>();
        return services;
    }
}
