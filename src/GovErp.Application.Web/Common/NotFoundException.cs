namespace GovErp.Application.Web.Common;

public sealed class NotFoundException(string message) : Exception(message);
