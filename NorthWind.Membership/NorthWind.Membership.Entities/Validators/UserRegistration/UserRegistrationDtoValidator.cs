namespace NorthWind.Membership.Entities.Validators.UserRegistration;

internal class UserRegistrationDtoValidator : AbstractModelValidator<UserRegistrationDto>
{
    public UserRegistrationDtoValidator(IValidationService<UserRegistrationDto> service) : base(service)
    {
        AddRuleFor(c => c.FirstName).StopOnFirstFailure().NotEmpty(UserRegistrationMessages.RequiredFirstNameErrorMessage).MaximumLength(RegistrationLimits.FirstName, RegistrationLimits.FirstNameMessage);
        AddRuleFor(c => c.LastName).StopOnFirstFailure().NotEmpty(UserRegistrationMessages.RequiredLastNameErrorMessage).MaximumLength(RegistrationLimits.LastName, RegistrationLimits.LastNameMessage);
        AddRuleFor(c => c.Email).StopOnFirstFailure()
            .NotEmpty(UserRegistrationMessages.RequiredEmailErrorMessage)
            .MaximumLength(RegistrationLimits.Email, RegistrationLimits.EmailMessage).EmailAddress(UserRegistrationMessages.InvalidEmailErrorMessage);
        AddRuleFor(c => c.Password).NotEmpty(UserRegistrationMessages.RequiredPasswordErrorMessage);
        AddRuleFor(c => c.Password)
            .Must(p => string.IsNullOrWhiteSpace(p) || p.Length >= 6, UserRegistrationMessages.PasswordMinimumLengthErrorMessage)
            .Must(p => string.IsNullOrWhiteSpace(p) || p.Any(char.IsLower), UserRegistrationMessages.PasswordLowercaseErrorMessage)
            .Must(p => string.IsNullOrWhiteSpace(p) || p.Any(char.IsUpper), UserRegistrationMessages.PasswordUppercaseErrorMessage)
            .Must(p => string.IsNullOrWhiteSpace(p) || p.Any(char.IsDigit), UserRegistrationMessages.PasswordDigitErrorMessage)
            .Must(p => string.IsNullOrWhiteSpace(p) || p.Any(c => !char.IsLetterOrDigit(c)), UserRegistrationMessages.PasswordSymbolErrorMessage);
    }
}
