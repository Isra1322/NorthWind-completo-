using NorthWind.Sales.Entities.Dtos;

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Services;

internal class SalesReadDataContext(IOptions<DBOptions> options)
    : NorthWindSalesContext(options), ISalesReadDataContext
{
    private const int PageSize = 8;
    private const int CustomerLookupPageSize = 5;
    private const int InvoicePageSize = 10;
    public async Task<InvoicePage> FilterInvoices(InvoiceSearchFilters filters, int page)
    {
        var orders = Orders.AsNoTracking().AsQueryable();
        if (filters.CustomerId.Length > 0)
            orders = orders.Where(o => o.CustomerId.ToUpper().Contains(filters.CustomerId));
        if (filters.Identification.Length > 0)
            orders = orders.Where(o => Customers.Any(c => c.Id == o.CustomerId &&
                c.Identification != null && c.Identification.Contains(filters.Identification)));
        if (filters.InvoiceNumber.Length > 0)
        {
            orders = orders.Where(o => o.Id.ToString().Contains(filters.InvoiceNumber));
        }
        if (filters.IssueDate.Length > 0)
        {
            // OrderDate stores the server's local wall-clock time. Do not convert it to UTC.
            var start = DateOnly.ParseExact(filters.IssueDate, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
                .ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
            var end = start.AddDays(1);
            orders = orders.Where(o => o.OrderDate >= start && o.OrderDate < end);
        }
        int count = await orders.CountAsync();
        int pages = Math.Max(1, (int)Math.Ceiling(count / (double)InvoicePageSize));
        page = Math.Clamp(page, 1, pages);
        var query = from order in orders
                    join customer in Customers.AsNoTracking() on order.CustomerId equals customer.Id into customers
                    from customer in customers.DefaultIfEmpty()
                    select new { Order = order, CustomerName = customer == null ? "" : customer.Name,
                        CustomerIdentification = customer == null ? null : customer.Identification };
        var items = await query.OrderByDescending(x => x.Order.OrderDate).ThenByDescending(x => x.Order.Id)
            .Skip((page - 1) * InvoicePageSize).Take(InvoicePageSize)
            .Select(x => new InvoiceSummary(x.Order.Id, x.CustomerName, x.Order.CustomerId, x.Order.OrderDate,
                x.Order.Total ?? (x.Order.Subtotal ?? OrderDetails.Where(d => d.OrderId == x.Order.Id)
                    .Sum(d => (decimal?)(d.UnitPrice * d.Quantity)) ?? 0m) + (x.Order.TaxAmount ?? 0m),
                x.CustomerIdentification))
            .ToArrayAsync();
        return new(items, page, pages, count, DateOnly.FromDateTime(DateTime.Today));
    }
    public async Task<InvoicePage> SearchInvoices(string? search, int page)
    {
        var query = from order in Orders.AsNoTracking()
                    join customer in Customers.AsNoTracking() on order.CustomerId equals customer.Id into customers
                    from customer in customers.DefaultIfEmpty()
                    select new { Order = order, CustomerName = customer == null ? "" : customer.Name,
                        CustomerIdentification = customer == null ? null : customer.Identification };
        if (!string.IsNullOrEmpty(search))
            query = query.Where(x => x.Order.Id.ToString().Contains(search) ||
                x.CustomerName.Contains(search) || x.Order.CustomerId.Contains(search));
        int count = await query.CountAsync();
        int pages = Math.Max(1, (int)Math.Ceiling(count / (double)InvoicePageSize));
        page = Math.Clamp(page, 1, pages);
        var items = await query.OrderByDescending(x => x.Order.OrderDate).ThenByDescending(x => x.Order.Id)
            .Skip((page - 1) * InvoicePageSize).Take(InvoicePageSize)
            .Select(x => new InvoiceSummary(x.Order.Id, x.CustomerName, x.Order.CustomerId, x.Order.OrderDate,
                x.Order.Total ?? (x.Order.Subtotal ?? OrderDetails.Where(d => d.OrderId == x.Order.Id)
                    .Sum(d => (decimal?)(d.UnitPrice * d.Quantity)) ?? 0m) + (x.Order.TaxAmount ?? 0m),
                x.CustomerIdentification))
            .ToArrayAsync();
        return new(items, page, pages, count);
    }
    public async Task<SearchPage> SearchProducts(string? id, string? name, int page)
    {
        var query = Products.AsNoTracking().Where(p => p.UnitsInStock > 0 && p.UnitPrice > 0);
        if (!string.IsNullOrEmpty(id))
        {
            if (int.TryParse(id, out int productId))
                query = query.Where(p => p.Id == productId);
            else
                query = query.Where(p => p.Id.ToString() == id);
        }
        if (!string.IsNullOrEmpty(name)) query = query.Where(p => p.Name.Contains(name));
        int pages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        return new(await query.OrderBy(p => p.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(p => new LookupItem(p.Id.ToString(), p.Name, p.UnitPrice, p.UnitsInStock, null, null, null, null, null, null, null)).ToArrayAsync(), page, pages);
    }
    public async Task<SearchPage> SearchCustomers(string? id, string? name, int page)
    {
        var query = Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(id)) query = query.Where(c => c.Id.Contains(id));
        if (!string.IsNullOrEmpty(name)) query = query.Where(c => c.Name.Contains(name) ||
            (c.Identification != null && c.Identification.Contains(name)));
        int pages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / (double)PageSize));
        page = Math.Clamp(page, 1, pages);
        return new(await query.OrderBy(c => c.Id).Skip((page - 1) * PageSize).Take(PageSize)
            .Select(c => new LookupItem(c.Id, c.Name, 0, 0, c.Identification, c.Address, c.Email, c.Phone,
                c.City, c.Country, c.PostalCode)).ToArrayAsync(), page, pages);
    }
    public async Task<SearchPage> SearchCustomersByIdentification(string? identification, string? name, int page)
    {
        var query = Customers.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(identification))
            query = query.Where(c => c.Identification != null && c.Identification.Contains(identification));
        if (!string.IsNullOrEmpty(name))
        {
            string normalizedName = name.ToUpperInvariant();
            query = query.Where(c => c.Name.ToUpper().Contains(normalizedName));
        }
        int pages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / (double)CustomerLookupPageSize));
        page = Math.Clamp(page, 1, pages);
        return new(await query.OrderBy(c => c.Id).Skip((page - 1) * CustomerLookupPageSize).Take(CustomerLookupPageSize)
            .Select(c => new LookupItem(c.Id, c.Name, 0, 0, c.Identification, c.Address, c.Email, c.Phone,
                c.City, c.Country, c.PostalCode)).ToArrayAsync(), page, pages);
    }
    public async Task<InvoiceDto?> GetInvoice(int id)
    {
        var order = await Orders.AsNoTracking().SingleOrDefaultAsync(o => o.Id == id);
        if (order is null) return null;
        var customer = await Customers.AsNoTracking().SingleOrDefaultAsync(c => c.Id == order.CustomerId);
        var lines = await (from detail in OrderDetails.AsNoTracking()
                           join product in Products.AsNoTracking() on detail.ProductId equals product.Id
                           where detail.OrderId == id
                           orderby detail.ProductId
                           select new InvoiceLine(detail.ProductId, detail.ProductName ?? product.Name,
                               detail.UnitPrice, detail.Quantity)).ToArrayAsync();
        // Historical orders retain their original prices; tax was not recorded before this migration.
        decimal subtotal = order.Subtotal ?? lines.Sum(l => l.Amount);
        decimal tax = order.TaxAmount ?? 0;
        return new(order.Id, order.CustomerId, order.OrderDate, order.ShipAddress, order.ShipCity,
            order.ShipCountry, order.ShipPostalCode, subtotal, order.TaxRate ?? 0,
            tax, order.Total ?? subtotal + tax, lines, order.TaxAmount.HasValue,
            customer?.Name, customer?.Identification, customer?.Address, customer?.Email, customer?.Phone,
            TaxRateRecorded: order.TaxRate.HasValue);
    }
}
