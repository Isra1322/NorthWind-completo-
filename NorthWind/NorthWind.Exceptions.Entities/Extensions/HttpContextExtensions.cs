namespace NorthWind.Exceptions.Entities.Extensions;

internal static class HttpContextExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async ValueTask WriteProblemDetailsAsync(
        this HttpContext context, ProblemDetails details)
    {
        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = details.Status ?? StatusCodes.Status500InternalServerError;
        await JsonSerializer.SerializeAsync(context.Response.Body, details,
            JsonOptions, context.RequestAborted);
    }
}