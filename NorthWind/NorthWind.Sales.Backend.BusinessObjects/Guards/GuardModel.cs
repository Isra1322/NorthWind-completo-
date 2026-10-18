using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Exceptions.Entities.Exceptions;

namespace NorthWind.Sales.Backend.BusinessObjects.Guards;

public static class GuardModel
{
    public static async Task AgainstNotValid<T>(IModelValidatorHub<T> modelValidatorHub, T model)
    {
        if (!await modelValidatorHub.Validate(model))
        {
            var errors = modelValidatorHub.Errors.ToArray();
            if (errors.Any(error => error is ValueObjects.InventoryConflictError))
                throw new InventoryConflictException(errors);
            throw new ValidationException(errors);
        }
    }
}
