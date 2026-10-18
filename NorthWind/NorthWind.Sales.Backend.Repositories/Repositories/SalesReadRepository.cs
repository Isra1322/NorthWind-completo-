using NorthWind.Sales.Entities.Dtos;

namespace NorthWind.Sales.Backend.Repositories.Repositories;

internal class SalesReadRepository(ISalesReadDataContext context) : ISalesReadRepository
{
    public Task<SearchPage> SearchProducts(string? id, string? name, int page) => context.SearchProducts(id, name, page);
    public Task<SearchPage> SearchCustomers(string? id, string? name, int page) => context.SearchCustomers(id, name, page);
    public Task<SearchPage> SearchCustomersByIdentification(string? identification, string? name, int page) => context.SearchCustomersByIdentification(identification, name, page);
    public Task<InvoiceDto?> GetInvoice(int id) => context.GetInvoice(id);
    public Task<InvoicePage> SearchInvoices(string? search, int page) => context.SearchInvoices(search, page);
    public Task<InvoicePage> FilterInvoices(InvoiceSearchFilters filters, int page) => context.FilterInvoices(filters, page);
}
