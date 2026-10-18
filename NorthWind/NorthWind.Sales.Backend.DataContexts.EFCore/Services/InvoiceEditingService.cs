using System.Globalization;
using System.Text.Json;
using System.Transactions;
using NorthWind.Sales.Backend.BusinessObjects.Interfaces;
using NorthWind.Sales.Backend.Repositories.Entities;
using NorthWind.Sales.Entities.Dtos;
using NorthWind.Sales.Backend.BusinessObjects.Exceptions;

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Services;

internal sealed class InvoiceEditingService(
    IOptions<DBOptions> options,
    IOptions<NorthWind.Sales.Backend.BusinessObjects.ValueObjects.SalesTaxOptions> taxOptions,
    IInvoicePdfPresenter pdfPresenter) : IInvoiceEditingService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private NorthWindSalesContext CreateContext() => new(options);

    public async Task<InvoiceVersionInfo> UpdateAsync(int invoiceId, UpdateInvoiceDto update, string userName, CancellationToken cancellationToken = default)
    {
        try { return await UpdateCoreAsync(invoiceId, update, userName, cancellationToken); }
        catch (Exception ex) when (IsSqlDeadlock(ex))
        { throw new InvoiceConflictException("La factura o el inventario cambiaron simultáneamente. No se guardó la edición; vuelva a cargar la factura e intente nuevamente.", ex); }
    }

    private async Task<InvoiceVersionInfo> UpdateCoreAsync(int invoiceId, UpdateInvoiceDto update, string userName, CancellationToken cancellationToken)
    {
        Validate(update);
        await using var db = CreateContext();
        using var transaction = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.Serializable, Timeout = TimeSpan.FromMinutes(1) },
            TransactionScopeAsyncFlowOption.Enabled);
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken)
            ?? throw new KeyNotFoundException("No se encontró la factura.");

        var oldDetails = await db.OrderDetails.Where(x => x.OrderId == invoiceId).ToDictionaryAsync(x => x.ProductId, cancellationToken);
        var productIds = update.Lines.Select(x => x.ProductId).Distinct().ToArray();
        var products = await db.Products.Where(x => productIds.Contains(x.Id) || oldDetails.Keys.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        if (update.Lines.Any(x => !products.ContainsKey(x.ProductId)))
            throw new InvoiceConflictException("Uno o más productos ya no están disponibles.");

        var currentInvoice = await BuildInvoiceAsync(db, order, cancellationToken);
        var existingVersions = await db.InvoiceVersions.Where(x => x.InvoiceId == invoiceId)
            .OrderBy(x => x.VersionNumber).ToListAsync(cancellationToken);
        if (existingVersions.Count == 0)
        {
            existingVersions.Add(CreateVersion(invoiceId, 1, order.OrderDate, "Registro histórico", currentInvoice));
            db.InvoiceVersions.Add(existingVersions[0]);
            await SaveAsync(db, cancellationToken);
        }

        var newLines = update.Lines.ToDictionary(x => x.ProductId);
        foreach (var productId in oldDetails.Keys.Union(newLines.Keys))
        {
            var product = products[productId];
            int previousQuantity = oldDetails.TryGetValue(productId, out var oldLine) ? oldLine.Quantity : 0;
            int nextQuantity = newLines.TryGetValue(productId, out var nextLine) ? nextLine.Quantity : 0;
            int availableForThisInvoice = product.UnitsInStock + previousQuantity;
            if (nextQuantity > availableForThisInvoice)
                throw new InvoiceConflictException($"Stock insuficiente para {product.Name}. Disponible: {availableForThisInvoice}.");
            int adjustedStock = product.UnitsInStock + previousQuantity - nextQuantity;
            if (adjustedStock > short.MaxValue)
                throw new InvoiceConflictException($"El ajuste de {product.Name} excede el máximo de inventario permitido.");
            product.UnitsInStock = (short)adjustedStock;
        }

        var lines = update.Lines.Select(x =>
        {
            oldDetails.TryGetValue(x.ProductId, out var existingLine);
            return new InvoiceLine(x.ProductId, existingLine?.ProductName ?? products[x.ProductId].Name,
                existingLine?.UnitPrice ?? products[x.ProductId].UnitPrice, x.Quantity);
        }).ToArray();
        foreach (var line in lines)
        {
            if (oldDetails.TryGetValue(line.ProductId, out var existingLine))
            {
                existingLine.Quantity = line.Quantity;
                existingLine.ProductName = line.ProductName;
            }
            else
                db.OrderDetails.Add(new OrderDetail { OrderId = invoiceId, ProductId = line.ProductId, UnitPrice = line.UnitPrice,
                    Quantity = line.Quantity, ProductName = line.ProductName });
        }
        foreach (var oldLine in oldDetails.Values.Where(x => !newLines.ContainsKey(x.ProductId))) db.OrderDetails.Remove(oldLine);

        order.ShipAddress = update.ShipAddress.Trim();
        order.ShipCity = update.ShipCity.Trim();
        order.ShipCountry = update.ShipCountry.Trim();
        order.ShipPostalCode = string.IsNullOrWhiteSpace(update.ShipPostalCode) ? string.Empty : update.ShipPostalCode.Trim();
        decimal subtotal = decimal.Round(lines.Sum(x => x.Amount), 2, MidpointRounding.AwayFromZero);
        decimal rate = taxOptions.Value.Enabled ? taxOptions.Value.EffectiveRate : 0m;
        order.Subtotal = subtotal;
        order.TaxRate = rate;
        order.TaxAmount = decimal.Round(subtotal * rate, 2, MidpointRounding.AwayFromZero);
        order.Total = subtotal + order.TaxAmount.Value;
        order.Discount = 0m;

        await SaveAsync(db, cancellationToken);
        var updatedInvoice = await BuildInvoiceAsync(db, order, cancellationToken);
        int versionNumber = existingVersions.Max(x => x.VersionNumber) + 1;
        var version = CreateVersion(invoiceId, versionNumber, DateTime.Now, NormalizeUser(userName), updatedInvoice);
        db.InvoiceVersions.Add(version);
        await SaveAsync(db, cancellationToken);
        transaction.Complete();
        return new(version.VersionNumber, version.ModifiedAt, version.ModifiedBy);
    }

    public async Task<IReadOnlyList<InvoiceVersionInfo>> GetVersionsAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var order = await db.Orders.SingleOrDefaultAsync(x => x.Id == invoiceId, cancellationToken);
        if (order is null) return [];
        using var transaction = new TransactionScope(TransactionScopeOption.Required,
            new TransactionOptions { IsolationLevel = IsolationLevel.Serializable, Timeout = TimeSpan.FromMinutes(1) },
            TransactionScopeAsyncFlowOption.Enabled);
        var versions = await db.InvoiceVersions.Where(x => x.InvoiceId == invoiceId).OrderBy(x => x.VersionNumber).ToListAsync(cancellationToken);
        if (versions.Count == 0)
        {
            var invoice = await BuildInvoiceAsync(db, order, cancellationToken);
            var initial = CreateVersion(invoiceId, 1, order.OrderDate, "Registro histórico", invoice);
            db.InvoiceVersions.Add(initial);
            await SaveAsync(db, cancellationToken);
            versions.Add(initial);
        }
        transaction.Complete();
        return versions.Select(ToInfo).ToArray();
    }

    public async Task<InvoiceVersionDto?> GetVersionAsync(int invoiceId, int versionNumber, CancellationToken cancellationToken = default)
    {
        await GetVersionsAsync(invoiceId, cancellationToken);
        await using var db = CreateContext();
        var version = await db.InvoiceVersions.SingleOrDefaultAsync(x => x.InvoiceId == invoiceId && x.VersionNumber == versionNumber, cancellationToken);
        return version is null ? null : ToDto(version);
    }

    public async Task<InvoiceVersionDto?> GetCurrentVersionAsync(int invoiceId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var version = await db.InvoiceVersions.Where(x => x.InvoiceId == invoiceId).OrderByDescending(x => x.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        return version is null ? null : ToDto(version);
    }

    public async Task<LookupItem?> GetProductForEditAsync(int productId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext();
        var product = await db.Products.AsNoTracking().SingleOrDefaultAsync(x => x.Id == productId, cancellationToken);
        return product is null ? null : new(product.Id.ToString(CultureInfo.InvariantCulture), product.Name, product.UnitPrice, product.UnitsInStock);
    }

    private async Task<InvoiceDto> BuildInvoiceAsync(NorthWindSalesContext db, Order order, CancellationToken cancellationToken)
    {
        var customer = await db.Customers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == order.CustomerId, cancellationToken);
        var lines = await (from detail in db.OrderDetails.AsNoTracking()
                           join product in db.Products.AsNoTracking() on detail.ProductId equals product.Id
                           where detail.OrderId == order.Id
                           orderby detail.ProductId
                           select new InvoiceLine(detail.ProductId, detail.ProductName ?? product.Name, detail.UnitPrice, detail.Quantity))
            .ToArrayAsync(cancellationToken);
        decimal subtotal = order.Subtotal ?? lines.Sum(x => x.Amount);
        decimal tax = order.TaxAmount ?? 0m;
        return new(order.Id, order.CustomerId, order.OrderDate, order.ShipAddress, order.ShipCity, order.ShipCountry,
            order.ShipPostalCode, subtotal, order.TaxRate ?? 0m, tax, order.Total ?? subtotal + tax, lines,
            order.TaxAmount.HasValue, customer?.Name, customer?.Identification, customer?.Address, customer?.Email,
            customer?.Phone, order.TaxRate.HasValue);
    }

    private InvoiceVersion CreateVersion(int invoiceId, int number, DateTime modifiedAt, string modifiedBy, InvoiceDto invoice)
    {
        var versioned = invoice with { VersionNumber = number, ModifiedAt = number == 1 ? null : modifiedAt };
        return new() { InvoiceId = invoiceId, VersionNumber = number, ModifiedAt = modifiedAt,
            ModifiedBy = modifiedBy, SnapshotJson = JsonSerializer.Serialize(versioned, JsonOptions), PdfContent = pdfPresenter.Render(versioned) };
    }
    private static InvoiceVersionInfo ToInfo(InvoiceVersion v) => new(v.VersionNumber, v.ModifiedAt, v.ModifiedBy);
    private static InvoiceVersionDto ToDto(InvoiceVersion v) => new(ToInfo(v),
        JsonSerializer.Deserialize<InvoiceDto>(v.SnapshotJson, JsonOptions) ?? throw new InvalidDataException("La versión de factura está dañada."), v.PdfContent);
    private static string NormalizeUser(string userName) => string.IsNullOrWhiteSpace(userName) ? "Usuario autenticado" : userName.Trim()[..Math.Min(256, userName.Trim().Length)];
    private static void Validate(UpdateInvoiceDto update)
    {
        if (update is null || !TextInputRules.IsAddress(update.ShipAddress) || update.ShipAddress.Length > 60 ||
            string.IsNullOrWhiteSpace(update.ShipCity) || update.ShipCity.Length is < 3 or > 15 || !IsPlaceName(update.ShipCity) ||
            string.IsNullOrWhiteSpace(update.ShipCountry) || update.ShipCountry.Length is < 3 or > 15 || !IsPlaceName(update.ShipCountry) ||
            update.ShipPostalCode?.Length > 10 || (update.ShipPostalCode is not null && update.ShipPostalCode.Any(c => c is < '0' or > '9')))
            throw new InvoiceInputException("Revise los datos de entrega: hay campos vacíos, caracteres no permitidos o valores demasiado largos.");
        if (update.Lines is null || update.Lines.Count == 0)
            throw new InvoiceInputException("La factura debe tener al menos un producto.");
        if (update.Lines.Any(x => x is null))
            throw new InvoiceInputException("La lista de productos contiene una línea inválida.");
        if (update.Lines.Any(x => x.ProductId <= 0 || x.Quantity <= 0) || update.Lines.Select(x => x.ProductId).Distinct().Count() != update.Lines.Count)
            throw new InvoiceInputException("Los productos deben ser únicos y sus cantidades mayores que cero.");
    }

    private static bool IsPlaceName(string value) => TextInputRules.IsPlace(value);

    private static async Task SaveAsync(NorthWindSalesContext db, CancellationToken cancellationToken)
    {
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException ex)
        { throw new InvoiceConflictException("El inventario o la factura fueron modificados simultáneamente. No se guardaron cambios; vuelva a cargar la factura.", ex); }
    }

    private static bool IsSqlDeadlock(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current.GetType().FullName == "Microsoft.Data.SqlClient.SqlException" &&
                current.GetType().GetProperty("Number")?.GetValue(current) is int number && number == 1205) return true;
        }
        return false;
    }
}
