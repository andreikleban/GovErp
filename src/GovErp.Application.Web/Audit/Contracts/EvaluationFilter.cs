namespace GovErp.Application.Web.Audit.Contracts;

/// <summary>Фильтры списка оценок; null — без ограничения. Document — подстрока TransactionRef (номер документа), без учёта регистра;
/// Trigger и Overall — точное совпадение без учёта регистра; RuleFingerprint — подстрока отпечатка (в таблице он сокращён).</summary>
public sealed record EvaluationFilter(string? Document = null, string? Trigger = null, string? Overall = null, string? RuleFingerprint = null)
{
    public static readonly EvaluationFilter None = new();
}
