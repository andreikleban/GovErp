# План 3: Application + Infrastructure + Docker

> Перед реализацией прочитать [обязательные уточнения согласованности](2026-09-22-plan-consistency.md). Они исправляют даты, резервирование, транзакции и безопасность в ранних фрагментах ниже.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать слой сценариев (app-сервисы, сборщик снимков, порты аудита/тенантов/объяснений), слой внешнего мира (EF Core + SQL Server, схемы по контекстам, БД на тенанта, seed, аутентификация), точку сборки и Docker — так, чтобы сквозные сценарии (Submit с резервированием, Post одной транзакцией, гонка двух Submit, изоляция тенантов) проходили интеграционными тестами против настоящего SQL Server.

**Architecture:** `GovErp.Application.Web` оркестрирует четыре контекста по идентификаторам; `ValidationSubjectAssembler` — единственное место, где они встречаются. `GovErp.Infrastructure` реализует все порты: один `GovErpDbContext` со схемами `coa`, `ledger`, `ap`, `validation`, `audit`; `MasterDbContext` для тенантов и пользователей. Тенант резолвится при логине, `GovErpDbContext` получает connection string из `ITenantContext`. Post — одна транзакция (исключение GE-E1), Submit — резервирование под `rowversion` (GE-14).

**Tech Stack:** .NET 10, EF Core 10 (SqlServer, Design), `Microsoft.Extensions.Options`, `Microsoft.AspNetCore.Identity` только ради `PasswordHasher<T>`, Testcontainers.MsSql, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-21-validation-engine-design.md` (разделы 3.6–3.7, 4.4 в части Template, 5, 6.1–6.2, 7). Манифест: `GE-1..GE-9`, `GE-12`, `GE-14`, `GE-E1`, `GE-E4`.

**Зависит от:** планы 1 и 2.

## Global Constraints

- Всё из планов 1–2.
- Application ссылается на `Domain.*`; Infrastructure — на `Domain.*` и `Application.Web`; Web — на `Application.Web` и `Infrastructure` только в `Program.cs` и `Extensions/`.
- Наружу из Application — только `*Vm`, `*Command`, примитивы (`CA-10`, `NM-10`). Ни одного `Entities/`-типа в публичных сигнатурах app-сервисов.
- Все app-сервисы принимают `ActorContext` явно (`NM-16`); контекст из окружения — только в Web.
- Один `SaveChangesAsync` = одна транзакция. Только `IUnitOfWork` вызывает его; репозитории не сохраняют.
- `EvaluationRecord`, `AuditEvent`, `ExplanationRecord` — только INSERT (`GE-12`), проверяется interceptor'ом.
- Версии пакетов — в `Directory.Packages.props`: `Microsoft.EntityFrameworkCore.SqlServer` / `.Design` / `.Relational` **10.0.0** (или актуальная 10.0.x), `Microsoft.Extensions.Identity.Core` 10.0.0, `Microsoft.Extensions.Options.ConfigurationExtensions` 10.0.0, `Testcontainers.MsSql` 4.3.0, `Microsoft.Extensions.DependencyInjection` 10.0.0. Если версия не найдена — взять ближайшую 10.0.x, не менять major.
- Отклонение от спеки, фиксируемое планом: `AccountCode` хранится **одной** колонкой `FullCode` (`nvarchar(64)`) через `HasConversion`, а не четырьмя — одна колонка, один индекс, одна конверсия; сегменты восстанавливаются `AccountCode.Parse`.

---

## Структура файлов

```
src/GovErp.Application.Web/
  Common/ActorContext.cs, Roles.cs, AuthorizationException.cs, NotFoundException.cs, IClock.cs, RoleMapping.cs
  Persistence/IUnitOfWork.cs
  Tenancy/ITenantContext.cs, ITenantCatalog.cs, TenantInfo.cs
  Identity/ISignIn.cs
  Audit/IAuditTrail.cs, AuditEvent.cs, AuditEventVm.cs
  Explanation/IExplanationGenerator.cs, ExplanationAudience.cs, ExplanationResult.cs, ExplanationRecord.cs,
              IExplanationRepository.cs, ExplanationVm.cs, IExplanationAppService.cs, ExplanationAppService.cs
  Validation/PostingOptions.cs, ValidationSubjectAssembler.cs, EvaluationMapping.cs, Contracts/EvaluationVm.cs (+ вложенные Vm)
  Invoices/Contracts/InvoiceVm.cs, DistributionVm.cs, ApprovalVm.cs, OverrideVm.cs, ActionOutcomeVm.cs
           Commands/CreateInvoiceCommand.cs, UpdateInvoiceCommand.cs, DistributionCommand.cs, InvoicePreset.cs
           Mapping/InvoiceMapping.cs, IInvoiceAppService.cs, InvoiceAppService.cs
  Approvals/Contracts/ApprovalQueueItemVm.cs, IApprovalAppService.cs, ApprovalAppService.cs
  Posting/IPostingAppService.cs, PostingAppService.cs
  Budget/Contracts/BudgetLineVm.cs, Commands/AmendBudgetCommand.cs, IBudgetAppService.cs, BudgetAppService.cs
  Reference/Contracts/*.cs, IReferenceAppService.cs, ReferenceAppService.cs
  Extensions/ServiceCollectionExtensions.cs
src/GovErp.Infrastructure/
  Persistence/GovErpDbContext.cs, GovErpDbContextFactory.cs, Conversions.cs, JsonColumn.cs, AppendOnlyInterceptor.cs, EfUnitOfWork.cs
  Persistence/Configurations/{Coa,Ledger,Ap,Validation,Audit}/*.cs
  Persistence/Repositories/Ef*.cs
  Persistence/Migrations/  (сгенерированные)
  Master/MasterDbContext.cs, Tenant.cs, UserAccount.cs, MasterDbContextFactory.cs, Migrations/
  Tenancy/TenantContext.cs, MasterTenantCatalog.cs
  Identity/MasterSignIn.cs
  Audit/EfAuditTrail.cs
  Explanation/TemplateExplanationGenerator.cs, EfExplanationRepository.cs
  Seed/SeedRunner.cs, SpringfieldSeed.cs, ShelbyvilleSeed.cs, RuleSeed.cs, MasterSeed.cs
  Startup/DatabaseInitializer.cs
  Extensions/ServiceCollectionExtensions.cs
src/GovErp.Web/Program.cs, Extensions/ServiceCollectionExtensions.cs, appsettings.json, Dockerfile
docker-compose.yml, .env.example
tests/GovErp.Application.Web.Tests/  (Testcontainers)
```

---

### Task 1: Application — общие типы и порты

**Files:**
- Create: всё из `Common/`, `Persistence/`, `Tenancy/`, `Identity/`, `Audit/` (кроме Vm), `Explanation/IExplanationGenerator.cs`, `ExplanationAudience.cs`, `ExplanationResult.cs`, `ExplanationRecord.cs`, `IExplanationRepository.cs`, `Validation/PostingOptions.cs`
- Test: нет (типы без логики); компиляция.

**Interfaces (Produces):**

```csharp
// Common/ActorContext.cs
namespace GovErp.Application.Web.Common;
public sealed record ActorContext(TenantId TenantId, UserId UserId, string UserName, IReadOnlySet<string> Roles, string? DepartmentCode)
{
    public bool IsInRole(string role) => Roles.Contains(role);
    public void Require(params string[] anyOf)
    {
        if (!anyOf.Any(IsInRole)) throw new AuthorizationException($"{UserName} needs one of: {string.Join(", ", anyOf)}.");
    }
}

// Common/Roles.cs
public static class Roles
{
    public const string ApClerk = "ApClerk";
    public const string DepartmentHead = "DepartmentHead";
    public const string GrantsManager = "GrantsManager";
    public const string BudgetOfficer = "BudgetOfficer";
    public const string FinanceDirector = "FinanceDirector";
    public static readonly string[] Approvers = [DepartmentHead, GrantsManager, BudgetOfficer, FinanceDirector];
}

// Common/AuthorizationException.cs
public sealed class AuthorizationException(string message) : Exception(message);
// Common/NotFoundException.cs
public sealed class NotFoundException(string message) : Exception(message);
// Common/IClock.cs
public interface IClock { DateTimeOffset Now { get; } DateOnly Today => DateOnly.FromDateTime(Now.UtcDateTime); }

// Common/RoleMapping.cs — три перечисления ролей (Application string, Validation.ApproverRole, Payables.ApproverRole)
public static class RoleMapping
{
    public static Domain.Validation.ValueObjects.ApproverRole ToValidation(string role) => Enum.Parse<Domain.Validation.ValueObjects.ApproverRole>(role);
    public static Domain.Payables.Entities.ApproverRole ToPayables(string role) => Enum.Parse<Domain.Payables.Entities.ApproverRole>(role);
    public static Domain.Validation.ValueObjects.ApproverRole ToValidation(Domain.Payables.Entities.ApproverRole r) => Enum.Parse<Domain.Validation.ValueObjects.ApproverRole>(r.ToString());
    public static string ToRoleName(Domain.Validation.ValueObjects.ApproverRole r) => r.ToString();
}

// Persistence/IUnitOfWork.cs
public interface IUnitOfWork
{
    Task SaveChangesAsync(CancellationToken ct = default);
    /// <summary>Одна транзакция БД на несколько агрегатов — для атомарной финансовой команды (GE-E1).</summary>
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default);
}

// Tenancy/ITenantContext.cs
public interface ITenantContext { TenantId TenantId { get; } string ConnectionString { get; } bool IsInitialized { get; } }
public interface ITenantContextInitializer { void Initialize(TenantId tenantId, string connectionString); }
// Tenancy/TenantInfo.cs
public sealed record TenantInfo(TenantId Id, string Name, string ConnectionString);
// Tenancy/ITenantCatalog.cs
public interface ITenantCatalog { Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default); Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default); }

// Identity/ISignIn.cs
public interface ISignIn { Task<ActorContext?> AuthenticateAsync(string userName, string password, CancellationToken ct = default); }

// Audit/AuditEvent.cs
public sealed record AuditEvent(Guid Id, TenantId TenantId, DateTimeOffset OccurredAt, UserId Actor, string ActorName, string Action, string SubjectRef, string CorrelationId, string PayloadJson);
// Audit/IAuditTrail.cs
public interface IAuditTrail
{
    void Record(ActorContext actor, string action, string subjectRef, string correlationId, object payload);
    Task<IReadOnlyList<AuditEvent>> ListBySubjectAsync(string subjectRef, CancellationToken ct = default);
}

// Explanation/ExplanationAudience.cs
public enum ExplanationAudience { FinanceUser, DepartmentManager, Auditor, Public }
// Explanation/ExplanationResult.cs
public sealed record ExplanationResult(string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason);
// Explanation/IExplanationGenerator.cs
public interface IExplanationGenerator
{
    Task<ExplanationResult> ExplainAsync(Domain.Validation.Entities.EvaluationRecord record, ExplanationAudience audience, CancellationToken ct = default);
}
// Explanation/ExplanationRecord.cs — хранимая запись, не доменная сущность
public sealed record ExplanationRecord(Guid Id, Guid EvaluationId, string TransactionRef, ExplanationAudience Audience, string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason, DateTimeOffset GeneratedAt);
// Explanation/IExplanationRepository.cs
public interface IExplanationRepository { void Add(ExplanationRecord record); Task<IReadOnlyList<ExplanationRecord>> ListByEvaluationAsync(Guid evaluationId, CancellationToken ct = default); }

// Validation/PostingOptions.cs
public sealed class PostingOptions
{
    public string AccountsPayableObject { get; set; } = "2100";
    public string ReserveForEncumbrancesObject { get; set; } = "2900";
    public string EncumbrancesObject { get; set; } = "5900";
    public string BalanceSheetDepartment { get; set; } = "0000";
    public Domain.Validation.ValueObjects.PostingAccounts ToPostingAccounts() =>
        new(new ObjectCode(AccountsPayableObject), new ObjectCode(ReserveForEncumbrancesObject), new ObjectCode(EncumbrancesObject), new DepartmentCode(BalanceSheetDepartment));
}
```

- [ ] **Step 1:** Добавить в `Directory.Build.props` для проекта `GovErp.Application.Web` глобальный using `GovErp.Domain.Shared.ValueObjects` (расширить условие `ItemGroup` на `StartsWith('GovErp.Application.')` и `'GovErp.Infrastructure'`).
- [ ] **Step 2:** Создать все файлы из блока выше (по одному публичному типу на файл, пространство имён = папка).
- [ ] **Step 3:** `dotnet build src/GovErp.Application.Web` — Expected: успех.
- [ ] **Step 4:** Commit: `Application: actor context, ports for tenancy, audit, explanation, unit of work`.

---

### Task 2: ValidationSubjectAssembler

**Files:**
- Create: `Validation/ValidationSubjectAssembler.cs`
- Test: `tests/GovErp.Application.Web.Tests/` (проект, юнит-часть без БД), `Support/InMemoryRepositories.cs`, `ValidationSubjectAssemblerTests.cs`

**Interfaces:**
- Produces: `ValidationSubjectAssembler.BuildAsync(VendorInvoice invoice, CancellationToken) → ValidationSubject`. Конструктор принимает `IFundRepository, IGrantRepository, IAccountCombinationRepository, IReferenceDataRepository, IBudgetLineRepository, IEncumbranceRepository, IFiscalPeriodRepository, IVendorRepository, IVendorInvoiceRepository, IEvaluationRecordRepository, IOptions<PostingOptions>, IClock`.

- [ ] **Step 1: Создать тестовый проект**

```powershell
dotnet new xunit -n GovErp.Application.Web.Tests -o tests/GovErp.Application.Web.Tests
Remove-Item tests/GovErp.Application.Web.Tests/UnitTest1.cs
dotnet sln add tests/GovErp.Application.Web.Tests
foreach ($p in "Shared","ChartOfAccounts","Ledger","Payables","Validation") { dotnet add tests/GovErp.Application.Web.Tests reference "src/GovErp.Domain.$p" }
dotnet add tests/GovErp.Application.Web.Tests reference src/GovErp.Application.Web
dotnet add tests/GovErp.Application.Web.Tests reference src/GovErp.Infrastructure
```
csproj: CPM-вариант + `Microsoft.Extensions.Options`, `Microsoft.Extensions.DependencyInjection`, `Testcontainers.MsSql`, `Microsoft.EntityFrameworkCore.SqlServer`.

- [ ] **Step 2: Фейки репозиториев**

`Support/InMemoryRepositories.cs` — по одному классу на порт, над `List<T>`: `InMemoryFundRepository`, `InMemoryGrantRepository`, `InMemoryAccountCombinationRepository`, `InMemoryReferenceDataRepository`, `InMemoryBudgetLineRepository`, `InMemoryEncumbranceRepository`, `InMemoryFiscalPeriodRepository`, `InMemoryVendorRepository`, `InMemoryVendorInvoiceRepository`, `InMemoryEvaluationRecordRepository`. Каждый: конструктор `params T[] items`, методы порта — LINQ по списку, `AddAsync` — `Add`. `FixedClock(DateTimeOffset now) : IClock`.

Пример (остальные по образцу):
```csharp
public sealed class InMemoryBudgetLineRepository(params BudgetLine[] lines) : IBudgetLineRepository
{
    public List<BudgetLine> Items { get; } = [.. lines];
    public Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fy, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(l => l.Account == account && l.FiscalYear == fy));
    public Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fy, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BudgetLine>>(Items.Where(l => l.FiscalYear == fy).ToList());
    public Task AddAsync(BudgetLine line, CancellationToken ct = default) { Items.Add(line); return Task.CompletedTask; }
}
```

`Support/SpringfieldFixture.cs` — статический набор доменных объектов, повторяющий seed (фонды 101/202/501/701, департаменты, object-коды, грант `G-COPS-26`, комбинации, бюджетные строки из спеки 2.3, PO `PO-2026-0451`, vendor, период 2026-09 открыт / 2026-08 закрыт). Этот же набор план 3 задача 6 переиспользует для `SpringfieldSeed` — поэтому вынести его в `src/GovErp.Infrastructure/Seed/SpringfieldData.cs` (публичный статический класс, возвращающий доменные объекты) и в тестах ссылаться на него.

- [ ] **Step 3: Тесты**

```csharp
public class ValidationSubjectAssemblerTests
{
    private static ValidationSubjectAssembler Assembler(SpringfieldData data, VendorInvoice? existing = null, EvaluationRecord? lastEval = null) =>
        new(new InMemoryFundRepository(data.Funds), new InMemoryGrantRepository(data.Grants),
            new InMemoryAccountCombinationRepository(data.Combinations), new InMemoryReferenceDataRepository(data.Departments, data.Objects),
            new InMemoryBudgetLineRepository(data.BudgetLines), new InMemoryEncumbranceRepository(data.Encumbrances),
            new InMemoryFiscalPeriodRepository(data.Periods), new InMemoryVendorRepository(data.Vendors),
            new InMemoryVendorInvoiceRepository(existing is null ? [] : [existing]),
            new InMemoryEvaluationRecordRepository(lastEval is null ? [] : [lastEval]),
            Options.Create(new PostingOptions()), new FixedClock(new DateTimeOffset(2026, 9, 21, 0, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Exercise_invoice_yields_exercise_snapshot()
    {
        var data = SpringfieldData.Create();
        var inv = data.NonPoExerciseInvoice();
        var s = await Assembler(data).BuildAsync(inv);
        s.Transaction.Total.Should().Be(Money.Of(160_000m));
        s.Transaction.IsPoBacked.Should().BeFalse();
        var d = s.Distributions.Single();
        d.Fund!.Control.Should().Be(BudgetControl.Hard);
        d.Fund.GrantRule.Should().Be(GrantRule.Required);
        d.Grant!.Eligibility.Should().Be(GrantEligibilityResult.Eligible);
        d.Combination.IsActiveOnDate.Should().BeTrue();
        d.Budget.Available.Should().Be(Money.Of(147_000m));
        d.Encumbrance.Should().BeNull();
        s.PeriodIsOpen.Should().BeTrue();
        s.VersionsAtLastApproval.Should().BeNull();
    }

    [Fact]
    public async Task Po_backed_invoice_carries_encumbrance_snapshot()
    {
        var data = SpringfieldData.Create();
        var s = await Assembler(data).BuildAsync(data.PoBackedInvoice());
        s.Distributions.Single().Encumbrance.Should().BeEquivalentTo(new { PoLineRef = "PO-2026-0451/1", Remaining = Money.Of(160_000m), IsOpen = true });
    }

    [Fact]
    public async Task Missing_combination_and_budget_are_reported_not_thrown()
    {
        var data = SpringfieldData.Create();
        var inv = new VendorInvoice("X-1", data.Vendors[0].Id, new DateOnly(2026, 9, 15), Money.Of(10m), null, data.ClerkId, DateTimeOffset.UtcNow);
        inv.AddDistribution(AccountCode.Parse("101-3000-54000"), Money.Of(10m), null);
        var d = (await Assembler(data).BuildAsync(inv)).Distributions.Single();
        d.Combination.Exists.Should().BeFalse();
        d.Budget.Exists.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_and_closed_period_are_detected()
    {
        var data = SpringfieldData.Create();
        var first = data.NonPoExerciseInvoice();
        var dup = data.NonPoExerciseInvoice();   // тот же vendor + номер
        var s = await Assembler(data, existing: first).BuildAsync(dup);
        s.Transaction.IsDuplicate.Should().BeTrue();

        var aug = data.NonPoExerciseInvoice(date: new DateOnly(2026, 8, 15));
        (await Assembler(data).BuildAsync(aug)).PeriodIsOpen.Should().BeFalse();
    }
}
```

`SpringfieldData` должен предоставлять: `Funds`, `Departments`, `Objects`, `Grants`, `Combinations`, `BudgetLines`, `Encumbrances`, `Periods`, `Vendors`, `PurchaseOrders`, `ClerkId`, `NonPoExerciseInvoice(DateOnly? date = null)`, `PoBackedInvoice()`, `MultiFundInvoice()`, `Rules` (список `RuleDefinition` = `DemoRules.All()` из плана 2, продублированный здесь, потому что тестовый проект Validation не референсится).

- [ ] **Step 4: Реализация**

```csharp
namespace GovErp.Application.Web.Validation;

public sealed class ValidationSubjectAssembler(
    IFundRepository funds, IGrantRepository grants, IAccountCombinationRepository combinations, IReferenceDataRepository reference,
    IBudgetLineRepository budgetLines, IEncumbranceRepository encumbrances, IFiscalPeriodRepository periods,
    IVendorRepository vendors, IVendorInvoiceRepository invoices, IEvaluationRecordRepository evaluations,
    IOptions<PostingOptions> posting, IClock clock)
{
    public async Task<ValidationSubject> BuildAsync(VendorInvoice invoice, CancellationToken ct = default)
    {
        var vendor = await vendors.FindAsync(invoice.VendorId, ct) ?? throw new NotFoundException($"Vendor {invoice.VendorId} not found.");
        var isDuplicate = await invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.Number, invoice.Id, ct);
        var fy = FiscalYear.FromDate(invoice.InvoiceDate);
        var (py, pm) = FiscalPeriod.KeyFor(invoice.InvoiceDate);
        var period = await periods.FindAsync(py, pm, ct);

        var tx = new TransactionSnapshot(invoice.Reference, invoice.Version, "AP_INVOICE", invoice.InvoiceDate, invoice.Total,
            new VendorSnapshot(vendor.Id, vendor.Name, vendor.Status == VendorStatus.Debarred, vendor.SamRegistered),
            invoice.IsPoBacked, isDuplicate, invoice.CreatedBy);

        var distributions = new List<DistributionSnapshot>();
        foreach (var d in invoice.Distributions)
        {
            distributions.Add(await SnapshotAsync(d, invoice.InvoiceDate, fy, ct));
        }

        var approvals = invoice.Approvals.Where(a => a.Decision == ApprovalDecision.Approved).Select(a => RoleMapping.ToValidation(a.Role)).ToList();
        var overrides = invoice.Overrides.Select(o => new OverrideSnapshot(o.RuleId, o.UserId, o.Reason)).ToList();

        RuleSetVersions? versionsAtApproval = null;
        var lastApproval = invoice.Approvals.LastOrDefault(a => a.Decision == ApprovalDecision.Approved && a.EvaluationRef is not null);
        if (lastApproval is not null)
        {
            versionsAtApproval = (await evaluations.FindAsync(lastApproval.EvaluationRef!.Value, ct))?.RuleSetVersions;
        }

        return new ValidationSubject(tx, distributions, approvals, overrides, period?.IsOpen ?? false, versionsAtApproval, posting.Value.ToPostingAccounts());
    }

    private async Task<DistributionSnapshot> SnapshotAsync(InvoiceDistribution d, DateOnly date, FiscalYear fy, CancellationToken ct)
    {
        var fund = await funds.FindAsync(d.Account.Fund, ct);
        var combination = await combinations.FindAsync(d.Account, ct);
        var budget = await budgetLines.FindAsync(d.Account, fy, ct);
        var grant = d.Account.Grant is null ? null : await grants.FindAsync(d.Account.Grant, ct);
        var encumbrance = d.PoLineRef is null ? null : await encumbrances.FindByPoLineAsync(d.PoLineRef, ct);

        FundSnapshot? fundSnapshot = fund is null ? null : new(fund.Code.Value, fund.Name,
            fund.Type == FundType.Enterprise ? FundKind.Enterprise : FundKind.Governmental,
            fund.ControlMode == Domain.ChartOfAccounts.Entities.BudgetControlMode.Hard ? BudgetControl.Hard : BudgetControl.Soft,
            Enum.Parse<GrantRule>(fund.GrantPolicy.ToString()),
            Enum.Parse<FundRestriction>(fund.Check(d.Account.Department, d.Account.Object).ToString()), fund.IsActive);

        GrantSnapshot? grantSnapshot = d.Account.Grant is null ? null
            : grant is null ? new(d.Account.Grant.Value, false, GrantEligibilityResult.GrantNotActive, "Missing")
            : new(grant.Code.Value, grant.IsFederal, Enum.Parse<GrantEligibilityResult>(grant.CheckEligibility(date, d.Account.Department, d.Account.Object).ToString()), grant.Status.ToString());

        var combinationSnapshot = combination is null ? new CombinationSnapshot(false, false, "Missing")
            : new CombinationSnapshot(true, combination.IsActiveOn(date), combination.Status.ToString());

        var budgetSnapshot = budget is null ? BudgetSnapshot.Missing
            : new BudgetSnapshot(true, budget.Amended, budget.Actuals, budget.Encumbered, budget.Held, budget.Available);

        EncumbranceSnapshot? encSnapshot = d.PoLineRef is null ? null
            : encumbrance is null ? new(d.PoLineRef, Money.Zero, false)
            : new(encumbrance.PoLineRef, encumbrance.Remaining, encumbrance.Status == EncumbranceStatus.Open);

        return new DistributionSnapshot(d.LineNo, d.Account, d.Amount, combinationSnapshot, fundSnapshot, grantSnapshot, budgetSnapshot, encSnapshot);
    }
}
```

Перечисления `FundRestrictionCheck`/`GrantEligibility`/`GrantPolicy` (ChartOfAccounts) и `FundRestriction`/`GrantEligibilityResult`/`GrantRule` (Validation) имеют одинаковые имена членов — `Enum.Parse` по строке и есть маппинг; добавить тест `RoleAndEnumMappingTests`, который перебирает `Enum.GetNames` обеих сторон и проверяет равенство множеств — чтобы расхождение ловилось сборкой тестов, а не в рантайме.

- [ ] **Step 5:** `dotnet test tests/GovErp.Application.Web.Tests --filter "FullyQualifiedName~Assembler|FullyQualifiedName~Mapping"` — Expected: зелёные.
- [ ] **Step 6:** Commit: `Application: ValidationSubjectAssembler with in-memory tests`.

---

### Task 3: Infrastructure — DbContext, конфигурации, миграции, Master

**Files:**
- Create: `Persistence/GovErpDbContext.cs`, `GovErpDbContextFactory.cs`, `Conversions.cs`, `JsonColumn.cs`, `Persistence/Configurations/**/*.cs`, `Master/MasterDbContext.cs`, `Master/Tenant.cs`, `Master/UserAccount.cs`, `Master/MasterDbContextFactory.cs`
- Modify: `src/GovErp.Infrastructure/GovErp.Infrastructure.csproj` — пакеты EF; `Directory.Packages.props`
- Modify: `src/GovErp.Domain.Ledger/Entities/BudgetLine.cs` — добавить `ChangeStamp` (см. шаг 3)
- Generate: `Persistence/Migrations/*Initial*`, `Master/Migrations/*Initial*`

**Interfaces:**
- Produces: `GovErpDbContext` с `DbSet` для `Fund, Department, ObjectCodeDefinition, Grant, AccountCombination, BudgetLine, Encumbrance, JournalEntry, FiscalPeriod, Vendor, PurchaseOrder, VendorInvoice, RuleDefinition, EvaluationRecord, ExplanationRecord, AuditEvent`; `MasterDbContext` с `Tenants`, `Users`.

- [ ] **Step 1: Пакеты**

В `Directory.Packages.props` добавить `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.Identity.Core`, `Microsoft.Extensions.Options.ConfigurationExtensions` (10.0.x), `Testcontainers.MsSql` 4.3.0. В `GovErp.Infrastructure.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design"><PrivateAssets>all</PrivateAssets></PackageReference>
  <PackageReference Include="Microsoft.Extensions.Identity.Core" />
  <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
</ItemGroup>
```
Установить инструмент: `dotnet tool install --global dotnet-ef` (или `dotnet tool update --global dotnet-ef`).

- [ ] **Step 2: Конверсии и JSON-колонки**

`Persistence/Conversions.cs`:
```csharp
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GovErp.Infrastructure.Persistence;

internal static class Conversions
{
    public static readonly ValueConverter<Money, decimal> Money = new(m => m.Amount, d => new Money(d));
    public static readonly ValueConverter<FundCode, string> Fund = new(c => c.Value, s => new FundCode(s));
    public static readonly ValueConverter<DepartmentCode, string> Department = new(c => c.Value, s => new DepartmentCode(s));
    public static readonly ValueConverter<ObjectCode, string> Object = new(c => c.Value, s => new ObjectCode(s));
    public static readonly ValueConverter<GrantCode, string> Grant = new(c => c.Value, s => new GrantCode(s));
    public static readonly ValueConverter<AccountCode, string> Account = new(c => c.ToString(), s => AccountCode.Parse(s));
    public static readonly ValueConverter<FiscalYear, int> FiscalYear = new(f => f.Year, y => new FiscalYear(y));
    public static readonly ValueConverter<UserId, Guid> User = new(u => u.Value, g => new UserId(g));
    public static readonly ValueConverter<TenantId, string> Tenant = new(t => t.Value, s => new TenantId(s));
}
```

`Persistence/JsonColumn.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GovErp.Infrastructure.Persistence;

internal static class JsonColumn
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() },
    };

    public static PropertyBuilder<T> AsJson<T>(this PropertyBuilder<T> builder) =>
        builder.HasConversion(
                new ValueConverter<T, string>(v => JsonSerializer.Serialize(v, Options), s => JsonSerializer.Deserialize<T>(s, Options)!),
                new ValueComparer<T>(
                    (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                    v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!))
            .HasColumnType("nvarchar(max)");
}
```

Тест `JsonRoundTripTests` в `Application.Web.Tests`: `ValidationSubject` из `SpringfieldData` → JSON → обратно → `Should().BeEquivalentTo`. Если наследники `SegmentCode` не десериализуются (STJ сопоставляет параметр `value` со свойством `Value` — должно работать), добавить `JsonConverter<FundCode>` и т.д. в `Options`.

- [ ] **Step 3: ChangeStamp в BudgetLine**

`rowversion` меняется только при UPDATE строки владельца; добавление строки в owned-таблицу `BudgetReservations` владельца не трогает. Поэтому в `BudgetLine` добавить:
```csharp
/// <summary>Растёт при каждом изменении; гарантирует UPDATE строки владельца и срабатывание rowversion.</summary>
public int ChangeStamp { get; private set; }
```
и `ChangeStamp++` в `Amend`, `Reserve` (только при успехе), `Commit`, `Release`, `RecordEncumbrance`, `RecordLiquidation`, `RecordActuals`. Тест в `Domain.Ledger.Tests`: `Reserve_and_commit_bump_change_stamp` — после `Reserve` = 1, после `Commit` = 2; отказ `Reserve` в Hard-фонде не меняет.

- [ ] **Step 4: DbContext и конфигурации**

`Persistence/GovErpDbContext.cs`:
```csharp
public sealed class GovErpDbContext(DbContextOptions<GovErpDbContext> options) : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ObjectCodeDefinition> ObjectCodes => Set<ObjectCodeDefinition>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<AccountCombination> AccountCombinations => Set<AccountCombination>();
    public DbSet<BudgetLine> BudgetLines => Set<BudgetLine>();
    public DbSet<Encumbrance> Encumbrances => Set<Encumbrance>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<Vendor> Vendors => Set<Vendor>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<VendorInvoice> VendorInvoices => Set<VendorInvoice>();
    public DbSet<RuleDefinition> RuleDefinitions => Set<RuleDefinition>();
    public DbSet<EvaluationRecord> EvaluationRecords => Set<EvaluationRecord>();
    public DbSet<ExplanationRecord> Explanations => Set<ExplanationRecord>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.ApplyConfigurationsFromAssembly(typeof(GovErpDbContext).Assembly,
            t => t.Namespace!.StartsWith("GovErp.Infrastructure.Persistence.Configurations"));
}
```

Конфигурации — `IEntityTypeConfiguration<T>`, по файлу на тип, в подпапках `Coa/`, `Ledger/`, `Ap/`, `Validation/`, `Audit/`. Общие приёмы: `ToTable("X", "schema")`; перечисления — `HasConversion<string>()`; `Money` — `HasConversion(Conversions.Money).HasPrecision(18, 2)`; коллекции с приватным полем — `b.Navigation(x => x.Prop).HasField("_field").UsePropertyAccessMode(PropertyAccessMode.Field)`.

Образец `Configurations/Coa/FundConfiguration.cs`:
```csharp
public sealed class FundConfiguration : IEntityTypeConfiguration<Fund>
{
    public void Configure(EntityTypeBuilder<Fund> b)
    {
        b.ToTable("Funds", "coa");
        b.HasKey(x => x.Code);
        b.Property(x => x.Code).HasConversion(Conversions.Fund).HasMaxLength(3);
        b.Property(x => x.Name).HasMaxLength(200);
        b.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.Basis).HasConversion<string>().HasMaxLength(20);
        b.Property(x => x.ControlMode).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.GrantPolicy).HasConversion<string>().HasMaxLength(10);
        b.Property(x => x.AllowedDepartments).HasConversion(
            v => string.Join(',', v.Select(c => c.Value)),
            s => s.Length == 0 ? new List<DepartmentCode>() : s.Split(',', StringSplitOptions.None).Select(x => new DepartmentCode(x)).ToList(),
            new ValueComparer<IReadOnlyList<DepartmentCode>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()))
            .HasMaxLength(500);
        b.Property(x => x.AllowedObjects).HasConversion(
            v => string.Join(',', v.Select(c => c.Value)),
            s => s.Length == 0 ? new List<ObjectCode>() : s.Split(',', StringSplitOptions.None).Select(x => new ObjectCode(x)).ToList(),
            new ValueComparer<IReadOnlyList<ObjectCode>>((a, c) => a!.SequenceEqual(c!), v => v.Count, v => v.ToList()))
            .HasMaxLength(500);
    }
}
```

Остальные конфигурации — по этому образцу:

| Тип | Таблица | Ключ | Особенности |
|---|---|---|---|
| `Department` | `coa.Departments` | `Code` | конверсия `Conversions.Department` |
| `ObjectCodeDefinition` | `coa.ObjectCodes` | `Code` | `Category` string |
| `Grant` | `coa.Grants` | `Code` | `OwnsOne(Period)` → колонки `PeriodFrom`, `PeriodTo`; списки как у `Fund`; `Status` string |
| `AccountCombination` | `coa.AccountCombinations` | `Id` | `Code` конверсия `Account`, `HasMaxLength(64)`, уникальный индекс; `CreatedBy`/`ApprovedBy` конверсия `User` |
| `BudgetLine` | `ledger.BudgetLines` | `Id` | см. ниже |
| `Encumbrance` | `ledger.Encumbrances` | `Id` | уникальный индекс `PoLineRef`; `Liquidations` — `OwnsMany(...).ToJson()` + `HasField("_liquidations")`; `Ignore(Remaining)`; `Property<byte[]>("RowVersion").IsRowVersion()` |
| `JournalEntry` | `ledger.JournalEntries` | `Id` | `Lines` — `OwnsMany` в `ledger.JournalLines` (`Account` конверсия, `Family` string, `Debit`/`Credit` decimal); `HasField("_lines")`; `PostedBy` конверсия; индекс `SourceRef` |
| `FiscalPeriod` | `ledger.FiscalPeriods` | `(Year, Month)` | `Status` string; `Ignore(IsOpen)` |
| `Vendor` | `ap.Vendors` | `Id` | `Status` string |
| `PurchaseOrder` | `ap.PurchaseOrders` | `Id` | уникальный индекс `Number`; `Lines` — `OwnsMany` в `ap.PurchaseOrderLines`; `Ignore(Total)` |
| `VendorInvoice` | `ap.VendorInvoices` | `Id` | см. ниже |
| `RuleDefinition` | `validation.RuleDefinitions` | `Id` | `Parameters`, `OverridableBy` — `AsJson()`; `Step`/`Layer`/`Severity` string; уникальный индекс `(RuleId, Layer, Version)` |
| `EvaluationRecord` | `validation.EvaluationRecords` | `Id` | `RuleSetVersions`, `Outcomes`, `Capabilities`, `ApprovalRoute`, `PostingPreview`, `PostingCheck`, `InputSnapshot` — `AsJson()`; `Trigger`/`Overall` string; `EvaluatedBy` конверсия; индекс `TransactionRef` |
| `ExplanationRecord` | `validation.Explanations` | `Id` | `Audience` string; индекс `EvaluationId` |
| `AuditEvent` | `audit.Events` | `Id` | `TenantId`, `Actor` конверсии; `PayloadJson` nvarchar(max); индекс `SubjectRef` |

`BudgetLineConfiguration`:
```csharp
b.ToTable("BudgetLines", "ledger");
b.HasKey(x => x.Id);
b.Property(x => x.Account).HasConversion(Conversions.Account).HasMaxLength(64);
b.Property(x => x.FiscalYear).HasConversion(Conversions.FiscalYear);
b.HasIndex(x => new { x.Account, x.FiscalYear }).IsUnique();
b.Property(x => x.ControlMode).HasConversion<string>().HasMaxLength(10);
b.Property(x => x.Adopted).HasConversion(Conversions.Money).HasPrecision(18, 2);
b.Property(x => x.Actuals).HasConversion(Conversions.Money).HasPrecision(18, 2);
b.Property(x => x.Encumbered).HasConversion(Conversions.Money).HasPrecision(18, 2);
b.Ignore(x => x.Amended).Ignore(x => x.Held).Ignore(x => x.Available);
b.Property<byte[]>("RowVersion").IsRowVersion();
b.OwnsMany(x => x.Amendments, o =>
{
    o.ToTable("BudgetAmendments", "ledger");
    o.WithOwner().HasForeignKey("BudgetLineId");
    o.Property<int>("Id").ValueGeneratedOnAdd();
    o.HasKey("Id");
    o.Property(a => a.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
    o.Property(a => a.Reference).HasMaxLength(100);
});
b.Navigation(x => x.Amendments).HasField("_amendments").UsePropertyAccessMode(PropertyAccessMode.Field);
b.OwnsMany(x => x.Reservations, o =>
{
    o.ToTable("BudgetReservations", "ledger");
    o.WithOwner().HasForeignKey("BudgetLineId");
    o.HasKey(r => r.Id);
    o.Property(r => r.SourceRef).HasMaxLength(100);
    o.HasIndex(r => r.SourceRef);
    o.Property(r => r.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
    o.Property(r => r.Status).HasConversion<string>().HasMaxLength(10);
});
b.Navigation(x => x.Reservations).HasField("_reservations").UsePropertyAccessMode(PropertyAccessMode.Field);
```

`VendorInvoiceConfiguration`: `Distributions` → owned таблица `ap.InvoiceDistributions` (ключ shadow `Id`, `Account` конверсия, `Amount` decimal, `PoLineRef` nullable), `Approvals` → `ap.InvoiceApprovals` (`Role`/`Decision` string, `UserId` конверсия), `Overrides` → `ap.InvoiceOverrides`; `ReservationRefs` — `b.Property(x => x.ReservationRefs).HasField("_reservationRefs").AsJson()`; `Status` string; `CreatedBy` конверсия; `RowVersion`; индекс `(VendorId, Number)`; `Ignore(Reference, IsPoBacked, DistributedTotal)`; `Total` decimal.

- [ ] **Step 5: Master**

```csharp
namespace GovErp.Infrastructure.Master;

public sealed class Tenant { public string Id { get; set; } = ""; public string Name { get; set; } = ""; public string DatabaseName { get; set; } = ""; }

public sealed class UserAccount
{
    public Guid Id { get; set; }
    public string UserName { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string TenantId { get; set; } = "";
    public List<string> Roles { get; set; } = [];
    public string? DepartmentCode { get; set; }
}

public sealed class MasterDbContext(DbContextOptions<MasterDbContext> options) : DbContext(options)
{
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<UserAccount> Users => Set<UserAccount>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Tenant>(t => { t.ToTable("Tenants"); t.HasKey(x => x.Id); t.Property(x => x.Id).HasMaxLength(50); });
        b.Entity<UserAccount>(u =>
        {
            u.ToTable("Users");
            u.HasKey(x => x.Id);
            u.HasIndex(x => x.UserName).IsUnique();
            u.Property(x => x.Roles).AsJson();
        });
    }
}
```
Анемичные классы с публичными сеттерами допустимы: это инфраструктурная модель, не домен (`DDD-7`).

Фабрики для `dotnet ef` — `GovErpDbContextFactory : IDesignTimeDbContextFactory<GovErpDbContext>` и `MasterDbContextFactory`, обе с `UseSqlServer("Server=localhost;Database=GovErp_Design;Trusted_Connection=True;TrustServerCertificate=True")`.

- [ ] **Step 6: Миграции**

```powershell
dotnet ef migrations add Initial --project src/GovErp.Infrastructure --startup-project src/GovErp.Infrastructure --context GovErpDbContext --output-dir Persistence/Migrations
dotnet ef migrations add Initial --project src/GovErp.Infrastructure --startup-project src/GovErp.Infrastructure --context MasterDbContext --output-dir Master/Migrations
```
Проверить в сгенерированной миграции `GovErpDbContext`: пять `EnsureSchema`; `rowversion` на `BudgetLines`, `Encumbrances`, `VendorInvoices`; ни одного FK между схемами.

- [ ] **Step 7:** `dotnet build && dotnet test tests/GovErp.Domain.Ledger.Tests` — успех. Commit: `Infrastructure: EF model with schema per context, master context, initial migrations`.

---

### Task 4: Infrastructure — репозитории, UoW, append-only, аудит, tenancy, sign-in, Template-объяснение

**Files:**
- Create: `Persistence/Repositories/Ef*Repository.cs` (13 файлов — по одному на порт), `Persistence/EfUnitOfWork.cs`, `Persistence/AppendOnlyInterceptor.cs`, `Audit/EfAuditTrail.cs`, `Explanation/EfExplanationRepository.cs`, `Explanation/TemplateExplanationGenerator.cs`, `Tenancy/TenantContext.cs`, `Tenancy/TenancyOptions.cs`, `Tenancy/MasterTenantCatalog.cs`, `Identity/MasterSignIn.cs`, `Common/SystemClock.cs`, `Extensions/ServiceCollectionExtensions.cs`

**Interfaces:**
- Produces: `services.AddInfrastructure(IConfiguration)`; `GovErpDbContext` scoped с connection string из `ITenantContext`; `MasterDbContext` из `ConnectionStrings:Master`; `TenancyOptions.TenantConnectionTemplate` (`"Server=...;Database={0};..."`).

- [ ] **Step 1: Репозитории**

Образец:
```csharp
public sealed class EfBudgetLineRepository(GovErpDbContext db) : IBudgetLineRepository
{
    public Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fy, CancellationToken ct = default) =>
        db.BudgetLines.Include(l => l.Amendments).Include(l => l.Reservations)
          .SingleOrDefaultAsync(l => l.Account == account && l.FiscalYear == fy, ct);

    public async Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fy, CancellationToken ct = default) =>
        await db.BudgetLines.Include(l => l.Amendments).Include(l => l.Reservations)
            .Where(l => l.FiscalYear == fy).OrderBy(l => l.Account).ToListAsync(ct);

    public async Task AddAsync(BudgetLine line, CancellationToken ct = default) => await db.BudgetLines.AddAsync(line, ct);
}
```
`EfVendorInvoiceRepository.ExistsDuplicateAsync`: `AnyAsync(i => i.VendorId == vendorId && i.NormalizedInvoiceNumber == normalizedNumber && i.Id != excludingInvoiceId)`; `FindAsync` и `ListAsync` — с `Include` всех owned-таблиц. `EfEvaluationRecordRepository.ListByTransactionAsync` — сортировка по `EvaluatedAt`. Остальные — тривиальны.

- [ ] **Step 2: UoW и append-only**

```csharp
public sealed class EfUnitOfWork(GovErpDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);

    public async Task ExecuteInTransactionAsync(Func<CancellationToken, Task> work, CancellationToken ct = default)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await work(ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }
}

public sealed class AppendOnlyInterceptor : SaveChangesInterceptor
{
    private static readonly Type[] AppendOnly = [typeof(EvaluationRecord), typeof(AuditEvent), typeof(ExplanationRecord)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r) { Check(e.Context!); return r; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default) { Check(e.Context!); return ValueTask.FromResult(r); }

    private static void Check(DbContext db)
    {
        var offender = db.ChangeTracker.Entries()
            .FirstOrDefault(x => AppendOnly.Contains(x.Entity.GetType()) && x.State is EntityState.Modified or EntityState.Deleted);
        if (offender is not null)
        {
            throw new InvalidOperationException($"{offender.Entity.GetType().Name} is append-only (GE-12).");
        }
    }
}
```

- [ ] **Step 3: Аудит, объяснения, часы**

`EfAuditTrail(GovErpDbContext db, ITenantContext tenant, IClock clock)`: `Record` → `db.AuditEvents.Add(new AuditEvent(Guid.NewGuid(), tenant.TenantId, clock.Now, actor.UserId, actor.UserName, action, subjectRef, correlationId, JsonSerializer.Serialize(payload, JsonColumn.Options)))`; `ListBySubjectAsync` — по `SubjectRef`, сортировка по `OccurredAt`. `EfExplanationRepository` — `Add` / `ListByEvaluationAsync`. `SystemClock : IClock` → `DateTimeOffset.UtcNow`.

`TemplateExplanationGenerator : IExplanationGenerator` — детерминированный текст из `EvaluationRecord`, `Provider = "Template"`, `PromptVersion = "template-1"`. Структура текста:
```
Invoice INV-V-7781 for 160,000.00 from Acme Consulting was evaluated on 2026-09-21 (trigger: Manual). Result: HARD STOP.
Funds charged: line 1 — 160,000.00 to 701-6000-53100-G-COPS-26 (Grants Fund, hard budget control).
Checks performed: SEG_REQUIRED, SEG_GRANT_FORBIDDEN, COA_COMBINATION_ACTIVE, ... (steps 1–6 executed until the first hard stop).
Findings: BUDGET_AVAILABILITY [HardStop] — <Message>. Required action: <Resolution>.
Approvals required: DepartmentHead (6000), GrantsManager, FinanceDirector. Recorded: none.
Accounting entries to be created: none until the stop is resolved. | Dr 701-6000-53100-G-COPS-26 160,000.00 / Cr 701-0000-2100 160,000.00
Rule versions applied: engine-1.0.0 / core 1 / federal 1 / state 1 / tenant 1.
```
Три варианта по аудитории: `Public` — без имён пользователей, без `Inputs`; `Auditor` — с версиями, всеми `Inputs`/`Computed` и overrides; `FinanceUser`/`DepartmentManager` — без сырых inputs, с сообщениями и resolution. Список пройденных правил берётся из `record.InputSnapshot` + `record.Outcomes`: правило «пройдено», если для его шага ≤ последнего выполненного шага нет outcome с этим `RuleId`. Последний выполненный шаг = `max(Outcomes.Step)` или 6, если outcomes нет. Реализовать `StringBuilder`-ом, без внешних библиотек.

- [ ] **Step 4: Tenancy и sign-in**

```csharp
public sealed class TenancyOptions { public string TenantConnectionTemplate { get; set; } = ""; }

public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    private TenantId? _tenantId;
    private string? _connectionString;

    public TenantId TenantId => _tenantId ?? throw new InvalidOperationException("Tenant context is not initialized.");
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Tenant context is not initialized.");
    public bool IsInitialized => _tenantId is not null;

    public void Initialize(TenantId tenantId, string connectionString)
    {
        _tenantId = tenantId;
        _connectionString = connectionString;
    }
}

public sealed class MasterTenantCatalog(MasterDbContext master, IOptions<TenancyOptions> options) : ITenantCatalog
{
    public async Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default)
    {
        var t = await master.Tenants.FindAsync([id.Value], ct);
        return t is null ? null : ToInfo(t);
    }

    public async Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default) =>
        (await master.Tenants.OrderBy(t => t.Name).ToListAsync(ct)).Select(ToInfo).ToList();

    private TenantInfo ToInfo(Tenant t) =>
        new(new TenantId(t.Id), t.Name, string.Format(options.Value.TenantConnectionTemplate, t.DatabaseName));
}

public sealed class MasterSignIn(MasterDbContext master, IPasswordHasher<UserAccount> hasher) : ISignIn
{
    public async Task<ActorContext?> AuthenticateAsync(string userName, string password, CancellationToken ct = default)
    {
        var u = await master.Users.SingleOrDefaultAsync(x => x.UserName == userName, ct);
        if (u is null || hasher.VerifyHashedPassword(u, u.PasswordHash, password) == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return new ActorContext(new TenantId(u.TenantId), new UserId(u.Id), u.UserName, u.Roles.ToHashSet(), u.DepartmentCode);
    }
}
```

- [ ] **Step 5: Регистрация**

`Extensions/ServiceCollectionExtensions.cs`:
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
{
    s.Configure<TenancyOptions>(cfg.GetSection("Tenancy"));
    s.AddDbContext<MasterDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Master")));
    s.AddScoped<TenantContext>();
    s.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
    s.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());
    s.AddSingleton<AppendOnlyInterceptor>();
    s.AddDbContext<GovErpDbContext>((sp, o) => o
        .UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString, x => x.EnableRetryOnFailure())
        .AddInterceptors(sp.GetRequiredService<AppendOnlyInterceptor>()));
    s.AddScoped<IUnitOfWork, EfUnitOfWork>();
    s.AddScoped<IFundRepository, EfFundRepository>();
    s.AddScoped<IGrantRepository, EfGrantRepository>();
    s.AddScoped<IAccountCombinationRepository, EfAccountCombinationRepository>();
    s.AddScoped<IReferenceDataRepository, EfReferenceDataRepository>();
    s.AddScoped<IBudgetLineRepository, EfBudgetLineRepository>();
    s.AddScoped<IEncumbranceRepository, EfEncumbranceRepository>();
    s.AddScoped<IJournalRepository, EfJournalRepository>();
    s.AddScoped<IFiscalPeriodRepository, EfFiscalPeriodRepository>();
    s.AddScoped<IVendorRepository, EfVendorRepository>();
    s.AddScoped<IPurchaseOrderRepository, EfPurchaseOrderRepository>();
    s.AddScoped<IVendorInvoiceRepository, EfVendorInvoiceRepository>();
    s.AddScoped<IRuleDefinitionRepository, EfRuleDefinitionRepository>();
    s.AddScoped<IEvaluationRecordRepository, EfEvaluationRecordRepository>();
    s.AddScoped<IAuditTrail, EfAuditTrail>();
    s.AddScoped<IExplanationRepository, EfExplanationRepository>();
    s.AddScoped<ITenantCatalog, MasterTenantCatalog>();
    s.AddScoped<ISignIn, MasterSignIn>();
    s.AddSingleton<IPasswordHasher<UserAccount>, PasswordHasher<UserAccount>>();
    s.AddSingleton<IClock, SystemClock>();
    s.AddScoped<IExplanationGenerator, TemplateExplanationGenerator>();   // план 4 заменит выбором по конфигурации
    return s;
}
```

- [ ] **Step 6:** `dotnet build` — успех. Commit: `Infrastructure: repositories, unit of work, append-only interceptor, audit, tenancy, sign-in, template explanation`.

---

### Task 5: Application — контракты и app-сервисы

**Files:**
- Create: `Validation/Contracts/EvaluationVm.cs` (+ `OutcomeVm`, `RouteStepVm`, `PreviewLineVm`, `CapabilitiesVm`, `PostingCheckVm`, `RuleSetVersionsVm` — в одном файле как вложенные records допустимо, это один контракт), `Validation/EvaluationMapping.cs`, `Validation/EvaluationRunner.cs`
- Create: `Invoices/**`, `Approvals/**`, `Posting/**`, `Budget/**`, `Reference/**`, `Explanation/ExplanationVm.cs`, `IExplanationAppService.cs`, `ExplanationAppService.cs`, `Audit/AuditEventVm.cs`, `Extensions/ServiceCollectionExtensions.cs`
- Test: нет юнит-тестов (сценарии проверяются интеграционно в задаче 8); компиляция + архитектурный тест `Application_PublicApi_DoesNotExposeEntities` (добавить в `GovErp.Architecture.Tests/DomainPurityTests.cs`).

**Interfaces (Produces):**

```csharp
// Validation/Contracts/EvaluationVm.cs
public sealed record EvaluationVm(Guid Id, string TransactionRef, int TransactionVersion, string Trigger, DateTimeOffset EvaluatedAt, Guid EvaluatedBy,
    string Overall, CapabilitiesVm Capabilities, RuleSetVersionsVm RuleSetVersions, IReadOnlyList<OutcomeVm> Outcomes,
    IReadOnlyList<RouteStepVm> ApprovalRoute, IReadOnlyList<PreviewLineVm> PostingPreview, PostingCheckVm? PostingCheck);
public sealed record OutcomeVm(string RuleId, int RuleVersion, int Step, string StepName, string Layer, int? Line, string Severity,
    IReadOnlyDictionary<string, string> Inputs, IReadOnlyDictionary<string, string> Computed, string Message, string Resolution,
    IReadOnlyList<string> OverridableBy, string? OverriddenByUser, string? OverrideReason);
public sealed record RouteStepVm(string Role, string? Department, string Reason, bool IsSatisfied);
public sealed record PreviewLineVm(string Account, string Family, decimal Debit, decimal Credit, string Description);
public sealed record CapabilitiesVm(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost, bool CanPay);
public sealed record PostingCheckVm(bool Passed, IReadOnlyList<string> Failures);
public sealed record RuleSetVersionsVm(string Engine, int Core, int Federal, int State, int Tenant);

// Invoices/Contracts
public sealed record InvoiceVm(Guid Id, string Number, string Reference, Guid VendorId, string VendorName, DateOnly InvoiceDate, decimal Total,
    string? PoNumber, string Status, int Version, Guid CreatedBy, IReadOnlyList<DistributionVm> Distributions,
    IReadOnlyList<ApprovalVm> Approvals, IReadOnlyList<OverrideVm> Overrides, EvaluationVm? LastEvaluation);
public sealed record DistributionVm(int LineNo, string Account, decimal Amount, string? PoLineRef);
public sealed record ApprovalVm(string Role, Guid UserId, string Decision, string? Reason, DateTimeOffset At);
public sealed record OverrideVm(string RuleId, Guid UserId, string Reason, DateTimeOffset At);
public sealed record InvoiceListItemVm(Guid Id, string Reference, string VendorName, decimal Total, string Status, string? LastOverall, DateOnly InvoiceDate);
/// <summary>Результат действия: принято ли, почему нет, и актуальная оценка.</summary>
public sealed record ActionOutcomeVm(bool Accepted, string? Reason, InvoiceVm Invoice);

// Invoices/Commands
public sealed record DistributionCommand(string Account, decimal Amount, int? PoLineNo);
public sealed record CreateInvoiceCommand(string Number, Guid VendorId, DateOnly InvoiceDate, decimal Total, string? PoNumber, IReadOnlyList<DistributionCommand> Distributions);
public sealed record UpdateInvoiceCommand(Guid InvoiceId, string Number, Guid VendorId, DateOnly InvoiceDate, decimal Total, string? PoNumber, IReadOnlyList<DistributionCommand> Distributions);
public enum InvoicePreset { NonPoGrant, PoBackedGrant, MultiFund }

// Invoices/IInvoiceAppService.cs
public interface IInvoiceAppService
{
    Task<IReadOnlyList<InvoiceListItemVm>> ListAsync(ActorContext actor, CancellationToken ct = default);
    Task<InvoiceVm> GetAsync(Guid id, ActorContext actor, CancellationToken ct = default);
    Task<InvoiceVm> CreateDraftAsync(CreateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<InvoiceVm> CreateFromPresetAsync(InvoicePreset preset, ActorContext actor, CancellationToken ct = default);
    Task<InvoiceVm> UpdateDraftAsync(UpdateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<EvaluationVm> ValidateAsync(Guid id, ActorContext actor, CancellationToken ct = default);
    Task<ActionOutcomeVm> SubmitAsync(Guid id, ActorContext actor, CancellationToken ct = default);
}

// Approvals
public sealed record ApprovalQueueItemVm(Guid InvoiceId, string Reference, string VendorName, decimal Total, string Overall, string RequiredRole, string Reason);
public interface IApprovalAppService
{
    Task<IReadOnlyList<ApprovalQueueItemVm>> GetQueueAsync(ActorContext actor, CancellationToken ct = default);
    Task<ActionOutcomeVm> ApproveAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
    Task<ActionOutcomeVm> RejectAsync(Guid invoiceId, string reason, ActorContext actor, CancellationToken ct = default);
    Task<ActionOutcomeVm> OverrideAsync(Guid invoiceId, string ruleId, string reason, ActorContext actor, CancellationToken ct = default);
}

// Posting
public interface IPostingAppService
{
    Task<ActionOutcomeVm> PostAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PreviewLineVm>> GetJournalAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
}

// Budget
public sealed record BudgetLineVm(string Account, int FiscalYear, string ControlMode, decimal Adopted, decimal Amended, decimal Actuals, decimal Encumbered, decimal Held, decimal Available, IReadOnlyList<AmendmentVm> Amendments);
public sealed record AmendmentVm(decimal Amount, string Reference, DateOnly EffectiveDate);
public sealed record AmendBudgetCommand(string Account, decimal Amount, string Reference);
public interface IBudgetAppService
{
    Task<IReadOnlyList<BudgetLineVm>> ListAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default);
    Task<BudgetLineVm> AmendAsync(AmendBudgetCommand cmd, ActorContext actor, CancellationToken ct = default);
}

// Reference
public sealed record SegmentValueVm(string Code, string Name, bool IsActive, IReadOnlyDictionary<string, string> Attributes);
public sealed record SegmentsVm(IReadOnlyList<SegmentValueVm> Funds, IReadOnlyList<SegmentValueVm> Departments, IReadOnlyList<SegmentValueVm> Objects, IReadOnlyList<SegmentValueVm> Grants);
public sealed record CombinationVm(string Code, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Source);
public sealed record RuleVm(string RuleId, int Version, int Step, string Layer, string? Severity, IReadOnlyDictionary<string, string> Parameters, IReadOnlyList<string> OverridableBy, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsEnabled, string Message);
public sealed record VendorVm(Guid Id, string Code, string Name, string Status, bool SamRegistered);
public sealed record PurchaseOrderVm(string Number, Guid VendorId, string Status, IReadOnlyList<(int LineNo, string Account, decimal Amount)> Lines);
public interface IReferenceAppService
{
    Task<SegmentsVm> GetSegmentsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<CombinationVm>> GetCombinationsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<RuleVm>> GetRulesAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<VendorVm>> GetVendorsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderVm>> GetPurchaseOrdersAsync(ActorContext actor, CancellationToken ct = default);
}

// Explanation / Audit
public sealed record ExplanationVm(Guid EvaluationId, string Audience, string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason, DateTimeOffset GeneratedAt);
public sealed record AuditEventVm(DateTimeOffset OccurredAt, string ActorName, string Action, string SubjectRef, string CorrelationId, string PayloadJson);
public interface IExplanationAppService
{
    Task<ExplanationVm> ExplainAsync(Guid evaluationId, ExplanationAudience audience, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<ExplanationVm>> ListAsync(Guid evaluationId, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationVm>> GetEvaluationHistoryAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEventVm>> GetAuditTrailAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
}
```

- [ ] **Step 1: EvaluationRunner — общий шаг «собрать снимок, разрешить правила, оценить, сохранить»**

`Validation/EvaluationRunner.cs`:
```csharp
public sealed class EvaluationRunner(ValidationSubjectAssembler assembler, IRuleDefinitionRepository rules, IEvaluationRecordRepository records, IClock clock)
{
    private static readonly ValidationPipeline Pipeline = new(RuleCatalog.Default);

    public async Task<(EvaluationRecord Record, ValidationSubject Subject)> RunAsync(VendorInvoice invoice, EvaluationTrigger trigger, ActorContext actor, CancellationToken ct)
    {
        var subject = await assembler.BuildAsync(invoice, ct);
        var ruleSet = RuleResolution.Resolve(await rules.ListAsync(ct), invoice.InvoiceDate);
        var record = Pipeline.Evaluate(subject, ruleSet, trigger, actor.UserId, clock.Now);
        await records.AddAsync(record, ct);
        return (record, subject);
    }

    /// <summary>Синтетическая запись после конфликта резервирования: копия оценки с добавленным Hard Stop.</summary>
    public async Task<EvaluationRecord> RunWithSyntheticHardStopAsync(VendorInvoice invoice, ActorContext actor, string ruleId, string message, CancellationToken ct)
    {
        var subject = await assembler.BuildAsync(invoice, ct);                              // перечитывает бюджет — уже с чужим Held
        var ruleSet = RuleResolution.Resolve(await rules.ListAsync(ct), invoice.InvoiceDate);
        var record = Pipeline.Evaluate(subject, ruleSet, EvaluationTrigger.Submit, actor.UserId, clock.Now);
        if (record.Overall != Severity.HardStop)
        {
            // бюджет успел освободиться — но резерв мы не получили; фиксируем конфликт явно
            record = SyntheticHardStop(record, subject, ruleId, message, actor, ruleSet);
        }
        await records.AddAsync(record, ct);
        return record;
    }
}
```
`SyntheticHardStop` строит новый `EvaluationRecord` через `internal`-конструктор — он недоступен из Application. Поэтому добавить в `Domain.Validation` публичную фабрику `EvaluationRecord.WithAdditionalOutcome(RuleOutcome outcome)` → новая запись с тем же содержимым + outcome, пересчитанными `Overall`/`Capabilities` (маленькая правка домена, покрыть тестом `Record_with_additional_hard_stop_outcome_is_hard_stop` в Validation.Tests). Outcome для конфликта: `new RuleOutcome("BUDGET_CONCURRENCY", 1, ValidationStep.BudgetAvailability, RuleLayer.Core, null, Severity.HardStop, inputs, computed, message, "Retry the submission; another transaction reserved this budget first.", [], null)`.

- [ ] **Step 2: EvaluationMapping и InvoiceMapping**

`EvaluationMapping.ToVm(EvaluationRecord)` — прямое поле-в-поле; `Severity`/`Trigger`/`Layer` → `ToString()`; `StepName` → `ToString()` enum'а `ValidationStep`; `Money` → `.Amount`. `InvoiceMapping.ToVm(VendorInvoice, Vendor, EvaluationRecord? last)`, `ToListItem(...)`.

- [ ] **Step 3: InvoiceAppService и атомарный Submit/Withdraw**

Реализовать алгоритм spec §5.2–5.3. Create/Update сохраняют весь header и distributions, ContentVersion увеличивается один раз на изменение содержания; индекс VendorId + NormalizedInvoiceNumber закрывает гонку дубликатов. Submit группирует бюджет/PO, захватывает claims и reserves и сохраняет статус/evaluation/audit/receipt одной транзакцией. Withdraw автора до Post атомарно освобождает всё и возвращает Draft. Каждый retry создаёт новый scope; отдельного ReserveWithRetry и компенсационных коммитов нет.

- [ ] **Step 4: ApprovalAppService**

Approve разрешён только нужной роли/департаменту, не автору. Сохраняет ApprovalCycleId, ContentVersion, fingerprint и EvaluationId; изменение правил открывает новый цикл, изменение доступного бюджета просто перевалидирует. Override привязан к конкретному outcome, содержанию и активному циклу, с причиной. Reject освобождает claims/reserves и закрывает цикл атомарно; историю не удалять.

- [ ] **Step 5: PostingAppService**

Реализовать spec §5.4: вся загрузка/оценка внутри транзакции; не использовать оценку, рассчитанную до начала транзакции. Actuals += полный invoice, Encumbered -= consumed claims, Held -= own reservations, ровно один раз. Бюджетный резерв и liquidation вместе покрывают полную сумму. Сохранять Billing claims как consumed для накопительного PO tolerance. GetJournal читает сохранённые записи, не пересоздаёт preview.

- [ ] Проверить новые тесты task 9 ниже и task 8 до перехода к Web. Commit: `feat: implement atomic invoice lifecycle and scoped approvals`.
- [ ] **Step 6: Budget, Reference, Explanation**

`BudgetAppService.AmendAsync`: `actor.Require(BudgetOfficer, FinanceDirector)`; `line.Amend(Money.Of(cmd.Amount), cmd.Reference, clock.Today)`; аудит `BudgetAmended` с `SubjectRef = account`; сохранить. `ListAsync` — маппинг. `ReferenceAppService` — чтение и маппинг; `Attributes` у фонда: `Type`, `Basis`, `ControlMode`, `GrantPolicy`; у гранта: `Sponsor`, `IsFederal`, `PeriodFrom`, `PeriodTo`, `Status`. `ExplanationAppService.ExplainAsync`: загрузить `EvaluationRecord`, вызвать `IExplanationGenerator`, сохранить `ExplanationRecord` через `IExplanationRepository`, аудит `ExplanationGenerated` (payload: provider, model, fallback), сохранить, вернуть Vm. `GetEvaluationHistoryAsync` — `evaluations.ListByTransactionAsync(invoice.Reference)`. `GetAuditTrailAsync` — `audit.ListBySubjectAsync(invoice.Reference)`.

- [ ] **Step 7: Регистрация и архитектурный тест**

`Application.Web/Extensions/ServiceCollectionExtensions.cs`: `AddApplication(IConfiguration)` — `Configure<PostingOptions>(cfg.GetSection("Posting"))`, `AddScoped<ValidationSubjectAssembler>()`, `AddScoped<EvaluationRunner>()`, шесть `AddScoped<I*AppService, *AppService>()`.

В `GovErp.Architecture.Tests/DomainPurityTests.cs` добавить:
```csharp
[Fact]
public void Application_public_api_does_not_expose_entities()
{
    var app = typeof(GovErp.Application.Web.Common.ActorContext).Assembly;
    var entityNamespaces = ArchitectureFixture.DomainContexts.SelectMany(a => a.GetTypes()).Where(t => t.Namespace?.EndsWith(".Entities") == true).ToHashSet();
    var offenders = app.GetTypes().Where(t => t.IsInterface && t.Name.EndsWith("AppService"))
        .SelectMany(t => t.GetMethods())
        .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
        .SelectMany(Unwrap)
        .Where(entityNamespaces.Contains).Select(t => t.FullName).Distinct().ToList();
    offenders.Should().BeEmpty(because: "CA-10");

    static IEnumerable<Type> Unwrap(Type t) => t.IsGenericType ? t.GetGenericArguments().SelectMany(Unwrap).Append(t) : [t];
}
```

- [ ] **Step 8:** `dotnet build && dotnet test tests/GovErp.Architecture.Tests` — успех. Commit: `Application: invoice, approval, posting, budget, reference and explanation services`.

---

### Task 6: Seed и инициализация БД

**Files:**
- Create: `Infrastructure/Seed/SpringfieldData.cs` (если не создан в задаче 2), `SpringfieldSeed.cs`, `ShelbyvilleSeed.cs`, `RuleSeed.cs`, `MasterSeed.cs`, `SeedRunner.cs`, `Startup/DatabaseInitializer.cs`

**Interfaces:**
- Produces: `DatabaseInitializer.InitializeAsync(IServiceProvider, CancellationToken)`: миграция Master → `MasterSeed` (2 тенанта, 10 пользователей) → для каждого тенанта: создать `TenantContext`-scope, `Database.MigrateAsync()`, seed, если `Funds` пуст. `SeedRunner.SeedAsync(GovErpDbContext, TenantId)`.

- [ ] **Step 1: SpringfieldData** — доменные объекты по спеке 2.2–2.5 (фонды с ограничениями, департаменты 3000/4000/5000/6000 + 0000 «Balance sheet», object-коды 53100/54000/55000/2100/1010/2900/5900, гранты `G-COPS-26` и `G-FEMA-24` (Closed), комбинации: все пять бюджетных строк + `701-3000-54000-G-COPS-26` + `101-0000-2100`/`202-0000-2100`/`501-0000-2100`/`701-0000-2100` + `701-0000-2900-G-COPS-26` + `701-3000-5900-G-COPS-26` + одна `Inactive` `701-6000-53100-G-FEMA-24`; бюджетные строки таблицы 2.3 (через конструктор + `RecordActuals` + `RecordEncumbrance`); encumbrances `PO-2026-0450/1` 60,000 и `PO-2026-0449/1` 36,000 на `701-6000-53100-G-COPS-26`, `PO-2026-0451/1` 160,000 на `701-3000-53100-G-COPS-26`; PO с теми же номерами; vendors `Acme Consulting` (Active, SAM) и `Shady LLC` (Debarred); периоды 2025 7..12 + 2026 1..6, `2026-05` закрыт). `ClerkId` = фиксированный Guid пользователя `ap.clerk`.
- [ ] **Step 2: RuleSeed** — 12 `RuleDefinition` = `DemoRules.All()` из плана 2, сообщения и resolution — из спеки 4.2.
- [ ] **Step 3: MasterSeed** — тенанты `springfield` («City of Springfield», БД `GovErp_Springfield`), `shelbyville`; пользователи (пароль у всех `Demo!2026`, хеш через `PasswordHasher`):

| UserName | Роли | Dept | Tenant |
|---|---|---|---|
| `ap.clerk` | ApClerk | — | springfield |
| `fire.chief` | DepartmentHead | 6000 | springfield |
| `police.chief` | DepartmentHead | 3000 | springfield |
| `pw.director` | DepartmentHead | 4000 | springfield |
| `water.director` | DepartmentHead | 5000 | springfield |
| `grants.manager` | GrantsManager | — | springfield |
| `budget.officer` | BudgetOfficer | — | springfield |
| `finance.director` | FinanceDirector | — | springfield |
| `shelby.clerk` | ApClerk | — | shelbyville |
| `shelby.finance` | FinanceDirector | — | shelbyville |

- [ ] **Step 4: DatabaseInitializer** — как описано; ловить `SqlException` при недоступности сервера и повторять до 10 раз с паузой 3 с (контейнер SQL стартует дольше приложения).
- [ ] **Step 5:** Commit: `Infrastructure: seed data for Springfield and Shelbyville, database initializer`.

---

### Task 7: Web host, конфигурация, Docker

**Files:**
- Modify: `src/GovErp.Web/Program.cs`, `appsettings.json`; Create: `src/GovErp.Web/Extensions/ServiceCollectionExtensions.cs`, `src/GovErp.Web/Dockerfile`, `docker-compose.yml`, `.env.example`, `.dockerignore`

- [ ] **Step 1: Program.cs** — `builder.Services.AddApplication(cfg).AddInfrastructure(cfg)`; Razor components (шаблон); `app.MapGet("/health", ...)`; перед `app.Run()` — `await DatabaseInitializer.InitializeAsync(app.Services, CancellationToken.None)`. Аутентификация и страницы — план 4.
- [ ] **Step 2: appsettings.json**
```json
{
  "ConnectionStrings": { "Master": "Server=localhost,1433;Database=GovErp_Master;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True" },
  "Tenancy": { "TenantConnectionTemplate": "Server=localhost,1433;Database={0};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True" },
  "Posting": { "AccountsPayableObject": "2100", "ReserveForEncumbrancesObject": "2900", "EncumbrancesObject": "5900", "BalanceSheetDepartment": "0000" }
}
```
`${SA_PASSWORD}` подставляется в `Program.cs` из переменной окружения (`cfg.GetConnectionString("Master")!.Replace("${SA_PASSWORD}", Environment.GetEnvironmentVariable("SA_PASSWORD"))`) — или проще: переопределить через env `ConnectionStrings__Master` и `Tenancy__TenantConnectionTemplate` в compose. Выбрать второе, в `appsettings.json` оставить localhost-значения с паролем-заглушкой для локального запуска.
- [ ] **Step 3: Dockerfile** (multi-stage: `mcr.microsoft.com/dotnet/sdk:10.0` → publish `src/GovErp.Web` → `mcr.microsoft.com/dotnet/aspnet:10.0`, `EXPOSE 8080`, `ASPNETCORE_URLS=http://+:8080`).
- [ ] **Step 4: docker-compose.yml**
```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment: { ACCEPT_EULA: "Y", MSSQL_SA_PASSWORD: "${SA_PASSWORD}" }
    ports: ["1433:1433"]
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 12
  web:
    build: { context: ., dockerfile: src/GovErp.Web/Dockerfile }
    depends_on: { sqlserver: { condition: service_healthy } }
    environment:
      ConnectionStrings__Master: "Server=sqlserver;Database=GovErp_Master;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True"
      Tenancy__TenantConnectionTemplate: "Server=sqlserver;Database={0};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True"
      Explanation__Provider: "${EXPLANATION_PROVIDER:-Template}"
      ANTHROPIC_API_KEY: "${ANTHROPIC_API_KEY:-}"
    ports: ["8080:8080"]
```
`.env.example`: `SA_PASSWORD=GovErp!Demo2026`, `EXPLANATION_PROVIDER=Template`, `ANTHROPIC_API_KEY=`.
- [ ] **Step 5:** `docker compose up --build` — Expected: в логах `web` — миграции применены, seed выполнен, `Now listening on: http://[::]:8080`; `curl http://localhost:8080/health` → 200. `docker compose down`.
- [ ] **Step 6:** Commit: `Web host with database initialization; Docker Compose with SQL Server`.

---

### Task 8: Интеграционные тесты (Testcontainers)

**Files:**
- Create: `tests/GovErp.Application.Web.Tests/Integration/SqlServerFixture.cs`, `TenantScope.cs`, `SubmitTests.cs`, `PostTests.cs`, `ApprovalTests.cs`, `TenancyTests.cs`, `AppendOnlyTests.cs`, `ExplanationTests.cs`

**Interfaces:**
- `SqlServerFixture : IAsyncLifetime` (collection fixture): поднимает `MsSqlContainer`, создаёт `GovErp_Master`, `GovErp_Springfield`, `GovErp_Shelbyville` (через `DatabaseInitializer` с конфигурацией, указывающей на контейнер). `TenantScope.For(fixture, "springfield")` → `IServiceScope` с инициализированным `TenantContext`; `Actor(fixture, "ap.clerk")` → `ActorContext` через `ISignIn`.

- [ ] **Step 1: Фикстура** — `new MsSqlBuilder().Build()`; `ServiceCollection` с `AddApplication` + `AddInfrastructure` над `IConfiguration` из словаря (`ConnectionStrings:Master`, `Tenancy:TenantConnectionTemplate` из `container.GetConnectionString()` с подменой `Database=`); `DatabaseInitializer.InitializeAsync`. Каждый тест, меняющий данные, работает на своих инвойсах — seed не пересоздаётся.

- [ ] **Step 2: Тесты** (имена — из спеки 7):

```csharp
[Fact] Submit_TwoParallelInvoices_OneBudgetLine_ExactlyOneSucceeds
  // 10 повторов: две новые бюджетные строки? Нет — одна строка 202-4000-53100 (available 20,000, Hard):
  // два Draft по 15,000 (после каждого повтора — Reject обоих → резервы освобождены). Task.WhenAll(Submit(a), Submit(b)) в двух независимых scope.
  // Ассерт: ровно один Accepted; у второго Reason содержит "concurrent" или последний EvaluationRecord — HardStop с BUDGET_AVAILABILITY/BUDGET_CONCURRENCY; Held на строке = 15,000.
[Fact] Submit_HardStop_CreatesNoReservation            // сценарий задания: после Submit у 701-6000-53100-G-COPS-26 Held == 0, статус Draft
[Fact] Submit_Then_Reject_ReleasesReservations          // 101-6000-53100 6,000 → Submit → Reject → Held == 0, статус Draft
[Fact] Post_Commits_Liquidates_WritesJournal_InOneTransaction
  // PoBackedGrant preset → Submit → approve police.chief, grants.manager, finance.director → Post (finance.director)
  // Ассерт: статус Posted; encumbrance PO-2026-0451/1 Remaining 0 / Closed; строка 701-3000-53100-G-COPS-26: Actuals 260,000, Encumbered 0, Available 240,000; JournalEntries по INV — 1 запись, 4 строки; audit содержит InvoicePosted
[Fact] Post_JournalUnbalanced_RollsBackEverything
  // Подменить IJournalRepository в scope на фейк, бросающий LedgerException → после PostAsync (ожидаем исключение) статус остаётся Approved, encumbrance не изменилась
[Fact] Post_WhenRuleVersionChangedAfterApprove_IsRefused
  // Approved инвойс → вставить RuleDefinition BUDGET_LOW_REMAINING v2 (Tenant) → Post → Accepted false, Reason содержит REVALIDATION_REQUIRED
[Fact] Approve_ByAuthor_IsRefused                       // ap.clerk имеет только ApClerk → AuthorizationException; отдельным пользователем с ролью DepartmentHead, создавшим инвойс, — PayablesException "separation of duties"
[Fact] Approve_WhenEvaluationWorsened_IsRefused_InvoiceStaysSubmitted
  // Submit 101 инвойса 6,000 (available 10,000) → другой инвойс съедает бюджет (Submit 5,000 в Soft-фонде проходит) → Approve первого → Accepted false, статус Submitted
[Fact] Tenant_Shelbyville_CannotSeeSpringfieldInvoices  // ListAsync под shelby.clerk — пусто; GetAsync по Id из Springfield — NotFoundException
[Fact] EvaluationRecord_Update_IsRejected               // загрузить запись через DbContext, изменить через рефлексию/Entry(...).Property("Overall").CurrentValue, SaveChanges → InvalidOperationException
[Fact] Explanation_ProviderTemplate_NeverCallsChatClient // ExplainAsync → Provider == "Template", текст содержит "13,000.00" и "HARD STOP"
[Fact] Explanation_LlmFails_FallsBackToTemplate_AndRecordsReason — реализуется в плане 4 (там появится LLM-генератор); здесь — [Fact(Skip = "plan 4")]
```

- [ ] **Step 3:** `dotnet test tests/GovErp.Application.Web.Tests` (Docker должен быть запущен) — Expected: все зелёные, кроме одного Skip. Время — до 2–3 минут (старт контейнера).
- [ ] **Step 4:** Commit и push: `Integration tests: concurrency, posting transaction, approvals, tenancy, append-only`.

---

## Self-review

### Task 9: Закрытие проверок жизненного цикла

**Files:** `tests/GovErp.Application.Web.Tests/Invoices/LifecycleRegressionTests.cs`, `Persistence/Configurations/Ap/VendorInvoiceConfiguration.cs`, `Persistence/Configurations/Ledger/OpeningBalanceConfiguration.cs`, `Seed/SpringfieldData.cs`; пути Infrastructure относительно `src/GovErp.Infrastructure`.

- [ ] Написать следующие интеграционные тесты на SQL Server; каждый тест использует отдельную tenant-БД/seed, а не общие изменяемые остатки между тестами.

| Тест | Проверка |
|---|---|
| ApprovalPreservesContentVersionAndClaims | Submit → Approve: ContentVersion прежний, RowVersion новый; собственный резерв доступен Post |
| ResubmitDoesNotReuseOldApprovals | Approve → Withdraw → Submit без правки: новый ApprovalCycleId, старое согласование не удовлетворяет маршрут |
| SoftStopCanHoldButCannotPost | Soft-фонд, available 10000, invoice 12000: Held 12000, deficit 2000; Post без override запрещён |
| CumulativePoToleranceCannotBeSplit | PO 100000, posted 100000, два конкурентных инвойса по 3000: предел 105000 не превышается; один проходит, второй получает HardStop после retry |
| DuplicateRaceHasOneWinner | Одновременное создание номеров ` ABC ` и `abc` одному vendor: сохраняется один инвойс; другой получает понятную ошибку duplicate |
| WithdrawReleasesAllClaims | Submitted/Approved → Withdraw: budget/encumbrance/billing claims освобождены, журнал не создан, audit сохранён |
| PostedInvoiceCannotBeWithdrawn | Отказ без финансовых изменений |
| OpeningBalancesReconcile | Opening actuals 132000 + posted invoice 160000 = current actuals 292000; opening snapshot остаётся неизменным |
| PaymentHandoffReasons | Posted+active+due+no hold → true; каждое невыполненное условие даёт false с причиной |

- [ ] Запустить `dotnet test tests/GovErp.Application.Web.Tests --filter FullyQualifiedName~LifecycleRegression` и убедиться, что тесты выявляют отсутствующее поведение.
- [ ] Реализовать уникальный индекс `(VendorId, NormalizedInvoiceNumber)`, таблицы opening balances и billing claims, scoped approvals и Withdraw по spec §5; дубликат SQL преобразовать в бизнес-ошибку. Не полагаться на предварительный ExistsDuplicate.
- [ ] Повторить тесты и полный набор Application-тестов. Commit: `feat: complete invoice lifecycle invariants and regression coverage`.

**Покрытие спеки:** 3.6 (`IAuditTrail`) — задачи 1, 4; 3.7 / GE-9 (assembler) — задача 2; 5.1 (шесть app-сервисов) — задача 5; 5.2–5.4 (атомарные Submit/Post, rollback, конфликт) — задача 5, тесты в 8; 5.5 (идемпотентность — receipt команды) — задача 5; 6.2 (схемы, rowversion, append-only, tenancy, auth) — задачи 3–4; seed — 6; Docker — 7; интеграционные тесты 7 — задача 8. Синтетический `BUDGET_CONCURRENCY` — задача 5 через `EvaluationRecord.WithAdditionalOutcome` (правка Domain.Validation).

**Правки предыдущих планов, вносимые этим планом:** `BudgetLine.ChangeStamp` (задача 3), `IBudgetLineRepository.FindByReservationAsync` (задача 5), `EvaluationRecord.WithAdditionalOutcome` (задача 5), `EfUnitOfWork` → `BudgetConcurrencyException` (задача 5).

**Сознательные упрощения:** header инвойса не редактируется после создания; `Reject` сразу возвращает в Draft; роль отклонившего не различается; reconciler висящих резервов — только на слайде.


### Завершённые правила жизненного цикла (22 сентября 2026)

1. **Версии.** `ContentVersion` увеличивается только при изменении суммы, поставщика, дат, PO или distributions. `RowVersion` SQL Server меняется при любой записи и служит optimistic concurrency. Evaluation.TransactionVersion означает ContentVersion. Approve/Override не изменяют ContentVersion. Резервы и PO claims принадлежат InvoiceId + ContentVersion. При Reject/Withdraw старые решения остаются историей, но больше не удовлетворяют новый цикл согласования (`ApprovalCycleId`).
2. **Повторное согласование.** Изменение содержания или fingerprint применимых правил открывает новый ApprovalCycleId и делает старые approvals/overrides неприменимыми. Изменение свободного бюджета вызывает новую оценку без автоматического сброса approvals; новый HardStop блокирует действие, новый SoftStop требует своего override. Сравнение только числового severity недостаточно. Post требует активный цикл с тем же ContentVersion и fingerprint.
3. **SoftStop на Submit.** В Soft-фонде Submitted может удерживать резерв сверх available до разрешения исключения. UI явно показывает дефицит и не позволяет Post без override. В Hard-фонде недостаток бюджета запрещает резерв. После Reject/Withdraw весь резерв освобождается атомарно.
4. **PO tolerance.** База допуска — утверждённая сумма PO-строки, не её текущий остаток. `ProjectedBilled = AlreadyPostedAgainstPo + OtherActiveInvoiceClaims + CurrentInvoicePoAmount`; `CumulativeExcess = max(0, ProjectedBilled - AuthorizedPoAmount)`. При CumulativeExcess > AuthorizedPoAmount × 0.05 — HardStop. Нужен отдельный claim полной суммы PO-backed инвойса, включая превышение: encumbrance claim захватывает только ликвидируемую часть, а PO billing claim защищает накопленный допуск. Обе суммы меняются атомарно. Change order в демо не редактируется, поддерживается как seed-допущение.
5. **Дубликаты.** `NormalizedInvoiceNumber = Number.Trim().ToUpperInvariant()`. Уникальный индекс `(VendorId, NormalizedInvoiceNumber)` внутри tenant-БД, включая Draft/Rejected/Posted. Предварительная проверка даёт удобную ошибку, индекс закрывает гонку. Reject не освобождает номер. Пресеты всегда создают уникальный номер.
6. **Даты.** InvoiceDate, ServiceDate и PostingDate отдельные поля; для демо все равны 2026-06-15. Бюджетный год и открытый период определяются PostingDate, период допустимости услуги — ServiceDate, effective-правила демо — InvoiceDate. EvaluatedAt/RecordedAt — реальные UTC timestamp. Другие варианты дат отклоняются с объяснением ограничения демо, не молча приводятся к одной дате.
7. **Начальное состояние.** `ledger.OpeningBalances` хранит Account, FiscalYear, AsOfDate, InitialActuals, InitialEncumbered, SourceReference. Seed-остатки — начальный снимок, а не вымышленные проводки. Сверка: opening actuals + проведённые в прототипе финансовые расходы = actuals; encumbered отдельно сверяется с opening PO и его движениями.
8. **Отзыв.** `Withdraw` доступен автору для Submitted/Approved до Post. Одна транзакция освобождает budget/encumbrance/billing claims, закрывает цикл согласований, возвращает Draft и сохраняет причину в audit. После Post редактирование/отзыв запрещены. Корректирующие проводки и credit notes вне объёма.
9. **Готовность к оплате.** Вычисляемый `ReadyForPaymentHandoff = Posted && VendorActive && !PaymentHold && DueDate <= BusinessDate`. Добавить DueDate и PaymentHold; BusinessDate передаётся явно (в демо 2026-06-15). Это готовность передачи в платёжный модуль, не разрешение отправить деньги. Cash availability, банковские реквизиты и банковский платёж не реализуются. Статус Payable не хранить.
10. **Повтор демо.** Отдельная операторская команда `demo-reset --tenant springfield --confirm springfield` разрешена только при Environment=Demo и признаке IsDemo у тенанта. Проверить allowlist database names и закрыть активные операции на время сброса. Пересоздать только demo tenant-БД, повторить seed; Master и второй тенант не затрагивать. Никакого автоматического сброса при старте и удаления volumes штатной командой запуска. История Demo намеренно сбрасывается, что явно показывается оператору.

Вне объёма: мультивалютность (только USD, decimal(18,2), более двух дробных знаков — ошибка), налоги, credit notes, годовое закрытие, реальные закупочные проверки, изменение PO, банковские интеграции.
