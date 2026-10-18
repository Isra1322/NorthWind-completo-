namespace NorthWind.Validation.Entities.Interfaces;

public interface IFieldModelValidator<T>
{
    Task<IEnumerable<ValidationError>> ValidateField(T model, string propertyPath);
}
