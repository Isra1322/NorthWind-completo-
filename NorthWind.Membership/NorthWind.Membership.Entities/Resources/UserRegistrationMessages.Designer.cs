namespace NorthWind.Membership.Entities.Resources;
internal static class UserRegistrationMessages { private static readonly System.Resources.ResourceManager Manager = new("NorthWind.Membership.Entities.Resources.UserRegistrationMessages", typeof(UserRegistrationMessages).Assembly);
public static string RequiredFirstNameErrorMessage => Manager.GetString(nameof(RequiredFirstNameErrorMessage))!;
public static string RequiredLastNameErrorMessage => Manager.GetString(nameof(RequiredLastNameErrorMessage))!;
public static string RequiredEmailErrorMessage => Manager.GetString(nameof(RequiredEmailErrorMessage))!;
public static string InvalidEmailErrorMessage => Manager.GetString(nameof(InvalidEmailErrorMessage))!;
public static string RequiredPasswordErrorMessage => Manager.GetString(nameof(RequiredPasswordErrorMessage))!;
public static string PasswordMinimumLengthErrorMessage => Manager.GetString(nameof(PasswordMinimumLengthErrorMessage))!;
public static string PasswordLowercaseErrorMessage => Manager.GetString(nameof(PasswordLowercaseErrorMessage))!;
public static string PasswordUppercaseErrorMessage => Manager.GetString(nameof(PasswordUppercaseErrorMessage))!;
public static string PasswordDigitErrorMessage => Manager.GetString(nameof(PasswordDigitErrorMessage))!;
public static string PasswordSymbolErrorMessage => Manager.GetString(nameof(PasswordSymbolErrorMessage))!;
}