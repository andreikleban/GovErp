namespace GovErp.Application.Web.Commands;

/// <summary>Связывает агрегат с RowVersion, который видел пользователь: устаревшая форма даёт Conflict, а не тихую перезапись.</summary>
public interface IConcurrencyGuard
{
    void Expect(object aggregate, string? rowVersion);
    string? VersionOf(object aggregate);
}
