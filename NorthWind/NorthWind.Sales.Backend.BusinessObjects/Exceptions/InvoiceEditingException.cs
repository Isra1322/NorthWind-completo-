namespace NorthWind.Sales.Backend.BusinessObjects.Exceptions;

public sealed class InvoiceInputException(string message) : ArgumentException(message);
public sealed class InvoiceConflictException(string message, Exception? innerException = null) : InvalidOperationException(message, innerException);
