using System.Text.RegularExpressions;
using GovErp.Application.Web.Approvals.Contracts;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation.Contracts;

namespace GovErp.Web.Components.Invoices;

/// <summary>
/// Панель действий карточки: набор кнопок всегда один, а здесь для каждой — почему она сейчас недоступна
/// (null — доступна). Правила повторяют проверки сервисов, чтобы UI не предлагал заведомый отказ; окончательно решает сервер.
/// Решения по маршруту (Approve, Reject, Release Hold) берутся из очереди пользователя — той же, что на главной и в Approvals.
/// </summary>
public sealed partial class InvoiceActionPolicy
{
    private const string SaveFirst = "Save the invoice first.";
    private const string Posters = "Only the budget officer or finance director";
    private readonly InvoiceVm? _invoice;
    private readonly Guid _userId;
    private readonly bool _isClerk;
    private readonly bool _isPoster;
    private readonly bool _dirty;

    /// <param name="invoice">Null — новый документ, ещё не сохранённый.</param>
    /// <param name="pending">Элементы очереди текущего пользователя по этому инвойсу.</param>
    /// <param name="dirty">В форме есть несохранённые правки.</param>
    public InvoiceActionPolicy(InvoiceVm? invoice, Guid userId, bool isClerk, bool isPoster, IReadOnlyList<ApprovalQueueItemVm> pending, bool dirty)
    {
        _invoice = invoice;
        _userId = userId;
        _isClerk = isClerk;
        _isPoster = isPoster;
        _dirty = dirty;
        ApproveItem = pending.FirstOrDefault(p => p.Kind == "Approve");
        ReleaseItems = pending.Where(p => p.Kind == "Override").ToList();
    }

    public ApprovalQueueItemVm? ApproveItem { get; }
    public IReadOnlyList<ApprovalQueueItemVm> ReleaseItems { get; }

    private bool IsNew => _invoice is null;
    private string Status => _invoice?.Status ?? "New";
    private bool IsAuthor => _invoice is null || _invoice.CreatedBy == _userId;
    private EvaluationVm? Last => _invoice?.LastEvaluation;
    private IEnumerable<OutcomeVm> OpenSoftStops => Last?.Outcomes.Where(o => o.Severity == "SoftStop" && o.OverriddenBy is null) ?? [];
    private bool HasOpenHolds => Last?.Outcomes.Any(o => o.Severity is "SoftStop" or "HardStop" && o.OverriddenBy is null) == true;

    /// <summary>Правка шапки и строк на месте: только автор-клерк и только Draft; новый документ — любой клерк.</summary>
    public string? Edit =>
        !_isClerk ? "Only AP clerks edit invoices."
        : IsNew ? null
        : !IsAuthor ? "Only the author edits this invoice."
        : Status != "Draft" ? $"Only a draft is edited; this invoice is {Status}."
        : null;

    public bool CanEdit => Edit is null;

    public string? Save => Edit;

    public string? Submit =>
        IsNew ? SaveFirst
        : !_isClerk ? "Only AP clerks submit invoices."
        : !IsAuthor ? "Only the author submits this invoice."
        : Status != "Draft" ? $"Only a draft is submitted; this invoice is {Status}."
        : _dirty ? "Save your changes first: Submit sends the saved version."
        : null;

    public string? CheckFunds =>
        IsNew ? SaveFirst
        : Status is not ("Draft" or "Submitted" or "Approved") ? $"Funds are checked while the invoice is in progress; this invoice is {Status}."
        : _dirty ? "Save your changes first: the check runs on the saved version."
        : null;

    public string? Approve =>
        IsNew ? SaveFirst
        : ApproveItem is null ? NotApprover
        : HasOpenHolds ? "Open holds must be released before approval."
        : null;

    public string? Reject => IsNew ? SaveFirst : ApproveItem is null ? NotApprover : null;

    public string? ReleaseHold =>
        IsNew ? SaveFirst
        : ReleaseItems.Count > 0 ? null
        : Status is not ("Submitted" or "Approved") ? $"Holds are released on submitted or approved invoices; this invoice is {Status}."
        : !OpenSoftStops.Any() ? "No open soft stop to release."
        : IsAuthor ? "The author cannot release holds on their own invoice (separation of duties)."
        : $"Only {string.Join(" or ", OpenSoftStops.SelectMany(o => o.OverridableBy).Distinct().Select(RoleLabel))} can release these holds.";

    /// <summary>
    /// Допуск к проводке (PostingEligibility) движок вычисляет только в оценке самой команды Post, поэтому кнопка
    /// доступна по роли, статусу и отсутствию неснятых стопов; окончательную проверку делает сервер.
    /// </summary>
    public string? Post =>
        IsNew ? SaveFirst
        : !_isPoster ? $"{Posters} posts."
        : Status != "Approved" ? $"Only approved invoices are posted; this invoice is {Status}."
        : Last?.Overall is "HardStop" or "SoftStop" ? "Open holds must be released before posting."
        : null;

    public string? Withdraw =>
        IsNew ? SaveFirst
        : !IsAuthor ? "Only the author withdraws the invoice."
        : Status is not ("Submitted" or "Approved") ? $"Only submitted or approved invoices are withdrawn; this invoice is {Status}."
        : null;

    public string? ReturnToDraft =>
        IsNew ? SaveFirst
        : !IsAuthor ? "Only the author returns the invoice to draft."
        : Status != "Rejected" ? $"Only a rejected invoice returns to draft; this invoice is {Status}."
        : null;

    public string? PaymentHold =>
        IsNew ? SaveFirst
        : !_isPoster ? $"{Posters} holds or releases payment."
        : Status != "Posted" ? $"Payment hold applies to posted invoices; this invoice is {Status}."
        : null;

    public string PaymentHoldLabel => _invoice?.PaymentHold == true ? "Release Payment" : "Hold Payment";

    public string? Audit => IsNew ? "Nothing is recorded before the first save." : null;

    private string NotApprover =>
        Status != "Submitted" ? $"Only submitted invoices wait for approval; this invoice is {Status}."
        : IsAuthor ? "The author cannot approve or reject their own invoice (separation of duties)."
        : "No approval step on this invoice is waiting for you.";

    /// <summary>«BudgetOfficer» → «Budget Officer».</summary>
    public static string RoleLabel(string role) => WordBoundary().Replace(role, "$1 $2");

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
