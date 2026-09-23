using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Audit.Contracts;
using GovErp.Infrastructure.Seed;

namespace GovErp.Application.Web.Tests.Audit;

[Collection("sql")]
public sealed class AuditListTests(SqlServerFixture fixture)
{
    private const string General = "101-6000-53100";

    [Fact]
    public async Task Submitted_invoice_evaluations_include_the_document_and_events_filter_by_action()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t.GetAsync(inv.Id)).Reference;

        var audit = t.Service<IAuditAppService>();
        var evaluations = await audit.ListEvaluationsAsync(EvaluationFilter.None, t.Clerk);
        var ofDocument = evaluations.Where(e => e.DocumentId == inv.Id && e.DocumentReference == reference).ToList();
        ofDocument.Should().NotBeEmpty();
        ofDocument.Should().OnlyContain(e => e.Trigger == "Submit" && e.ActorName == SpringfieldData.ClerkId.ToString());
        var row = ofDocument[0];

        var byDocument = await audit.ListEvaluationsAsync(new EvaluationFilter(Document: reference), t.Clerk);
        byDocument.Should().OnlyContain(e => e.DocumentReference == reference);
        (await audit.ListEvaluationsAsync(new EvaluationFilter(Trigger: "Submit"), t.Clerk)).Should().Contain(e => e.Id == row.Id);
        (await audit.ListEvaluationsAsync(new EvaluationFilter(Trigger: "Post"), t.Clerk)).Should().NotContain(e => e.Id == row.Id);

        var submitted = await audit.ListEventsAsync(new EventFilter(Action: "InvoiceSubmitted"), t.Clerk);
        submitted.Should().Contain(e => e.SubjectRef == reference && e.SubjectInvoiceId == inv.Id);
        (await audit.ListEventsAsync(new EventFilter(Action: "InvoicePosted"), t.Clerk)).Should().NotContain(e => e.SubjectRef == reference);
    }

    [Fact]
    public async Task Springfield_evaluation_actor_is_the_clerk_display_name_from_Master()
    {
        var t = fixture.Named("springfield");
        var inv = await t.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t.GetAsync(inv.Id)).Reference;

        var rows = await t.Service<IAuditAppService>().ListEvaluationsAsync(new EvaluationFilter(Document: reference), t.Clerk);
        rows.Should().NotBeEmpty();
        rows.Should().OnlyContain(e => e.ActorName == "AP Clerk" && e.DocumentId == inv.Id);
    }

    [Fact]
    public async Task A_second_tenant_does_not_see_the_first_tenants_evaluations_or_events()
    {
        var t1 = await fixture.CreateTenantAsync();
        var inv = await t1.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await t1.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await t1.GetAsync(inv.Id)).Reference;

        var t2 = await fixture.CreateTenantAsync();
        var audit = t2.Service<IAuditAppService>();
        (await audit.ListEvaluationsAsync(EvaluationFilter.None, t2.Clerk)).Should().NotContain(e => e.DocumentReference == reference);
        (await audit.ListEventsAsync(EventFilter.None, t2.Clerk)).Should().NotContain(e => e.SubjectRef == reference);
    }

    [Fact]
    public async Task Shelbyville_audit_lists_do_not_contain_springfield_documents()
    {
        var springfield = fixture.Named("springfield");
        var inv = await springfield.CreateAsync(10_000m, null, (General, 10_000m, null));
        (await springfield.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var reference = (await springfield.GetAsync(inv.Id)).Reference;

        var shelbyville = fixture.Named("shelbyville");
        var audit = shelbyville.Service<IAuditAppService>();
        (await audit.ListEvaluationsAsync(EvaluationFilter.None, shelbyville.Clerk)).Should().NotContain(e => e.DocumentReference == reference);
        (await audit.ListEventsAsync(EventFilter.None, shelbyville.Clerk)).Should().NotContain(e => e.SubjectRef == reference);
    }
}
