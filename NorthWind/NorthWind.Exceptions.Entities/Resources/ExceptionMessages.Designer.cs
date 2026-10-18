namespace NorthWind.Exceptions.Entities.Resources;

internal static class ExceptionMessages
{
    private static readonly System.Resources.ResourceManager ResourceManager =
        new("NorthWind.Exceptions.Entities.Resources.ExceptionMessages", typeof(ExceptionMessages).Assembly);
    public static string ValidationExceptionTitle => ResourceManager.GetString(nameof(ValidationExceptionTitle))!;
    public static string ValidationExceptionDetail => ResourceManager.GetString(nameof(ValidationExceptionDetail))!;
    public static string UpdateExceptionTitle => ResourceManager.GetString(nameof(UpdateExceptionTitle))!;
    public static string UpdateExceptionDetail => ResourceManager.GetString(nameof(UpdateExceptionDetail))!;
    public static string UnauthorizedAccessExceptionTitle => ResourceManager.GetString(nameof(UnauthorizedAccessExceptionTitle))!;
    public static string UnauthorizedAccessExceptionDetail => ResourceManager.GetString(nameof(UnauthorizedAccessExceptionDetail))!;
    public static string UnhandledExceptionTitle => ResourceManager.GetString(nameof(UnhandledExceptionTitle))!;
    public static string UnhandledExceptionDetail => ResourceManager.GetString(nameof(UnhandledExceptionDetail))!;
}