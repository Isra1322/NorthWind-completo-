namespace NorthWind.Membership.Frontend.RazorViews.Resources;
internal static class CommonMessages {
private static readonly System.Resources.ResourceManager Manager = new("NorthWind.Membership.Frontend.RazorViews.Resources.CommonMessages", typeof(CommonMessages).Assembly);
public static string CreateAccountText => Manager.GetString(nameof(CreateAccountText))!;
public static string LoginText => Manager.GetString(nameof(LoginText))!;
public static string CloseSessionText => Manager.GetString(nameof(CloseSessionText))!;
 public static string EmailLabel => UserRegistrationMessages.EmailLabel; public static string PasswordLabel => UserRegistrationMessages.PasswordLabel; public static string OK => UserRegistrationMessages.AcceptButton; }
