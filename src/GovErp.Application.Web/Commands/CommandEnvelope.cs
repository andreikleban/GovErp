namespace GovErp.Application.Web.Commands;

/// <summary>The form keeps CommandId until the response; ExpectedRowVersion is the base64 RowVersion of the aggregate the user saw (null for Create).</summary>
public sealed record CommandEnvelope(Guid CommandId, string? ExpectedRowVersion);
