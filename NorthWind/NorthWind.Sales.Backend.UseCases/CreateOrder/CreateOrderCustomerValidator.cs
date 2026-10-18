using NorthWind.Sales.Backend.UseCases.Resources;
using NorthWind.Validation.Entities.Enums;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

namespace NorthWind.Sales.Backend.UseCases.CreateOrder;

internal class CreateOrderCustomerValidator(IQueriesRepository repository) : IModelValidator<CreateOrderDto>
{
    public ValidationConstraint Constraint => ValidationConstraint.ValidateIfThereAreNoPreviousErrors;
    public IEnumerable<ValidationError> Errors { get; private set; } = [];

    public async Task<bool> Validate(CreateOrderDto model)
    {
        Errors = [];
        decimal? balance = await repository.GetCustomerCurrentBalance(model.CustomerId);
        if (balance is null)
            Errors = [new(nameof(model.CustomerId), CreateOrderMessages.CustomerIdNotFoundError)];
        else if (balance > 0)
            Errors = [new(nameof(model.CustomerId),
                string.Format(CreateOrderMessages.CustomerWithBalanceErrorTemplate, model.CustomerId, balance))];
        return !Errors.Any();
    }
}