namespace NorthWind.ValidationService.FluentValidation;

internal class CollectionValidationRules<T, TProperty>(
    IRuleBuilderInitialCollection<T, TProperty> rule) : ICollectionValidationRules<T, TProperty>
{
    public ICollectionValidationRules<T, TProperty> SetValidator(IModelValidator<TProperty> validator)
    {
        rule.CustomAsync(async (item, context, cancellationToken) =>
        {
            IEnumerable<ValidationError> errors;
            if (context.RootContextData.TryGetValue(FluentValidationService<T>.SelectedPropertyKey, out var selected) && selected is string propertyPath)
            {
                string prefix = context.PropertyPath + ".";
                if (!propertyPath.StartsWith(prefix, StringComparison.Ordinal)) return;
                string childProperty = propertyPath[prefix.Length..];
                if (validator is IFieldModelValidator<TProperty> fieldValidator)
                    errors = await fieldValidator.ValidateField(item, childProperty);
                else
                {
                    await validator.Validate(item);
                    errors = validator.Errors.Where(error => error.PropertyName == childProperty);
                }
            }
            else
            {
                await validator.Validate(item);
                errors = validator.Errors;
            }
            foreach (var error in errors)
            {
                var path = string.IsNullOrEmpty(error.PropertyName) ? context.PropertyPath
                    : $"{context.PropertyPath}.{error.PropertyName}";
                context.AddFailure(new global::FluentValidation.Results.ValidationFailure(path, error.Message));
            }
        });
        return this;
    }
}
