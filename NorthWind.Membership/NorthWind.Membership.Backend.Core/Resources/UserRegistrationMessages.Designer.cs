namespace NorthWind.Membership.Backend.Core.Resources;
internal static class UserRegistrationMessages { private static readonly System.Resources.ResourceManager Manager = new("NorthWind.Membership.Backend.Core.Resources.UserRegistrationMessages", typeof(UserRegistrationMessages).Assembly);
public static string UserRegistrationErrorTitle => Manager.GetString(nameof(UserRegistrationErrorTitle))!;
public static string UserRegistrationErrorDetail => Manager.GetString(nameof(UserRegistrationErrorDetail))!;
}