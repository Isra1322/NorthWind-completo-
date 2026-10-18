using NorthWind.Exceptions.Entities.Exceptions;
using NorthWind.Validation.Entities.ValueObjects;
using NorthWind.Sales.Backend.BusinessObjects.Aggregates;
using NorthWind.Sales.Backend.BusinessObjects.ValueObjects;

namespace NorthWind.Sales.Backend.DataContexts.EFCore.Services;

internal class NorthWindSalesCommandsDataContext(IOptions<DBOptions> dbOptions, IOptions<SalesTaxOptions> taxOptions)
    : NorthWindSalesContext(dbOptions), INorthWindSalesCommandsDataContext
{
    public async Task<int?> FindOrderId(Guid requestId, bool lockRequest = false)
    {
        if (lockRequest)
        {
            // Serialize retries across API instances until the ambient transaction commits.
            await Database.OpenConnectionAsync();
            string resource = $"CreateOrder:{requestId:D}";
            await Database.ExecuteSqlInterpolatedAsync($"""
                DECLARE @result int;
                EXEC @result = sys.sp_getapplock @Resource={resource}, @LockMode='Exclusive',
                    @LockOwner='Transaction', @LockTimeout=30000;
                IF @result < 0 THROW 51000, 'No se pudo comprobar la solicitud de orden.', 1;
                """);
        }
        return await Orders.AsNoTracking().Where(o => o.RequestId == requestId)
            .Select(o => (int?)o.Id).SingleOrDefaultAsync();
    }

    public async Task AddOrderAsync(Order order) =>
        await Orders.AddAsync(order);

    public Task AddOrderDetailsAsync(IEnumerable<Repositories.Entities.OrderDetail> orderDetails)
    {
        var details = orderDetails.ToArray();
        foreach (var detail in details)
            detail.ProductName = Products.Local.Single(p => p.Id == detail.ProductId).Name;
        return OrderDetails.AddRangeAsync(details);
    }

    public async Task PrepareSaleAsync(OrderAggregate order)
    {
        var ids = order.OrderDetails.Select(d => d.ProductId).ToArray();
        var products = await Products.Where(p => ids.Contains(p.Id)).ToDictionaryAsync(p => p.Id);
        var errors = new List<ValidationError>();
        int index = 0;
        foreach (var detail in order.OrderDetails)
        {
            if (!products.TryGetValue(detail.ProductId, out var product))
                errors.Add(new($"OrderDetails[{index}].ProductId", "El producto ya no está disponible."));
            else if (detail.Quantity <= 0)
                errors.Add(new($"OrderDetails[{index}].Quantity", "La cantidad debe ser mayor que cero."));
            else if (product.UnitsInStock < detail.Quantity)
                errors.Add(new InventoryConflictError($"OrderDetails[{index}].Quantity",
                    detail.ProductId, product.UnitsInStock, detail.Quantity));
            else if (product.UnitPrice <= 0 || product.UnitPrice != detail.UnitPrice)
                errors.Add(new($"OrderDetails[{index}].UnitPrice", "El precio cambió o no está configurado. Seleccione nuevamente el producto."));
            index++;
        }
        if (errors.Any(error => error is InventoryConflictError)) throw new InventoryConflictException(errors);
        if (errors.Count > 0) throw new ValidationException(errors);
        order.CalculateTotals(taxOptions.Value.EffectiveRate);
        foreach (var detail in order.OrderDetails)
            products[detail.ProductId].UnitsInStock -= detail.Quantity;
    }

    public async Task SaveChangesAsync()
    {
        try { await GuardDBContext.AgainstSaveChangesErrorAsync(this); }
        catch (UpdateException exception) when (exception.InnerException is DbUpdateConcurrencyException)
        {
            throw new InventoryConflictException([new ValidationError("OrderDetails",
                "El inventario fue actualizado por otra venta. La orden no se guardó. Vuelva a enviar para comprobar nuevamente el stock.")]);
        }
    }
}
