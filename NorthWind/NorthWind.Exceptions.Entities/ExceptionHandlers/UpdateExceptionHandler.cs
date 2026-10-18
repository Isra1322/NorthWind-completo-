namespace NorthWind.Exceptions.Entities.ExceptionHandlers;

internal class UpdateExceptionHandler(ILogger<UpdateExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
        Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not UpdateException Ex) return false;
        var Details = new ProblemDetails
        {
            Status = StatusCodes.Status500InternalServerError,
            Type = "https://datatracker.ietf.org/doc/html/rfc7231#section-6.6.1",
            Title = ExceptionMessages.UpdateExceptionTitle,
            Detail = ExceptionMessages.UpdateExceptionDetail,
            Instance = "ProblemDetails/UpdateException"
        };
        logger.LogError(exception,
            ExceptionMessages.UpdateExceptionTitle + ": " + string.Join(" ", Ex.Entities));
        await httpContext.WriteProblemDetailsAsync(Details);
        return true;
    }
}