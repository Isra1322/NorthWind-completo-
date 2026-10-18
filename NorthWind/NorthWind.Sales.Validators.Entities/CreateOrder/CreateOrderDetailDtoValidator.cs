namespace NorthWind.Sales.Validators.Entities.CreateOrder;

internal class CreateOrderDetailDtoValidator : AbstractModelValidator<CreateOrderDetailDto>
{
    public CreateOrderDetailDtoValidator(IValidationService<CreateOrderDetailDto> validationService)
        : base(validationService)
    {
        AddRuleFor(d => d.ProductId)
            .GreaterThan(0, CreateOrderMessages.ProductIdGreaterThanZero);
        AddRuleFor<int>(d => d.Quantity)
            .GreaterThan(0, CreateOrderMessages.QuantityGreaterThanZero);
        AddRuleFor(d => d.UnitPrice)
            .GreaterThan<decimal>(0, CreateOrderMessages.UnitPriceGreaterThanZero);
    }
}