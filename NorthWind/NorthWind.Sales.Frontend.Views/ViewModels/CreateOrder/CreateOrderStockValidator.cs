using NorthWind.Validation.Entities.Enums;

namespace NorthWind.Sales.Frontend.Views.ViewModels.CreateOrder;

internal class CreateOrderStockValidator : IModelValidator<CreateOrderViewModel>, IFieldModelValidator<CreateOrderViewModel>
{
    public ValidationConstraint Constraint => ValidationConstraint.AlwaysValidate;
    public IEnumerable<ValidationError> Errors { get; private set; } = [];
    private static IEnumerable<ValidationError> GetErrors(CreateOrderViewModel model) =>
        model.OrderDetails.Select((detail, index) => (detail, index))
            .Where(item => item.detail.Quantity > item.detail.UnitsInStock)
            .Select(item => new ValidationError($"OrderDetails[{item.index}].Quantity",
                "La cantidad debe ser mayor que cero y no superar el stock disponible."));
    public Task<bool> Validate(CreateOrderViewModel model)
    {
        Errors = GetErrors(model).ToArray();
        return Task.FromResult(!Errors.Any());
    }
    public Task<IEnumerable<ValidationError>> ValidateField(CreateOrderViewModel model, string propertyPath)
    {
        const string prefix = "OrderDetails[", suffix = "].Quantity";
        if (!propertyPath.StartsWith(prefix, StringComparison.Ordinal) || !propertyPath.EndsWith(suffix, StringComparison.Ordinal) ||
            !int.TryParse(propertyPath.AsSpan(prefix.Length, propertyPath.Length - prefix.Length - suffix.Length), out int index) ||
            index < 0 || index >= model.OrderDetails.Count)
            return Task.FromResult<IEnumerable<ValidationError>>([]);
        var detail = model.OrderDetails[index];
        return Task.FromResult<IEnumerable<ValidationError>>(detail.Quantity > detail.UnitsInStock
            ? [new ValidationError(propertyPath, "La cantidad debe ser mayor que cero y no superar el stock disponible.")]
            : []);
    }
}
