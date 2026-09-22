namespace GovErp.Domain.Validation.Exceptions;

public sealed class ValidationException(string message) : Exception(message);
