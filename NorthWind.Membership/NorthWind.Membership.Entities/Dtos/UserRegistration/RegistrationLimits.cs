namespace NorthWind.Membership.Entities.Dtos.UserRegistration;

public static class RegistrationLimits
{
    public const int FirstName = 25;
    public const int LastName = 25;
    public const int Email = 50;
    public const string FirstNameMessage = "El nombre no puede superar los 25 caracteres.";
    public const string LastNameMessage = "Los apellidos no pueden superar los 25 caracteres.";
    public const string EmailMessage = "El correo electrónico no puede superar los 50 caracteres.";
}
