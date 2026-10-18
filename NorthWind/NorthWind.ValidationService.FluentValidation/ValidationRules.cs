namespace NorthWind.ValidationService.FluentValidation;

internal class ValidationRules<T, TProperty>(IRuleBuilder<T, TProperty> rule) : IValidationRules<T, TProperty>
{
    public IValidationRules<T, TProperty> NotEmpty(string errorMessage)
    {
        rule.NotEmpty().WithMessage(errorMessage);
        return this;
    }

    public IValidationRules<T, TProperty> NotNull(string errorMessage)
    {
        rule.NotNull().WithMessage(errorMessage);
        return this;
    }

    public IValidationRules<T, TProperty> Must(Func<TProperty, bool> predicate, string errorMessage)
    {
        rule.Must(predicate).WithMessage(errorMessage);
        return this;
    }

    public IValidationRules<T, TProperty> StopOnFirstFailure()
    {
        ((IRuleBuilderInitial<T, TProperty>)rule).Cascade(CascadeMode.Stop);
        return this;
    }

    public IValidationRules<T, TProperty> GreaterThan<TValue>(TValue valueToCompare, string errorMessage)
        where TValue : IComparable<TValue>, IComparable
    {
        if (rule is not IRuleBuilder<T, TValue> comparableRule)
            throw new ArgumentException("El tipo de comparación debe coincidir con el tipo de la propiedad.");
        comparableRule.GreaterThan(valueToCompare).WithMessage(errorMessage);
        return this;
    }

    public IValidationRules<T, TProperty> Equal(Expression<Func<T, TProperty>> expression, string errorMessage)
    {
        rule.Equal(expression).WithMessage(errorMessage);
        return this;
    }

    private IRuleBuilder<T, string> StringRule => rule as IRuleBuilder<T, string>
        ?? throw new InvalidOperationException("Esta regla requiere una propiedad string.");

    public IValidationRules<T, string> Length(int length, string errorMessage)
    {
        StringRule.Length(length).WithMessage(errorMessage);
        return new ValidationRules<T, string>(StringRule);
    }

    public IValidationRules<T, string> MaximumLength(int length, string errorMessage)
    {
        StringRule.MaximumLength(length).WithMessage(errorMessage);
        return new ValidationRules<T, string>(StringRule);
    }

    public IValidationRules<T, string> MinimumLength(int length, string errorMessage)
    {
        StringRule.MinimumLength(length).WithMessage(errorMessage);
        return new ValidationRules<T, string>(StringRule);
    }

    public IValidationRules<T, string> EmailAddress(string errorMessage)
    {
        StringRule.EmailAddress().WithMessage(errorMessage);
        return new ValidationRules<T, string>(StringRule);
    }
}