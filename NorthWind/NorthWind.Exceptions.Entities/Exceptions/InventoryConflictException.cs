namespace NorthWind.Exceptions.Entities.Exceptions;

public sealed class InventoryConflictException(IEnumerable<NorthWind.Validation.Entities.ValueObjects.ValidationError> errors)
    : ValidationException(errors);
