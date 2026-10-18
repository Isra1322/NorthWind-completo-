namespace NorthWind.Exceptions.Entities.ExceptionHandlers;

internal class ValidationExceptionHandler : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext,
        Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not ValidationException Ex) return false;
        bool conflict = exception is InventoryConflictException;
        var Details = new ProblemDetails
        {
            Status = conflict ? StatusCodes.Status409Conflict : StatusCodes.Status400BadRequest,
            Type = conflict ? "https://www.rfc-editor.org/rfc/rfc9110.html#section-15.5.10" : "https://datatracker.ietf.org/doc/html/rfc7231#section-6.5.1",
            Title = conflict ? "Conflicto de inventario" : ExceptionMessages.ValidationExceptionTitle,
            Detail = conflict ? "El inventario cambió. Puede volver a enviar para comprobar el stock nuevamente." : ExceptionMessages.ValidationExceptionDetail,
            Instance = "ProblemDetails/ValidationException"
        };
        Details.Extensions.Add("errors", Ex.Errors);
        await httpContext.WriteProblemDetailsAsync(Details);
        return true;
    }
}
