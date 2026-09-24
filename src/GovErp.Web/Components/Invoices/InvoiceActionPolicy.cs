using System.Text.RegularExpressions;
using GovErp.Application.Web.Approvals.Contracts;
using GovErp.Application.Web.Invoices.Contracts;
using GovErp.Application.Web.Validation.Contracts;

namespace GovErp.Web.Components.Invoices;

/// <summary>
/// The card action bar: the button set is always the same, and here each button gets the reason it is unavailable now
/// (null means available). The rules mirror the service checks so the UI does not offer a certain refusal; the server decides in the end.
/// Route decisions (Approve, Reject, Release Hold) come from the user's queue, the same one as on Home and in Approvals.
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

    /// <param name="invoice">Null for a new document that is not saved yet.</param>
    /// <param name="pending">The current user's queue items for this invoice.</param>
    /// <param name="dirty">The form has unsaved edits.</param>
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

    /// <summary>
    /// In-place editing of the header and lines: only the author clerk and only in Draft; a new document: any clerk.
    /// </summary>
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
    /// The engine computes posting eligibility (PostingEligibility) only in the evaluation of the Post command itself, so the button
    /// is available by role, status and the absence of open stops; the server makes the final check.
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

    public string? Pay =>
        IsNew ? SaveFirst
        : !_isPoster ? $"{Posters} records a payment."
        : Status != "Posted" ? $"Only a posted invoice is paid; this invoice is {Status}."
        : _invoice!.PaidAt is not null ? "This invoice is already paid."
        : _invoice.PaymentHold ? "A payment hold is on."
        : !_invoice.ReadyForPaymentHandoff ? "Payment waits until the vendor is active and the due date is on or before the business date."
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

    /// <summary>
    /// «BudgetOfficer» → «Budget Officer».
    /// </summary>
    public static string RoleLabel(string role) => WordBoundary().Replace(role, "$1 $2");

    [GeneratedRegex("([a-z])([A-Z])")]
    private static partial Regex WordBoundary();
}
