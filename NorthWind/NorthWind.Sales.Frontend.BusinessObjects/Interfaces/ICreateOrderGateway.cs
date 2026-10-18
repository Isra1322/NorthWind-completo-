namespace NorthWind.Sales.Frontend.BusinessObjects.Interfaces;

public interface ICreateOrderGateway
{
    Task<int> CreateOrderAsync(CreateOrderDto order);
    Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchAsync(bool products, string id, string name, int page);
    Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchCustomersByIdentificationAsync(string identification, string name, int page) =>
        SearchAsync(false, identification, name, page);
    Task<NorthWind.Sales.Entities.Dtos.TaxSettings> GetTaxSettingsAsync();
    Task<NorthWind.Sales.Entities.Dtos.InvoiceDto?> GetInvoiceAsync(int id);
    Task<byte[]> GetInvoicePdfAsync(int id);
    Task<NorthWind.Sales.Entities.Dtos.InvoicePage> FilterInvoicesAsync(NorthWind.Sales.Entities.Dtos.InvoiceSearchFilters filters, int page);
    Task<NorthWind.Sales.Entities.Dtos.InvoicePage> FilterInvoicesAsync(NorthWind.Sales.Entities.Dtos.InvoiceSearchFilters filters, int page, CancellationToken cancellationToken) => FilterInvoicesAsync(filters, page).WaitAsync(cancellationToken);
    Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchAsync(bool products, string id, string name, int page, CancellationToken cancellationToken) => SearchAsync(products, id, name, page).WaitAsync(cancellationToken);
    Task<NorthWind.Sales.Entities.Dtos.SearchPage> SearchCustomersByIdentificationAsync(string identification, string name, int page, CancellationToken cancellationToken) => SearchCustomersByIdentificationAsync(identification, name, page).WaitAsync(cancellationToken);
    Task<NorthWind.Sales.Entities.Dtos.InvoiceVersionInfo> UpdateInvoiceAsync(int id, NorthWind.Sales.Entities.Dtos.UpdateInvoiceDto update) => throw new NotSupportedException();
    Task<IReadOnlyList<NorthWind.Sales.Entities.Dtos.InvoiceVersionInfo>> GetInvoiceVersionsAsync(int id) => throw new NotSupportedException();
    Task<NorthWind.Sales.Entities.Dtos.InvoiceVersionDto?> GetInvoiceVersionAsync(int id, int version) => throw new NotSupportedException();
    Task<NorthWind.Sales.Entities.Dtos.LookupItem?> GetProductForEditAsync(int productId) => throw new NotSupportedException();
}
