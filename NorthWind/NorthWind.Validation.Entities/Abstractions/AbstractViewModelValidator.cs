namespace NorthWind.Validation.Entities.Abstractions;

public abstract class AbstractViewModelValidator<TModel, TViewModel>(
    IModelValidatorHub<TModel> modelValidatorHub,
    ValidationConstraint constraint = ValidationConstraint.AlwaysValidate) : IModelValidator<TViewModel>, IFieldModelValidator<TViewModel>
{
    public ValidationConstraint Constraint => constraint;
    public IEnumerable<ValidationError> Errors { get; private set; } = [];

    protected abstract TModel ToModel(TViewModel viewModel);

    public Task<IEnumerable<ValidationError>> ValidateField(TViewModel model, string propertyPath) =>
        modelValidatorHub.ValidateField(ToModel(model), propertyPath);

    public async Task<bool> Validate(TViewModel model)
    {
        Errors = [];
        bool valid = await modelValidatorHub.Validate(ToModel(model));
        Errors = modelValidatorHub.Errors.ToArray();
        return valid;
    }
}
