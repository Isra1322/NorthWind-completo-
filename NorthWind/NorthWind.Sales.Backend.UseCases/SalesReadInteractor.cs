using Microsoft.Extensions.Options;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.BusinessObjects.ValueObjects;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Validation.Entities.ValueObjects;

namespace NorthWind.Sales.Backend.UseCases;

internal class SalesReadInteractor(ISalesReadRepository repository,
    IUserService userService, IOptions<SalesTaxOptions> taxOptions) : ISalesReadInputPort
{
    public Task<SearchPage> SearchProducts(string? id, string? name, int page)
    {
        GuardUser.AgainstUnauthenticated(userService);
        ValidateSearch(SearchInputRules.ValidateLookup(true, id, name));
        return repository.SearchProducts(id?.Trim(), name?.Trim(), Math.Max(1, page));
    }
    public Task<SearchPage> SearchCustomers(string? id, string? name, int page)
    {
        GuardUser.AgainstUnauthenticated(userService);
        var errors = SearchInputRules.ValidateLookup(false, id, name);
        if (errors.ContainsKey("Name") && !string.IsNullOrWhiteSpace(name))
        {
            var trimmed = name.Trim();
            if (trimmed.Length <= 50 && trimmed.All(c => char.IsLetterOrDigit(c) || c is '-' or '.' or ' ' or '\''))
            {
                errors.Remove("Name");
            }
        }
        ValidateSearch(errors);
        return repository.SearchCustomers(id?.Trim(), name?.Trim(), Math.Max(1, page));
    }
    public Task<SearchPage> SearchCustomersByIdentification(string? identification, string? name, int page)
    {
        GuardUser.AgainstUnauthenticated(userService);
        ValidateSearch(SearchInputRules.ValidateCustomerCedulaLookup(identification, name));
        return repository.SearchCustomersByIdentification(identification?.Trim(), name?.Trim(), Math.Max(1, page));
    }
    public Task<InvoiceDto?> GetInvoice(int id)
    {
        GuardUser.AgainstUnauthenticated(userService);
        return repository.GetInvoice(id);
    }
    public TaxSettings GetTaxSettings()
    {
        GuardUser.AgainstUnauthenticated(userService);
        return new(taxOptions.Value.Enabled, taxOptions.Value.EffectiveRate);
    }
    public Task<InvoicePage> SearchInvoices(string? search, int page)
    {
        GuardUser.AgainstUnauthenticated(userService);
        return repository.SearchInvoices(search?.Trim(), Math.Max(1, page));
    }
    public Task<InvoicePage> FilterInvoices(InvoiceSearchFilters filters, int page)
    {
        GuardUser.AgainstUnauthenticated(userService);
        ValidateSearch(SearchInputRules.ValidateInvoices(filters, DateOnly.FromDateTime(DateTime.Today)));
        return repository.FilterInvoices(filters.Normalize(), Math.Max(1, page));
    }
    private static void ValidateSearch(Dictionary<string, string> errors)
    {
        if (errors.Count > 0)
            throw new NorthWind.Exceptions.Entities.Exceptions.ValidationException(errors.Select(e =>
                new NorthWind.Validation.Entities.ValueObjects.ValidationError(e.Key, e.Value)));
    }
}
