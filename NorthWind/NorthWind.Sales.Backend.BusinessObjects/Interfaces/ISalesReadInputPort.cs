using NorthWind.Sales.Entities.Dtos;

namespace NorthWind.Sales.Backend.BusinessObjects.Interfaces;

public interface ISalesReadInputPort
{
    Task<SearchPage> SearchProducts(string? id, string? name, int page);
    Task<SearchPage> SearchCustomers(string? id, string? name, int page);
    Task<SearchPage> SearchCustomersByIdentification(string? identification, string? name, int page);
    Task<InvoiceDto?> GetInvoice(int id);
    Task<InvoicePage> SearchInvoices(string? search, int page);
    Task<InvoicePage> FilterInvoices(InvoiceSearchFilters filters, int page);
    TaxSettings GetTaxSettings();
}

public interface IInvoicePdfPresenter
{
    byte[] Render(InvoiceDto invoice);
}
