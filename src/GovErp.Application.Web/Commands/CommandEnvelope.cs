namespace GovErp.Application.Web.Commands;

/// <summary>CommandId удерживается формой до ответа; ExpectedRowVersion — base64 RowVersion агрегата, который видел пользователь (null для Create).</summary>
public sealed record CommandEnvelope(Guid CommandId, string? ExpectedRowVersion);
