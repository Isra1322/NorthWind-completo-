namespace NorthWind.Sales.Validators.Entities.CreateOrder;

internal class CreateOrderDtoValidator : AbstractModelValidator<CreateOrderDto>
{
    public CreateOrderDtoValidator(IValidationService<CreateOrderDto> validationService,
        IModelValidator<CreateOrderDetailDto> detailValidator) : base(validationService)
    {
        AddRuleFor(o => o.CustomerId).StopOnFirstFailure()
            .NotEmpty(CreateOrderMessages.CustomerIdRequired)
            .Length(5, CreateOrderMessages.CustomerIdRequiredLength);
        AddRuleFor(o => o.ShipAddress).StopOnFirstFailure()
            .NotEmpty(CreateOrderMessages.ShipAddressRequired)
            .MaximumLength(60, CreateOrderMessages.ShipAddressMaximumLength)
            .Must(NorthWind.Sales.Entities.Dtos.TextInputRules.IsAddress, "Ingrese una dirección válida con letras o números y signos habituales de dirección.");
        AddRuleFor(o => o.ShipCity).StopOnFirstFailure()
            .NotEmpty(CreateOrderMessages.ShipCityRequired)
            .MinimumLength(3, CreateOrderMessages.ShipCityMinimumLength)
            .MaximumLength(15, CreateOrderMessages.ShipCityMaximumLength)
            .Must(IsPlaceName, "La ciudad debe contener letras, espacios, guiones o apóstrofes entre letras.");
        AddRuleFor(o => o.ShipCountry).StopOnFirstFailure()
            .NotEmpty(CreateOrderMessages.ShipCountryRequired)
            .MinimumLength(3, CreateOrderMessages.ShipCountryMinimumLength)
            .MaximumLength(15, CreateOrderMessages.ShipCountryMaximumLength)
            .Must(IsPlaceName, "El país debe contener letras, espacios, guiones o apóstrofes entre letras.");
        AddRuleFor(o => o.ShipPostalCode).StopOnFirstFailure()
            .MaximumLength(10, CreateOrderMessages.ShipPostalCodeMaximumLength)
            .Must(value => string.IsNullOrEmpty(value) || value.All(c => c is >= '0' and <= '9'),
                "El código postal solo puede contener dígitos del 0 al 9.");
        AddRuleFor(o => o.OrderDetails).StopOnFirstFailure()
            .NotNull(CreateOrderMessages.OrderDetailsRequired)
            .NotEmpty(CreateOrderMessages.OrderDetailsNotEmpty);
        AddRuleForEach(o => o.OrderDetails).SetValidator(detailValidator);
        AddRuleFor(o => o.OrderDetails).Must(details => details is not null && details.All(d => d is not null) &&
            details.Select(d => d.ProductId).Distinct().Count() == details.Count(),
            "Un producto solo puede aparecer una vez en la orden.");
        AddRuleFor(o => o.CustomerId).Must(value => string.IsNullOrWhiteSpace(value) || value.All(c =>
                c is >= 'A' and <= 'Z' or >= 'a' and <= 'z' or >= '0' and <= '9'),
            "El código del cliente debe contener únicamente letras y números.");
    }
    private static bool IsPlaceName(string value) => string.IsNullOrWhiteSpace(value) ||
        NorthWind.Sales.Entities.Dtos.TextInputRules.IsPlace(value);
}
