namespace NorthWind.Validation.Entities.Abstractions;

public abstract class AbstractModelValidator<T>(
    IValidationService<T> validationService,
    ValidationConstraint constraint = ValidationConstraint.AlwaysValidate) : IModelValidator<T>, IFieldModelValidator<T>
{
    public ValidationConstraint Constraint => constraint;
    public IEnumerable<ValidationError> Errors { get; private set; } = [];

    public async Task<bool> Validate(T model)
    {
        Errors = [];
        Errors = (await validationService.Validate(model)).ToArray();
        return !Errors.Any();
    }

    public Task<IEnumerable<ValidationError>> ValidateField(T model, string propertyPath) =>
        validationService.ValidateField(model, propertyPath);

    protected IValidationRules<T, TProperty> AddRuleFor<TProperty>(
        Expression<Func<T, TProperty>> expression) =>
        validationService.AddRuleFor(expression);

    protected ICollectionValidationRules<T, TProperty> AddRuleForEach<TProperty>(
        Expression<Func<T, IEnumerable<TProperty>>> expression) =>
        validationService.AddRuleForEach(expression);
}
