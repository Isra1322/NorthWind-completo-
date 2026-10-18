namespace NorthWind.Validation.Entities;

public class ModelValidatorHub<T>(IEnumerable<IModelValidator<T>> validators) : IModelValidatorHub<T>
{
    private readonly List<ValidationError> errors = [];
    public IEnumerable<ValidationError> Errors => errors;

    public async Task<IEnumerable<ValidationError>> ValidateField(T model, string propertyPath)
    {
        var fieldErrors = new List<ValidationError>();
        foreach (var validator in validators.OfType<IFieldModelValidator<T>>())
            fieldErrors.AddRange(await validator.ValidateField(model, propertyPath));
        return fieldErrors.Where(error => error.PropertyName == propertyPath)
            .DistinctBy(error => (error.PropertyName, error.Message)).ToArray();
    }

    public async Task<bool> Validate(T model)
    {
        errors.Clear();
        foreach (var validator in validators.Where(v => v.Constraint == ValidationConstraint.AlwaysValidate))
        {
            await validator.Validate(model);
            errors.AddRange(validator.Errors);
        }

        // Todos los validadores de la segunda fase reciben un modelo sin errores básicos.
        if (errors.Count == 0)
        {
            foreach (var validator in validators.Where(v =>
                v.Constraint == ValidationConstraint.ValidateIfThereAreNoPreviousErrors))
            {
                await validator.Validate(model);
                errors.AddRange(validator.Errors);
            }
        }
        return errors.Count == 0;
    }
}
