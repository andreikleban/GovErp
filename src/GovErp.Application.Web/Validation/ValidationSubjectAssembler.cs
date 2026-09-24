using GovErp.Application.Web.Common;
using GovErp.Domain.ChartOfAccounts.Repositories;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;
using GovErp.Domain.Validation.Repositories;
using GovErp.Domain.Validation.ValueObjects;
using Microsoft.Extensions.Options;

namespace GovErp.Application.Web.Validation;

/// <summary>The only place where the four contexts meet (GE-9): reads the aggregates and builds the snapshots.</summary>
public sealed class ValidationSubjectAssembler(
    IFundRepository funds, IGrantRepository grants, IAccountCombinationRepository combinations,
    IBudgetLineRepository budgetLines, IEncumbranceRepository encumbrances, IFiscalPeriodRepository periods,
    IVendorRepository vendors, IVendorInvoiceRepository invoices, IEvaluationRecordRepository evaluations,
    IOptions<PostingOptions> posting)
{
    public async Task<ValidationSubject> BuildAsync(VendorInvoice invoice, DateOnly businessDate, CancellationToken ct = default)
    {
        var vendor = await vendors.FindAsync(invoice.VendorId, ct) ?? throw new NotFoundException($"Vendor {invoice.VendorId} not found.");
        var fy = FiscalYear.FromDate(invoice.PostingDate);
        var (py, pm) = FiscalPeriod.KeyFor(invoice.PostingDate);
        var period = await periods.FindAsync(py, pm, ct);
        var cycle = invoice.ApprovalCycleId ?? Guid.Empty;
        var fingerprint = invoice.RuleFingerprint ?? "";
        var cv = invoice.ContentVersion;

        var tx = new TransactionSnapshot(invoice.Reference, cv, "AP_INVOICE", invoice.InvoiceDate, invoice.Total,
            new VendorSnapshot(vendor.Id, vendor.Name, vendor.Status == VendorStatus.Debarred, vendor.SamRegistered, vendor.Status == VendorStatus.Active),
            invoice.IsPoBacked, await invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.NormalizedInvoiceNumber, invoice.Id, ct),
            invoice.CreatedBy, cycle, fingerprint, invoice.Status.ToString(),
            invoice.ServiceDate, invoice.PostingDate, invoice.DueDate, invoice.PaymentHold);

        var budgets = new Dictionary<AccountCode, BudgetSnapshot>();
        foreach (var key in invoice.Distributions.Select(d => d.Account).Distinct())
        {
            var line = await budgetLines.FindAsync(key, fy, ct);
            budgets[key] = line is null
                ? BudgetSnapshot.Missing with { FiscalYear = fy.Year }
                : new BudgetSnapshot(true, line.Amended, line.Actuals, line.Encumbered, line.Held, line.Available, line.OwnHeld(invoice.Id, cv), fy.Year);
        }

        var distributions = new List<DistributionSnapshot>();
        foreach (var d in invoice.Distributions)
        {
            distributions.Add(new DistributionSnapshot(d.LineNo, d.Account, d.Amount,
                await CombinationAsync(d.Account, invoice.InvoiceDate, ct), await FundAsync(d, ct), await GrantAsync(d, invoice.ServiceDate, ct),
                budgets[d.Account], await EncumbranceAsync(invoice, d, ct)));
        }

        var approvals = invoice.Approvals
            .Where(a => a.Decision == ApprovalDecision.Approved)
            .Select(a => new ApprovalSnapshot(RoleMapping.ToValidation(a.Role), a.UserId, a.Department, a.ContentVersion,
                a.ApprovalCycleId, a.RuleFingerprint, a.EvaluationRef))
            .ToList();

        var active = invoice.Overrides.Where(o => o.Target.ContentVersion == cv && o.Target.ApprovalCycleId == cycle).ToList();
        var overrides = active.Select(o => new OverrideSnapshot(o.Target.RuleId, o.UserId, o.Reason, o.Target.RuleVersion,
            o.Target.DistributionLine, o.Target.ContentVersion, o.Target.ApprovalCycleId, fingerprint, o.Target.EvaluationRef,
            RoleMapping.ToValidation(o.Role), o.Target.OutcomeRef)).ToList();
        var previous = new List<PreviousEvaluation>();
        foreach (var id in active.Select(o => o.Target.EvaluationRef).Distinct())
        {
            if (await evaluations.FindAsync(id, ct) is { } record)
            {
                previous.Add(new PreviousEvaluation(record.Id, record.Outcomes));
            }
        }

        return new ValidationSubject(tx, distributions, [], overrides, period?.IsOpen ?? false, null, posting.Value.ToPostingAccounts(),
            approvals, businessDate) { PreviousEvaluations = previous };
    }

    private async Task<CombinationSnapshot> CombinationAsync(AccountCode account, DateOnly invoiceDate, CancellationToken ct) =>
        await combinations.FindAsync(account, ct) is { } c
            ? new CombinationSnapshot(true, c.IsActiveOn(invoiceDate), c.Status.ToString())
            : new CombinationSnapshot(false, false, "Missing");

    private async Task<FundSnapshot?> FundAsync(InvoiceDistribution d, CancellationToken ct) =>
        await funds.FindAsync(d.Account.Fund, ct) is { } f
            ? new FundSnapshot(f.Code.Value, f.Name, Enum.Parse<FundKind>(f.Type.ToString()), Enum.Parse<BudgetControl>(f.ControlMode.ToString()),
                Enum.Parse<GrantRule>(f.GrantPolicy.ToString()), Enum.Parse<FundRestriction>(f.Check(d.Account.Department, d.Account.Object).ToString()), f.IsActive)
            : null;

    private async Task<GrantSnapshot?> GrantAsync(InvoiceDistribution d, DateOnly serviceDate, CancellationToken ct)
    {
        if (d.Account.Grant is null)
        {
            return null;
        }

        return await grants.FindAsync(d.Account.Grant, ct) is { } g
            ? new GrantSnapshot(g.Code.Value, g.IsFederal,
                Enum.Parse<GrantEligibilityResult>(g.CheckEligibility(serviceDate, d.Account.Department, d.Account.Object).ToString()), g.Status.ToString())
            : new GrantSnapshot(d.Account.Grant.Value, false, GrantEligibilityResult.GrantNotActive, "Missing");
    }

    private async Task<EncumbranceSnapshot?> EncumbranceAsync(VendorInvoice invoice, InvoiceDistribution d, CancellationToken ct)
    {
        if (invoice.PoRef is null || d.PoLineNo is not { } no)
        {
            return null;
        }

        var lineRef = $"{invoice.PoRef}/{no}";
        if (await encumbrances.FindByPoLineAsync(lineRef, ct) is not { } e)
        {
            return null;   // BudgetAllocation → «Missing PO line snapshot»
        }

        bool Own(Guid invoiceId, int version) => invoiceId == invoice.Id && version == invoice.ContentVersion;
        Money Sum(IEnumerable<Money> values) => values.Aggregate(Money.Zero, (s, m) => s + m);
        var heldClaims = e.Claims.Where(c => c.Status == ClaimStatus.Held).ToList();
        var heldBilling = e.BillingClaims.Where(c => c.Status == ClaimStatus.Held).ToList();
        // IsOpen = billing still allowed. Fully liquidated lines close the encumbrance (Remaining = 0)
        // but Released stays zero; residual invoices within PO tolerance must still pass (GE-17).
        return new EncumbranceSnapshot(lineRef, e.Remaining, e.Released.IsZero,
            e.AuthorizedPoAmount, e.AlreadyPostedAgainstPo,
            OtherActiveInvoiceClaims: Sum(heldBilling.Where(c => !Own(c.InvoiceId, c.ContentVersion)).Select(c => c.Amount)),
            CurrentInvoicePoAmount: Sum(invoice.Distributions.Where(x => x.PoLineNo == no).Select(x => x.Amount)),
            OtherLiquidationClaims: Sum(heldClaims.Where(c => !Own(c.InvoiceId, c.ContentVersion)).Select(c => c.Amount)),
            OwnLiquidationClaim: Sum(heldClaims.Where(c => Own(c.InvoiceId, c.ContentVersion)).Select(c => c.Amount)));
    }
}
