using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Tests;

public class VendorInvoiceTests
{
    private static readonly UserId Author = UserId.New();
    private static readonly UserId Chief = UserId.New();
    private static readonly Guid VendorId = Guid.NewGuid();
    private static readonly DateOnly Date = new(2026, 6, 15);
    private static readonly DateTimeOffset At = new(2026, 9, 22, 10, 0, 0, TimeSpan.Zero);
    private static readonly AccountCode Account = AccountCode.Parse("101-6000-53100");
    private static readonly ApprovalRequirement Head = new(ApproverRole.DepartmentHead, new DepartmentCode("6000"));

    private static VendorInvoice Draft() => new(" inv-i ", VendorId, Date, Date.AddDays(-1), Date,
        Date.AddDays(30), Money.Of(100), null, Author, At);

    private static VendorInvoice Submitted()
    {
        var invoice = Draft();
        invoice.AddDistribution(Account, Money.Of(100), null);
        invoice.Submit(Guid.NewGuid(), "rules-1", [Head], [Guid.NewGuid()], [Guid.NewGuid()], [Guid.NewGuid()]);
        return invoice;
    }

    private static void Approve(VendorInvoice invoice)
    {
        invoice.RecordApproval(Head.Role, Head.Department, Chief, invoice.LastEvaluationRef!.Value, At);
        invoice.MarkApproved();
    }

    private static OverrideTarget Target(VendorInvoice invoice) => new(invoice.LastEvaluationRef!.Value,
        Guid.NewGuid(), "BUDGET", 1, 1, invoice.ContentVersion, invoice.ApprovalCycleId!.Value);

    private static void Post(VendorInvoice invoice) => invoice.Post(invoice.LastEvaluationRef!.Value,
        invoice.ContentVersion, invoice.ApprovalCycleId!.Value, "rules-1", At);

    [Fact]
    public void Replacing_lines_with_the_same_lines_changes_nothing_and_other_lines_replace_them()
    {
        var invoice = Draft();
        invoice.AddDistribution(Account, Money.Of(100), null);
        var version = invoice.ContentVersion;

        invoice.ReplaceDistributions([(Account, Money.Of(100), null)]);
        invoice.ContentVersion.Should().Be(version);

        var other = AccountCode.Parse("202-4000-53100");
        invoice.ReplaceDistributions([(Account, Money.Of(60), null), (other, Money.Of(40), null)]);
        invoice.Distributions.Select(d => (d.LineNo, d.Account, d.Amount))
            .Should().Equal((1, Account, Money.Of(60)), (2, other, Money.Of(40)));
        invoice.ContentVersion.Should().BeGreaterThan(version);

        var submitted = Submitted();
        FluentActions.Invoking(() => submitted.ReplaceDistributions([(Account, Money.Of(100), null)]))
            .Should().Throw<Exceptions.PayablesException>();
    }

    [Fact]
    public void Rejection_immediately_deactivates_previous_overrides()
    {
        var invoice = Submitted();
        var target = Target(invoice);
        invoice.Override(target, ApproverRole.FinanceDirector, Chief, "exception", At);
        invoice.Reject(Head.Role, Head.Department, Chief, "wrong coding", At);
        Assert.False(invoice.HasCurrentOverride(target));
        Assert.Single(invoice.Overrides);
    }

    [Fact]
    public void Due_date_before_invoice_date_is_rejected()
    {
        FluentActions.Invoking(() => new VendorInvoice("I-1", VendorId, Date, Date, Date, Date.AddDays(-1), Money.Of(100), null, Author, At))
            .Should().Throw<PayablesException>();
        var invoice = Draft();
        FluentActions.Invoking(() => invoice.UpdateHeader(invoice.Number, VendorId, Date, Date, Date, Date.AddDays(-1), Money.Of(100), null))
            .Should().Throw<PayablesException>();
    }

    [Fact]
    public void Draft_normalizes_number_and_keeps_dates_distinct()
    {
        var invoice = Draft();
        invoice.NormalizedInvoiceNumber.Should().Be("INV-I");
        invoice.ContentVersion.Should().Be(1);
        invoice.InvoiceDate.Should().Be(Date);
        invoice.ServiceDate.Should().Be(Date.AddDays(-1));
        invoice.PostingDate.Should().Be(Date);
        invoice.DueDate.Should().Be(Date.AddDays(30));
        invoice.CreatedAt.Should().Be(At);
        invoice.Status.Should().Be(InvoiceStatus.Draft);
    }

    [Fact]
    public void Distribution_edits_increment_content_and_renumber()
    {
        var invoice = Draft();
        invoice.AddDistribution(Account, Money.Of(60), null);
        invoice.AddDistribution(Account, Money.Of(40), null);
        invoice.RemoveDistribution(1);
        invoice.Distributions.Should().ContainSingle(d => d.LineNo == 1 && d.Amount == Money.Of(40));
        invoice.DistributedTotal.Should().Be(Money.Of(40));
        invoice.ContentVersion.Should().Be(4);
        FluentActions.Invoking(() => invoice.RemoveDistribution(9)).Should().Throw<PayablesException>();
        invoice.ContentVersion.Should().Be(4);
    }

    [Fact]
    public void Header_edit_changes_content_only_when_values_change()
    {
        var invoice = Draft();
        invoice.UpdateHeader(invoice.Number, VendorId, Date, Date.AddDays(-1), Date, Date.AddDays(30), Money.Of(100), null);
        invoice.ContentVersion.Should().Be(1);
        invoice.UpdateHeader(" next ", VendorId, Date, Date, Date.AddDays(1), Date.AddDays(60), Money.Of(200), "PO-1");
        invoice.ContentVersion.Should().Be(2);
        invoice.NormalizedInvoiceNumber.Should().Be("NEXT");
        invoice.Total.Should().Be(Money.Of(200));
        invoice.IsPoBacked.Should().BeTrue();
    }

    [Fact]
    public void Submit_requires_nonempty_balanced_distributions_and_preserves_state_on_failure()
    {
        var invoice = Draft();
        FluentActions.Invoking(() => invoice.Submit(Guid.NewGuid(), "r", [Head], [])).Should().Throw<PayablesException>();
        invoice.AddDistribution(Account, Money.Of(99), null);
        FluentActions.Invoking(() => invoice.Submit(Guid.NewGuid(), "r", [Head], [])).Should().Throw<PayablesException>();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.ApprovalCycleId.Should().BeNull();
    }

    [Fact]
    public void Submitted_content_cannot_be_edited()
    {
        var invoice = Submitted();
        FluentActions.Invoking(() => invoice.AddDistribution(Account, Money.Of(1), null)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.RemoveDistribution(1)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.UpdateHeader("x", VendorId, Date, Date, Date, Date, Money.Of(100), null))
            .Should().Throw<PayablesException>();
    }

    [Fact]
    public void Approval_requires_non_author_matching_role_department_and_current_evaluation()
    {
        var invoice = Submitted();
        var evaluation = invoice.LastEvaluationRef!.Value;
        FluentActions.Invoking(() => invoice.RecordApproval(Head.Role, Head.Department, Author, evaluation, At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.RecordApproval(Head.Role, new DepartmentCode("4000"), Chief, evaluation, At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.RecordApproval(ApproverRole.FinanceDirector, null, Chief, evaluation, At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.RecordApproval(Head.Role, Head.Department, Chief, Guid.NewGuid(), At)).Should().Throw<PayablesException>();
        invoice.Approvals.Should().BeEmpty();
        Approve(invoice);
        invoice.ContentVersion.Should().Be(2);
        invoice.Approvals.Single().At.Should().Be(At);
    }

    [Fact]
    public void Each_department_needs_its_own_approval_and_duplicate_is_rejected()
    {
        var invoice = Draft();
        invoice.AddDistribution(Account, Money.Of(100), null);
        var other = new ApprovalRequirement(Head.Role, new DepartmentCode("4000"));
        var evaluation = Guid.NewGuid();
        invoice.Submit(evaluation, "r", [Head, other], []);
        invoice.RecordApproval(Head.Role, Head.Department, Chief, evaluation, At);
        FluentActions.Invoking(() => invoice.MarkApproved()).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.RecordApproval(Head.Role, Head.Department, Chief, evaluation, At)).Should().Throw<PayablesException>();
        invoice.RecordApproval(other.Role, other.Department, Chief, evaluation, At);
        invoice.MarkApproved();
        invoice.Status.Should().Be(InvoiceStatus.Approved);
    }

    [Fact]
    public void Override_binds_to_exact_outcome_rule_line_content_and_cycle()
    {
        var invoice = Submitted();
        var target = Target(invoice);
        invoice.Override(target, ApproverRole.FinanceDirector, Chief, "Budget exception approved", At);
        var decision = invoice.Overrides.Single();
        decision.Target.Should().Be(target);
        decision.Reason.Should().Be("Budget exception approved");
        invoice.HasCurrentOverride(target).Should().BeTrue();
        invoice.HasCurrentOverride(target with { OutcomeRef = Guid.NewGuid() }).Should().BeFalse();
        invoice.HasCurrentOverride(target with { RuleVersion = 2 }).Should().BeFalse();
        invoice.ContentVersion.Should().Be(2);
        FluentActions.Invoking(() => invoice.Override(target, ApproverRole.FinanceDirector, Chief, " ", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Override(target with { ContentVersion = 1 }, ApproverRole.FinanceDirector, Chief, "reason", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Override(target with { ApprovalCycleId = Guid.NewGuid() }, ApproverRole.FinanceDirector, Chief, "reason", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Override(target with { DistributionLine = 9 }, ApproverRole.FinanceDirector, Chief, "reason", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Override(target, ApproverRole.FinanceDirector, Author, "reason", At)).Should().Throw<PayablesException>();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Reject_preserves_history_and_returns_release_ownership(bool approved)
    {
        var invoice = Submitted();
        var target = Target(invoice);
        invoice.Override(target, ApproverRole.FinanceDirector, Chief, "exception", At);
        if (approved) Approve(invoice);
        var cycle = invoice.ApprovalCycleId;
        var reservations = invoice.ReservationRefs.ToArray();
        var release = invoice.Reject(Head.Role, Head.Department, Chief, "wrong coding", At);
        invoice.Status.Should().Be(InvoiceStatus.Rejected);
        release.InvoiceId.Should().Be(invoice.Id);
        release.ContentVersion.Should().Be(2);
        release.ReservationRefs.Should().Equal(reservations);
        release.EncumbranceClaimRefs.Should().ContainSingle();
        release.PoBillingClaimRefs.Should().ContainSingle();
        invoice.ReturnToDraft();
        invoice.ContentVersion.Should().Be(2);
        invoice.ApprovalCycleId.Should().BeNull();
        invoice.Approvals.Should().Contain(a => a.Decision == ApprovalDecision.Rejected && a.ApprovalCycleId == cycle && a.At == At);
        invoice.Overrides.Should().ContainSingle();
        invoice.HasCurrentOverride(target).Should().BeFalse();
        invoice.Submit(Guid.NewGuid(), "rules-1", [Head], []);
        Assert.NotEqual(cycle, invoice.ApprovalCycleId);
        FluentActions.Invoking(() => invoice.MarkApproved()).Should().Throw<PayablesException>();
        Approve(invoice);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Withdraw_is_author_only_cancellable_and_closes_cycle(bool approved)
    {
        var invoice = Submitted();
        invoice.Override(Target(invoice), ApproverRole.FinanceDirector, Chief, "exception", At);
        if (approved) Approve(invoice);
        FluentActions.Invoking(() => invoice.Withdraw(Chief, "correction", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Withdraw(Author, " ", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Withdraw(Author, "correction", At, new CancellationToken(true)))
            .Should().Throw<OperationCanceledException>();
        invoice.ReservationRefs.Should().ContainSingle();
        var release = invoice.Withdraw(Author, "correction", At);
        release.ReservationRefs.Should().ContainSingle();
        invoice.Status.Should().Be(InvoiceStatus.Draft);
        invoice.ApprovalCycleId.Should().BeNull();
        invoice.ContentVersion.Should().Be(2);
        invoice.Overrides.Should().ContainSingle();
        invoice.Withdrawals.Should().ContainSingle(w => w.Reason == "correction" && w.At == At);
        invoice.ReservationRefs.Should().BeEmpty();
    }

    [Fact]
    public void Rule_change_starts_new_cycle_but_budget_reevaluation_does_not()
    {
        var invoice = Submitted();
        var oldTarget = Target(invoice);
        invoice.Override(oldTarget, ApproverRole.FinanceDirector, Chief, "exception", At);
        Approve(invoice);
        var cycle = invoice.ApprovalCycleId;
        var evaluation = Guid.NewGuid();
        invoice.Reevaluate(evaluation, invoice.ContentVersion, "rules-1", [Head]);
        invoice.ApprovalCycleId.Should().Be(cycle);
        invoice.Status.Should().Be(InvoiceStatus.Approved);
        invoice.Reevaluate(Guid.NewGuid(), invoice.ContentVersion, "rules-2", [Head]);
        Assert.NotEqual(cycle, invoice.ApprovalCycleId);
        invoice.Status.Should().Be(InvoiceStatus.Submitted);
        invoice.HasCurrentOverride(oldTarget).Should().BeFalse();
        invoice.Approvals.Should().ContainSingle();
        invoice.ReservationRefs.Should().ContainSingle();
        FluentActions.Invoking(() => invoice.MarkApproved()).Should().Throw<PayablesException>();
        Approve(invoice);
        invoice.Post(invoice.LastEvaluationRef!.Value, invoice.ContentVersion, invoice.ApprovalCycleId!.Value, "rules-2", At);
        invoice.Status.Should().Be(InvoiceStatus.Posted);
    }

    [Fact]
    public void Post_rejects_stale_cycle_content_rules_and_unapproved_state()
    {
        var invoice = Submitted();
        FluentActions.Invoking(() => Post(invoice)).Should().Throw<PayablesException>();
        Approve(invoice);
        FluentActions.Invoking(() => invoice.Post(invoice.LastEvaluationRef!.Value, 1, invoice.ApprovalCycleId!.Value, "rules-1", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Post(invoice.LastEvaluationRef!.Value, 2, Guid.NewGuid(), "rules-1", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Post(invoice.LastEvaluationRef!.Value, 2, invoice.ApprovalCycleId!.Value, "rules-2", At)).Should().Throw<PayablesException>();
        Post(invoice);
        invoice.PostedAt.Should().Be(At);
        FluentActions.Invoking(() => Post(invoice)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Withdraw(Author, "late", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => invoice.Reject(Head.Role, Head.Department, Chief, "late", At)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Payment_handoff_is_computed_using_explicit_business_date_vendor_and_hold()
    {
        var invoice = Submitted();
        invoice.ReadyForPaymentHandoff(true, Date.AddDays(30)).Should().BeFalse();
        Approve(invoice);
        Post(invoice);
        invoice.ReadyForPaymentHandoff(true, Date.AddDays(29)).Should().BeFalse();
        invoice.ReadyForPaymentHandoff(false, Date.AddDays(30)).Should().BeFalse();
        invoice.ReadyForPaymentHandoff(true, Date.AddDays(30)).Should().BeTrue();
        invoice.SetPaymentHold(true);
        invoice.ReadyForPaymentHandoff(true, Date.AddDays(31)).Should().BeFalse();
        invoice.SetPaymentHold(false);
        invoice.ReadyForPaymentHandoff(true, Date.AddDays(31)).Should().BeTrue();
        invoice.Status.Should().Be(InvoiceStatus.Posted);
        invoice.ContentVersion.Should().Be(2);
    }

    [Fact]
    public void Registered_reference_is_kept_and_blank_reference_is_rejected()
    {
        new VendorInvoice("V-1", VendorId, Date, Date, Date, Date.AddDays(30), Money.Of(10), null, Author, At, "AP-2026-000001")
            .Reference.Should().Be("AP-2026-000001");
        FluentActions.Invoking(() => new VendorInvoice("V-1", VendorId, Date, Date, Date, Date.AddDays(30), Money.Of(10), null, Author, At, " "))
            .Should().Throw<PayablesException>();
        Draft().Reference.Should().StartWith("INV-");
    }

    [Fact]
    public void Changes_to_owned_collections_advance_change_stamp()
    {
        var inv = Submitted();
        var before = inv.ChangeStamp;
        inv.RecordApproval(Head.Role, Head.Department, Chief, inv.LastEvaluationRef!.Value, At);
        inv.ChangeStamp.Should().BeGreaterThan(before);
    }
}
