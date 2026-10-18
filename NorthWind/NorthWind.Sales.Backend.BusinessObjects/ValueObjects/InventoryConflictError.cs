namespace NorthWind.Sales.Backend.BusinessObjects.ValueObjects;

public sealed class InventoryConflictError(string propertyName, int productId, int available, int requested)
    : NorthWind.Validation.Entities.ValueObjects.ValidationError(propertyName,
        $"No se pudo guardar la orden porque el inventario del producto {productId} cambió. Stock disponible: {available}. Cantidad solicitada: {requested}.");
