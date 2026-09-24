using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Explanation;
using GovErp.Application.Web.Identity;
using GovErp.Application.Web.Invoices;
using GovErp.Application.Web.Invoices.Commands;
using GovErp.Infrastructure.Seed;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Application.Web.Tests.Integration;

[Collection("sql")]
public sealed class PlatformTests(SqlServerFixture fixture)
{
    [Fact]
    public async Task TenantIsolation()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        var shelby = new ActorContext(new TenantId("shelbyville"), UserId.New(), "shelby.clerk", new HashSet<string> { Roles.ApClerk }, null);
        (await t.Service<IInvoiceAppService>().ListAsync(GovErp.Application.Web.Invoices.Contracts.InvoiceListFilter.None, shelby)).Should().BeEmpty();
        await FluentActions.Awaiting(() => t.Service<IInvoiceAppService>().GetAsync(inv.Id, shelby)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SignInResolvesTenantRolesAndDepartment()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<ISignIn>();
        var chief = await signIn.AuthenticateAsync("fire.chief", MasterSeed.DemoPassword);
        chief!.TenantId.Should().Be(new TenantId("springfield"));
        chief.DepartmentCode.Should().Be("6000");
        chief.IsInRole(Roles.DepartmentHead).Should().BeTrue();
        (await signIn.AuthenticateAsync("fire.chief", "wrong")).Should().BeNull();
    }

    [Fact]
    public async Task AppendOnlyViaEfAndDatabaseRights()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        (await t.Service<IInvoiceAppService>().ValidateAsync(new InvoiceActionCommand(TenantDriver.Env(), inv.Id), t.Clerk)).IsAccepted.Should().BeTrue();

        await FluentActions.Awaiting(() => t.WithDbAsync(async db =>
        {
            var record = await db.EvaluationRecords.FirstAsync();
            var flipped = record.Overall == Domain.Validation.ValueObjects.Severity.Allowed
                ? Domain.Validation.ValueObjects.Severity.HardStop
                : Domain.Validation.ValueObjects.Severity.Allowed;
            db.Entry(record).Property(r => r.Overall).CurrentValue = flipped;
            return await db.SaveChangesAsync();
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");

        await FluentActions.Awaiting(() => t.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("UPDATE audit.Events SET Action = N'x'")))
            .Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();   // DENY for the runtime user
    }

    [Fact]
    public async Task StaleFormGetsConflict()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        var stale = (await t.GetAsync(inv.Id)).RowVersion;
        (await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(TenantDriver.Env(stale), inv.Id, inv.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 20m, null,
            [new DistributionCommand("101-6000-53100", 20m, null)]), t.Clerk)).IsAccepted.Should().BeTrue();
        var second = await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(TenantDriver.Env(stale), inv.Id, inv.Number, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 30m, null,
            [new DistributionCommand("101-6000-53100", 30m, null)]), t.Clerk);
        second.Status.Should().Be(CommandStatus.Conflict);
        second.Retryable.Should().BeTrue();
    }

    [Fact]
    public async Task ExplanationTemplateNeverCallsChatClient()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, null, ("701-6000-53100-G-COPS-26", 160_000m, null));
        var validated = await t.Service<IInvoiceAppService>().ValidateAsync(new InvoiceActionCommand(TenantDriver.Env(), inv.Id), t.Clerk);
        var evaluationId = validated.Value!.LastEvaluation!.Id;
        var explained = await t.Service<IExplanationAppService>().ExplainAsync(evaluationId, ExplanationAudience.Auditor, TenantDriver.Env(), t.Clerk);
        explained.IsAccepted.Should().BeTrue();
        explained.Value!.Provider.Should().Be("Template");
        explained.Value.Text.Should().Contain("13,000.00");
        (await t.Service<IExplanationAppService>().ListAsync(evaluationId, t.Clerk)).Should().ContainSingle();
    }
}
