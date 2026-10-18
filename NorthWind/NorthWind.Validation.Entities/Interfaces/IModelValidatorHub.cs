namespace NorthWind.Validation.Entities.Interfaces;

public interface IModelValidatorHub<T>
{
    Task<bool> Validate(T model);
    IEnumerable<ValidationError> Errors { get; }
    async Task<IEnumerable<ValidationError>> ValidateField(T model, string propertyPath)
    {
        await Validate(model);
        return Errors.Where(error => error.PropertyName == propertyPath).ToArray();
    }
}
