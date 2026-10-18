namespace NorthWind.Validation.Entities.Interfaces;

public interface IDeferredFieldValidation
{
    bool DeferUntilBlur(string propertyName);
}
