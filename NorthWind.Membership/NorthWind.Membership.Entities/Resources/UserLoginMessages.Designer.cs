namespace NorthWind.Membership.Entities.Resources;
internal static class UserLoginMessages { private static readonly System.Resources.ResourceManager Manager = new("NorthWind.Membership.Entities.Resources.UserLoginMessages", typeof(UserLoginMessages).Assembly);
public static string RequiredEmailErrorMessage => Manager.GetString(nameof(RequiredEmailErrorMessage))!;
public static string InvalidEmailErrorMessage => Manager.GetString(nameof(InvalidEmailErrorMessage))!;
public static string RequiredPasswordErrorMessage => Manager.GetString(nameof(RequiredPasswordErrorMessage))!;
}