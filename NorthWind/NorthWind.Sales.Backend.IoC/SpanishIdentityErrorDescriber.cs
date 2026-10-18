using Microsoft.AspNetCore.Identity;

namespace NorthWind.Sales.Backend.IoC;

internal sealed class SpanishIdentityErrorDescriber : IdentityErrorDescriber
{
    private static IdentityError Error(string code, string description) => new() { Code = code, Description = description };
    public override IdentityError DefaultError() => Error(nameof(DefaultError), "No se pudo completar la operación. Intente nuevamente.");
    public override IdentityError ConcurrencyFailure() => Error(nameof(ConcurrencyFailure), "Los datos cambiaron. Actualice la página e intente nuevamente.");
    public override IdentityError DuplicateEmail(string email) => Error(nameof(DuplicateEmail), "Este correo electrónico ya está registrado.");
    public override IdentityError DuplicateUserName(string userName) => Error(nameof(DuplicateUserName), "Este correo electrónico ya está registrado.");
    public override IdentityError InvalidEmail(string? email) => Error(nameof(InvalidEmail), "Ingrese un correo electrónico válido.");
    public override IdentityError InvalidUserName(string? userName) => Error(nameof(InvalidUserName), "Ingrese un correo electrónico válido.");
    public override IdentityError PasswordTooShort(int length) => Error(nameof(PasswordTooShort), $"La contraseña debe tener al menos {length} caracteres.");
    public override IdentityError PasswordRequiresDigit() => Error(nameof(PasswordRequiresDigit), "La contraseña debe incluir al menos un número.");
    public override IdentityError PasswordRequiresLower() => Error(nameof(PasswordRequiresLower), "La contraseña debe incluir al menos una letra minúscula.");
    public override IdentityError PasswordRequiresUpper() => Error(nameof(PasswordRequiresUpper), "La contraseña debe incluir al menos una letra mayúscula.");
    public override IdentityError PasswordRequiresNonAlphanumeric() => Error(nameof(PasswordRequiresNonAlphanumeric), "La contraseña debe incluir al menos un símbolo.");
    public override IdentityError PasswordRequiresUniqueChars(int uniqueChars) => Error(nameof(PasswordRequiresUniqueChars), $"La contraseña debe incluir al menos {uniqueChars} caracteres diferentes.");
    public override IdentityError PasswordMismatch() => Error(nameof(PasswordMismatch), "La contraseña es incorrecta.");
    public override IdentityError InvalidToken() => Error(nameof(InvalidToken), "La solicitud expiró o no es válida. Intente nuevamente.");
    public override IdentityError UserAlreadyHasPassword() => Error(nameof(UserAlreadyHasPassword), "La cuenta ya tiene una contraseña.");
    public override IdentityError UserLockoutNotEnabled() => Error(nameof(UserLockoutNotEnabled), "No se puede bloquear esta cuenta.");
    public override IdentityError LoginAlreadyAssociated() => Error(nameof(LoginAlreadyAssociated), "Este acceso ya está asociado a una cuenta.");
    public override IdentityError InvalidRoleName(string? role) => Error(nameof(InvalidRoleName), "El nombre del rol no es válido.");
    public override IdentityError DuplicateRoleName(string role) => Error(nameof(DuplicateRoleName), "El rol ya existe.");
    public override IdentityError UserAlreadyInRole(string role) => Error(nameof(UserAlreadyInRole), "La cuenta ya pertenece a ese rol.");
    public override IdentityError UserNotInRole(string role) => Error(nameof(UserNotInRole), "La cuenta no pertenece a ese rol.");
    public override IdentityError RecoveryCodeRedemptionFailed() => Error(nameof(RecoveryCodeRedemptionFailed), "El código de recuperación no es válido.");
}
