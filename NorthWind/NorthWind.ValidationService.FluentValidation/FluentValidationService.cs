namespace NorthWind.ValidationService.FluentValidation;

internal class FluentValidationService<T> : IValidationService<T>
{
    internal const string SelectedPropertyKey = "NorthWind.SelectedValidationProperty";
    private readonly AbstractValidatorImplementation<T> validator = new();

    public IValidationRules<T, TProperty> AddRuleFor<TProperty>(Expression<Func<T, TProperty>> expression) =>
        new ValidationRules<T, TProperty>(validator.RuleFor(expression));

    public ICollectionValidationRules<T, TProperty> AddRuleForEach<TProperty>(
        Expression<Func<T, IEnumerable<TProperty>>> expression) =>
        new CollectionValidationRules<T, TProperty>(validator.RuleForEach(expression));

    public async Task<IEnumerable<ValidationError>> Validate(T model)
    {
        var result = await validator.ValidateAsync(model);
        return result.Errors.Select(e => new ValidationError(e.PropertyName, e.ErrorMessage)).ToArray();
    }

    public async Task<IEnumerable<ValidationError>> ValidateField(T model, string propertyPath)
    {
        var context = ValidationContext<T>.CreateWithOptions(model, options => options.IncludeProperties(propertyPath));
        context.RootContextData[SelectedPropertyKey] = propertyPath;
        var result = await validator.ValidateAsync(context);
        return result.Errors.Where(error => error.PropertyName == propertyPath)
            .Select(error => new ValidationError(error.PropertyName, error.ErrorMessage)).ToArray();
    }
}
