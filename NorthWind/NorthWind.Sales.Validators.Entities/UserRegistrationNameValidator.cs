using NorthWind.Membership.Entities.Dtos.UserRegistration;

namespace NorthWind.Sales.Validators.Entities;

internal class UserRegistrationNameValidator : NorthWind.Validation.Entities.Abstractions.AbstractModelValidator<UserRegistrationDto>
{
    public UserRegistrationNameValidator(IValidationService<UserRegistrationDto> service) : base(service)
    {
        AddRuleFor(user => user.FirstName).Must(IsName, "El nombre debe contener letras; se permiten espacios, guiones, puntos y apóstrofes.");
        AddRuleFor(user => user.LastName).Must(IsName, "El apellido debe contener letras; se permiten espacios, guiones, puntos y apóstrofes.");
    }

    private static bool IsName(string value)
    {
        // Required and length errors remain owned by the packaged validator.
        if (string.IsNullOrWhiteSpace(value)) return true;
        if (!NorthWind.Sales.Entities.Dtos.TextInputRules.IsName(value)) return false;
        string name = value.Trim(' ');
        if (!NorthWind.Sales.Entities.Dtos.TextInputRules.IsLetter(name[0]) ||
            name[^1] is '-' or '\'' or '’') return false;
        for (int index = 1; index < name.Length; index++)
            if (!NorthWind.Sales.Entities.Dtos.TextInputRules.IsLetter(name[index]) &&
                !NorthWind.Sales.Entities.Dtos.TextInputRules.IsLetter(name[index - 1]) &&
                !(name[index] == ' ' && name[index - 1] == '.')) return false;
        return true;
    }
}
