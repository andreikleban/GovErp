using System.Text.Json;
using GovErp.Domain.Validation.Codes;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Persistence;
using GovErp.Infrastructure.Seed;
using InvoiceRequirement = GovErp.Domain.Payables.Entities.ApprovalRequirement;
using InvoiceRole = GovErp.Domain.Payables.Entities.ApproverRole;
using OverrideTarget = GovErp.Domain.Payables.Entities.OverrideTarget;

namespace GovErp.Application.Web.Tests;

public class JsonRoundTripTests
{
    private static T RoundTrip<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value, JsonColumn.Options), JsonColumn.Options)!;

    [Fact]
    public async Task Validation_subject_and_evaluation_parts_round_trip()
    {
        var d = SpringfieldData.Create();
        var inv = d.PoBackedInvoice(164_800m);
        var subject = await ValidationSubjectAssemblerTests.Assembler(d).BuildAsync(inv, SpringfieldData.Jun15);
        var record = new ValidationPipeline(RuleCatalog.Default)
            .Evaluate(subject, d.Rules, EvaluationTrigger.Manual, SpringfieldData.ClerkId, DateTimeOffset.UtcNow);

        var back = RoundTrip(subject);
        back.Should().BeEquivalentTo(subject);
        back.Distributions.Single().Encumbrance!.ClaimableForInvoice.Should().Be(Money.Of(160_000m));

        RoundTrip(record.Outcomes).Should().BeEquivalentTo(record.Outcomes);
        RoundTrip(record.RuleSetVersions).Should().Be(record.RuleSetVersions);
        RoundTrip(record.Steps).Should().BeEquivalentTo(record.Steps);
        RoundTrip(record.PostingCheck).Should().BeEquivalentTo(record.PostingCheck);
    }

    [Fact]
    public void Approval_reason_reads_a_code_object_and_a_legacy_sentence()
    {
        var coded = new ApprovalRequirement(ApproverRole.GrantsManager, null, Problem.Of(RouteReasons.GrantFunded), false);
        RoundTrip(coded).Should().BeEquivalentTo(coded);

        const string legacy = """[{"Role":"DepartmentHead","Department":"6000","Reason":"Department 6000 is charged.","IsSatisfied":false}]""";
        var read = JsonSerializer.Deserialize<List<ApprovalRequirement>>(legacy, JsonColumn.Options)!;
        read.Should().ContainSingle().Which.Reason.Code.Should().Be("Department 6000 is charged.");
    }

    [Fact]
    public void Invoice_approval_route_and_override_target_round_trip()
    {
        IReadOnlyList<InvoiceRequirement> route =
            [new(InvoiceRole.DepartmentHead, new DepartmentCode("6000")), new(InvoiceRole.FinanceDirector, null)];
        RoundTrip(route).Should().Equal(route);
        var target = new OverrideTarget(Guid.NewGuid(), Guid.NewGuid(), "BUDGET", 2, 1, 3, Guid.NewGuid());
        RoundTrip(target).Should().Be(target);
    }
}
