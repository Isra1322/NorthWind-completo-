namespace NorthWind.Membership.Backend.Core.Resources;
internal static class UserLoginMessages { private static readonly System.Resources.ResourceManager Manager = new("NorthWind.Membership.Backend.Core.Resources.UserLoginMessages", typeof(UserLoginMessages).Assembly);
public static string InvalidUserCredentialsErrorMessage => Manager.GetString(nameof(InvalidUserCredentialsErrorMessage))!;
public static string UserLoginErrorTitle => Manager.GetString(nameof(UserLoginErrorTitle))!;
public static string UserLoginErrorDetail => Manager.GetString(nameof(UserLoginErrorDetail))!;
}