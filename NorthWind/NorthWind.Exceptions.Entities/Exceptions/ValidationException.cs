namespace NorthWind.Exceptions.Entities.Exceptions;

public class ValidationException : Exception
{
    public ValidationException() { }
    public ValidationException(string message) : base(message) { }
    public ValidationException(string message, Exception innerException)
        : base(message, innerException) { }
    public ValidationException(IEnumerable<ValidationError> errors) =>
        Errors = errors.ToArray();

    public IEnumerable<ValidationError> Errors { get; } = [];
}