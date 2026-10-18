using System.Text.RegularExpressions;
using NorthWind.Membership.Entities.Dtos.UserRegistration;
using NorthWind.Validation.Entities;
using NorthWind.Validation.Entities.Interfaces;
using NorthWind.Validation.Entities.ValueObjects;

namespace NorthWind.Sales.Validators.Entities;

// Replace only the packaged registration email rule; retain every other rule.
public sealed class RegistrationEmailValidatorHub(IEnumerable<IModelValidator<UserRegistrationDto>> validators)
    : IModelValidatorHub<UserRegistrationDto>
{
    private readonly ModelValidatorHub<UserRegistrationDto> inner = new(validators);
    public IEnumerable<ValidationError> Errors { get; private set; } = [];

    public async Task<bool> Validate(UserRegistrationDto model)
    {
        await inner.Validate(model);
        Errors = ReplaceEmailErrors(inner.Errors, model.Email).ToArray();
        return !Errors.Any();
    }

    public async Task<IEnumerable<ValidationError>> ValidateField(UserRegistrationDto model, string propertyPath) =>
        propertyPath == nameof(model.Email)
            ? EmailErrors(model.Email).ToArray()
            : await inner.ValidateField(model, propertyPath);

    private static IEnumerable<ValidationError> ReplaceEmailErrors(IEnumerable<ValidationError> errors, string? email) =>
        errors.Where(error => error.PropertyName != nameof(UserRegistrationDto.Email)).Concat(EmailErrors(email));

    private static IEnumerable<ValidationError> EmailErrors(string? email)
    {
        if (string.IsNullOrWhiteSpace(email))
            yield return new(nameof(UserRegistrationDto.Email), "Ingrese su correo electrónico.");
        else if (email.Length > RegistrationLimits.Email || !Regex.IsMatch(email,
            @"\A[A-Za-z0-9_+\-]+(?:\.[A-Za-z0-9_+\-]+)*@[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?(?:\.[A-Za-z0-9](?:[A-Za-z0-9\-]*[A-Za-z0-9])?)+\z",
            RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)))
            yield return new(nameof(UserRegistrationDto.Email), "Ingrese un correo electrónico válido.");
    }
}
