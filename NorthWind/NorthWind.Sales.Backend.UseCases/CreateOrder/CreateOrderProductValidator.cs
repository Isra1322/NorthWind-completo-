using NorthWind.Sales.Backend.UseCases.Resources;
using NorthWind.Validation.Entities.Enums;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

namespace NorthWind.Sales.Backend.UseCases.CreateOrder;

internal class CreateOrderProductValidator(IQueriesRepository repository) : IModelValidator<CreateOrderDto>
{
    public ValidationConstraint Constraint => ValidationConstraint.ValidateIfThereAreNoPreviousErrors;
    public IEnumerable<ValidationError> Errors { get; private set; } = [];

    public async Task<bool> Validate(CreateOrderDto model)
    {
        Errors = [];
        var requested = model.OrderDetails.Select((detail, index) => new { detail, index })
            .GroupBy(item => item.detail.ProductId)
            .Select(group => new
            {
                ProductId = group.Key,
                Quantity = group.Sum(item => (int)item.detail.Quantity),
                Index = group.First().index
            }).ToArray();
        var products = (await repository.GetProductsUnitsInStock(requested.Select(p => p.ProductId)))
            .ToDictionary(p => p.ProductId);
        var errors = new List<ValidationError>();

        foreach (var item in requested)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                errors.Add(new($"OrderDetails[{item.Index}].ProductId",
                    string.Format(CreateOrderMessages.ProductIdNotFoundErrorTemplate, item.ProductId)));
            else if (product.UnitsInStock < item.Quantity)
                errors.Add(new NorthWind.Sales.Backend.BusinessObjects.ValueObjects.InventoryConflictError(
                    $"OrderDetails[{item.Index}].Quantity", item.ProductId, product.UnitsInStock, item.Quantity));
        }
        Errors = errors;
        return errors.Count == 0;
    }
}
