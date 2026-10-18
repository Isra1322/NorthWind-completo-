using System.Collections;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Forms;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

namespace NorthWind.RazorComponents.Validators;

public class ModelValidator<T> : ComponentBase, IDisposable
{
    [CascadingParameter] public EditContext CurrentEditContext { get; set; } = null!;
    [Parameter, EditorRequired] public IModelValidatorHub<T> Validator { get; set; } = null!;
    [Parameter] public EventCallback<EditContext> OnValidSubmit { get; set; }
    [Parameter] public EventCallback<EditContext> OnInvalidSubmit { get; set; }
    [Parameter] public IReadOnlyDictionary<string, string[]> FieldDependencies { get; set; } = new Dictionary<string, string[]>();
    public bool IsSubmitting { get; private set; }

    private EditContext? editContext;
    private ValidationMessageStore? messages;
    private readonly SemaphoreSlim validationLock = new(1, 1);
    private readonly HashSet<FieldIdentifier> touchedFields = [];
    private readonly HashSet<FieldIdentifier> pendingFields = [];
    private readonly Dictionary<FieldIdentifier, int> fieldVersions = [];
    private int contextVersion;
    private bool submitted;
    private bool disposed;

    public void AddErrors(IEnumerable<ValidationError> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        if (disposed) return;
        if (editContext is null || messages is null)
            throw new InvalidOperationException("El componente de validación no está asociado a un EditContext.");
        contextVersion++;
        submitted = true;
        messages.Clear();
        foreach (var error in errors.DistinctBy(e => (e.PropertyName, e.Message)))
            messages.Add(ResolveField(editContext.Model, error.PropertyName), error.Message);
        editContext.NotifyValidationStateChanged();
    }

    protected override void OnParametersSet()
    {
        if (CurrentEditContext is null)
            throw new InvalidOperationException("ModelValidator debe estar dentro de un EditForm.");
        if (ReferenceEquals(editContext, CurrentEditContext)) return;
        contextVersion++;
        Unsubscribe();
        editContext = CurrentEditContext;
        messages = new ValidationMessageStore(editContext);
        touchedFields.Clear();
        pendingFields.Clear();
        fieldVersions.Clear();
        submitted = false;
        editContext.SetFieldCssClassProvider(new ProgressiveCssProvider(this));
        editContext.OnValidationRequested += ValidationRequested;
        editContext.OnFieldChanged += FieldChanged;
    }

    private void ValidationRequested(object? sender, ValidationRequestedEventArgs args)
    {
        // Support synchronous EditContext.Validate callers without bypassing asynchronous validation.
        messages!.Add(new FieldIdentifier(editContext!.Model, "__ValidationPending"), "Validando...");
        editContext.NotifyValidationStateChanged();
        _ = ValidateFormAsync();
    }

    private void FieldChanged(object? sender, FieldChangedEventArgs args)
    {
        pendingFields.Add(args.FieldIdentifier);
        if (Validator is IDeferredFieldValidation policy && policy.DeferUntilBlur(args.FieldIdentifier.FieldName))
        {
            // Editing clears stale errors; blur and submit still run the complete rule.
            fieldVersions[args.FieldIdentifier] = fieldVersions.GetValueOrDefault(args.FieldIdentifier) + 1;
            messages!.Clear(args.FieldIdentifier);
            editContext!.NotifyValidationStateChanged();
            return;
        }
        if (submitted || touchedFields.Contains(args.FieldIdentifier))
            _ = ValidateFieldAndDependentsAsync(args.FieldIdentifier, touch: false);
    }

    public Task ValidateFieldAsync<TField>(Expression<Func<TField>> field) =>
        ValidateFieldAndDependentsAsync(FieldIdentifier.Create(field), touch: true);

    public Task RefreshFieldAsync<TField>(Expression<Func<TField>> field) =>
        ValidateFieldAndDependentsAsync(FieldIdentifier.Create(field), touch: false);

    public async Task ValidateFormAsync()
    {
        if (disposed || IsSubmitting) return;
        IsSubmitting = true;
        submitted = true;
        int version = contextVersion;
        var context = editContext!;
        try
        {
            await Task.Yield();
            await validationLock.WaitAsync();
            bool valid;
            try
            {
                if (disposed || version != contextVersion) return;
                valid = await Validator.Validate((T)context.Model);
                var errors = Validator.Errors.ToArray();
                if (disposed || version != contextVersion) return;
                messages!.Clear();
                pendingFields.Clear();
                foreach (var error in errors.DistinctBy(e => (e.PropertyName, e.Message)))
                    messages.Add(ResolveField(context.Model, error.PropertyName), error.Message);
                valid &= !context.GetValidationMessages().Any();
                context.NotifyValidationStateChanged();
            }
            finally
            {
                validationLock.Release();
            }

            if (!disposed && version == contextVersion)
            {
                if (valid) await OnValidSubmit.InvokeAsync(context);
                else await OnInvalidSubmit.InvokeAsync(context);
            }
        }
        catch (Exception exception)
        {
            if (!disposed) await DispatchExceptionAsync(exception);
        }
        finally
        {
            IsSubmitting = false;
            if (!disposed && ReferenceEquals(context, editContext))
            {
                messages!.Clear(new FieldIdentifier(context.Model, "__ValidationPending"));
                context.NotifyValidationStateChanged();
            }
        }
    }

    private async Task ValidateFieldAndDependentsAsync(FieldIdentifier field, bool touch)
    {
        if (disposed) return;
        if (touch) touchedFields.Add(field);
        if (IsSubmitting || !submitted && !touchedFields.Contains(field)) return;
        await ValidateSingleFieldAsync(field);
        if (!disposed && ReferenceEquals(field.Model, editContext!.Model) && FieldDependencies.TryGetValue(field.FieldName, out var dependencies))
            foreach (string dependency in dependencies)
            {
                var dependentField = new FieldIdentifier(field.Model, dependency);
                if (submitted || touchedFields.Contains(dependentField)) await ValidateSingleFieldAsync(dependentField);
            }
    }

    private async Task ValidateSingleFieldAsync(FieldIdentifier field)
    {
        var context = editContext!;
        int version = contextVersion;
        int fieldVersion = fieldVersions.GetValueOrDefault(field) + 1;
        fieldVersions[field] = fieldVersion;
        try
        {
            await validationLock.WaitAsync();
            try
            {
                if (disposed || IsSubmitting || version != contextVersion || fieldVersions.GetValueOrDefault(field) != fieldVersion) return;
                string path = GetPropertyPath(context.Model, field);
                var errors = (await Validator.ValidateField((T)context.Model, path)).ToArray();
                if (disposed || IsSubmitting || version != contextVersion || fieldVersions.GetValueOrDefault(field) != fieldVersion) return;
                messages!.Clear(field);
                pendingFields.Remove(field);
                foreach (var error in errors.DistinctBy(e => (e.PropertyName, e.Message)))
                    messages.Add(field, error.Message);
                context.NotifyValidationStateChanged();
            }
            finally { validationLock.Release(); }
        }
        catch (Exception exception)
        {
            if (!disposed) await DispatchExceptionAsync(exception);
        }
    }

    private static string GetPropertyPath(object model, FieldIdentifier field)
    {
        if (ReferenceEquals(model, field.Model)) return field.FieldName;
        foreach (var property in model.GetType().GetProperties())
            if (property.GetValue(model) is IList items)
                for (int index = 0; index < items.Count; index++)
                    if (ReferenceEquals(items[index], field.Model)) return $"{property.Name}[{index}].{field.FieldName}";
        return field.FieldName;
    }

    private static FieldIdentifier ResolveField(object model, string propertyPath)
    {
        object current = model;
        string[] segments = propertyPath.Split('.');
        for (int position = 0; position < segments.Length; position++)
        {
            string segment = segments[position];
            int bracket = segment.IndexOf('[');
            if (bracket >= 0)
            {
                string property = segment[..bracket];
                var collection = current.GetType().GetProperty(property)?.GetValue(current);
                if (collection is not IList items ||
                    !int.TryParse(segment[(bracket + 1)..].TrimEnd(']'), out int index) ||
                    index < 0 || index >= items.Count || items[index] is not object item)
                    return new FieldIdentifier(model, propertyPath);
                current = item;
            }
            else if (position == segments.Length - 1)
            {
                return new FieldIdentifier(current, segment);
            }
            else
            {
                var next = current.GetType().GetProperty(segment)?.GetValue(current);
                if (next is null) return new FieldIdentifier(model, propertyPath);
                current = next;
            }
        }
        return new FieldIdentifier(model, propertyPath);
    }

    private void Unsubscribe()
    {
        if (editContext is null) return;
        editContext.OnValidationRequested -= ValidationRequested;
        editContext.OnFieldChanged -= FieldChanged;
        editContext.SetFieldCssClassProvider(new FieldCssClassProvider());
        messages?.Clear();
    }

    private sealed class ProgressiveCssProvider(ModelValidator<T> owner) : FieldCssClassProvider
    {
        public override string GetFieldCssClass(EditContext context, in FieldIdentifier field)
        {
            if (!context.GetValidationMessages(field).Any() &&
                (owner.pendingFields.Contains(field) || (!owner.submitted && !owner.touchedFields.Contains(field))))
                return context.IsModified(field) ? "modified" : "";
            return base.GetFieldCssClass(context, field);
        }
    }

    public void Dispose()
    {
        disposed = true;
        contextVersion++;
        touchedFields.Clear();
        fieldVersions.Clear();
        Unsubscribe();
    }
}
