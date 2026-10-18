namespace NorthWind.Membership.Frontend.RazorViews.Resources;
internal static class UserRegistrationMessages { private static readonly System.Resources.ResourceManager Manager = new("NorthWind.Membership.Frontend.RazorViews.Resources.UserRegistrationMessages", typeof(UserRegistrationMessages).Assembly);
public static string Title => Manager.GetString(nameof(Title))!;
public static string FirstNameLabel => Manager.GetString(nameof(FirstNameLabel))!;
public static string LastNameLabel => Manager.GetString(nameof(LastNameLabel))!;
public static string EmailLabel => Manager.GetString(nameof(EmailLabel))!;
public static string PasswordLabel => Manager.GetString(nameof(PasswordLabel))!;
public static string PasswordConfirmationLabel => Manager.GetString(nameof(PasswordConfirmationLabel))!;
public static string RegisterButton => Manager.GetString(nameof(RegisterButton))!;
public static string RegisteredMessage => Manager.GetString(nameof(RegisteredMessage))!;
public static string ConfirmPasswordRequiredErrorMessage => Manager.GetString(nameof(ConfirmPasswordRequiredErrorMessage))!;
public static string ConfirmPasswordErrorMessage => Manager.GetString(nameof(ConfirmPasswordErrorMessage))!;
public static string AcceptButton => Manager.GetString(nameof(AcceptButton))!;
}