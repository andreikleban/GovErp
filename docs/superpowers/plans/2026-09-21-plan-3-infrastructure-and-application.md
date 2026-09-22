# План 3: Application + Infrastructure + Docker

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать слой сценариев (атомарные команды жизненного цикла, сборщик снимков, порты), слой внешнего мира (EF Core + SQL Server, схема на контекст, БД на тенанта, receipts команд, seed, demo-reset), точку сборки и Docker — так, чтобы регрессионная матрица spec §7 (гонки бюджета и PO, атомарность Submit/Post, идемпотентность, повторное согласование, отзыв, изоляция тенантов) проходила против настоящего SQL Server.

**Architecture:** App-сервисы не держат `DbContext`: каждая команда выполняется `ITenantOperationRunner` в новом DI-scope с собственным `DbContext` и транзакцией (GE-18). Runner проверяет receipt по `CommandId`, выполняет тело, сохраняет receipt, аудит и все изменённые агрегаты одним `SaveChanges` (GE-E1, GE-16); при конфликте `rowversion` откатывает всё и один раз повторяет в новом scope, затем возвращает retryable `Conflict`. `ValidationSubjectAssembler` — единственное место, где встречаются четыре контекста (GE-9). Submit резервирует бюджет, захватывает encumbrance и PO billing claims, принадлежащие `InvoiceId + ContentVersion` (GE-14, GE-17); Post в Serializable-транзакции погашает именно их.

**Tech Stack:** .NET 10, EF Core 10 (SqlServer, Design), `Microsoft.Extensions.DependencyInjection` / `Options`, `Microsoft.Extensions.Identity.Core` (только `PasswordHasher<T>`), xUnit, FluentAssertions, Testcontainers.MsSql, Docker Compose.

**Spec:** `docs/superpowers/specs/2026-09-21-validation-engine-design.md` (разделы 2.5, 3.6–3.7, 4.4 в части Template, 5 целиком, 6.2, 6.4, 7). Манифест: `GE-1`, `GE-2`, `GE-5`, `GE-6`, `GE-9`, `GE-12`, `GE-14`…`GE-18`, `GE-E1`, `GE-E4`.

**Зависит от:** планы 1 и 2 (типы и сигнатуры — оттуда; этот план их не меняет).

## Global Constraints

- Всё из планов 1–2.
- Уточнения `2026-09-22-plan-consistency.md` (§2, §4, §5, §6, §7) и spec §5 внесены в задачи ниже; ранние наброски (`ReserveWithRetryAsync`, компенсации в `finally`, `BUDGET_CONCURRENCY`, `RuleSetVersions`, `ReservationRefs`) не использовать.
- Application ссылается на `Domain.*`; Infrastructure — на `Domain.*` и `Application.Web`; Web — на `Application.Web` и `Infrastructure` только в `Program.cs` и `Extensions/`.
- UI-facing `I*AppService` принимают `*Command` / примитивы и возвращают `*Vm` / `CommandResult<*Vm>` (`CA-10`, `NM-10`); порты (`I*Repository`, `IExplanationGenerator`) доменные типы принимают закономерно.
- Все app-сервисы принимают `ActorContext` явно (`NM-16`); тенант определяется только из `ActorContext`, который Web строит из аутентифицированного principal (план 4).
- `SaveChangesAsync` вызывает только runner; репозитории и app-сервисы не сохраняют.
- `EvaluationRecord`, `ExplanationRecord`, `JournalEntry`, `AuditEvent` — только INSERT: interceptor на EF-пути плюс `DENY UPDATE, DELETE` для runtime-пользователя БД (`GE-12`).
- Демо-дата документа и BusinessDate — 2026-06-15; открыт период 2026-06, закрыт 2026-05. Время оценок и аудита — реальное UTC.
- Версии пакетов — в `Directory.Packages.props`: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design`, `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.Extensions.Identity.Core` — последняя 10.0.x; `Testcontainers.MsSql` — последняя 4.x. Major не менять.
- `AccountCode` хранится одной колонкой `nvarchar(64)` через конвертер (spec §6.2).
- Коммит после каждой задачи; в конце сообщения — `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

---

## Структура файлов

```
src/GovErp.Application.Web/
  Common/        ActorContext.cs, Roles.cs, RoleMapping.cs, IClock.cs, AuthorizationException.cs, NotFoundException.cs
  Commands/      CommandEnvelope.cs, CommandStatus.cs, CommandResult.cs, CommandReceipt.cs, ICommandReceipts.cs,
                 IConcurrencyGuard.cs, ITenantOperationRunner.cs, DuplicateKeyException.cs
  Tenancy/       ITenantContext.cs, ITenantContextInitializer.cs, ITenantCatalog.cs, TenantInfo.cs
  Identity/      ISignIn.cs
  Audit/         AuditEvent.cs, IAuditTrail.cs, AuditEventVm.cs
  Explanation/   ExplanationAudience.cs, ExplanationResult.cs, ExplanationRecord.cs, IExplanationGenerator.cs,
                 IExplanationRepository.cs, ExplanationVm.cs, IExplanationAppService.cs, ExplanationAppService.cs
  Validation/    PostingOptions.cs, ValidationSubjectAssembler.cs, EvaluationMapping.cs, Contracts/EvaluationVm.cs
  Invoices/      InvoiceWorkspace.cs, Presets.cs, Contracts/*.cs, Commands/*.cs, Mapping/InvoiceMapping.cs,
                 IInvoiceAppService.cs, InvoiceAppService.cs
  Approvals/     Contracts/ApprovalQueueItemVm.cs, Commands/*.cs, IApprovalAppService.cs, ApprovalAppService.cs
  Posting/       IPostingAppService.cs, PostingAppService.cs
  Budget/        Contracts/BudgetLineVm.cs, Commands/AmendBudgetCommand.cs, IBudgetAppService.cs, BudgetAppService.cs
  Reference/     Contracts/*.cs, IReferenceAppService.cs, ReferenceAppService.cs
  Extensions/    ServiceCollectionExtensions.cs
src/GovErp.Infrastructure/
  Persistence/   GovErpDbContext.cs, GovErpDbContextFactory.cs, Conversions.cs, JsonColumn.cs, AppendOnlyInterceptor.cs,
                 EfConcurrencyGuard.cs, EfCommandReceipts.cs, Configurations/{Coa,Ledger,Ap,Validation,Audit}/*.cs,
                 Repositories/Ef*.cs, Migrations/
  Operations/    EfTenantOperationRunner.cs
  Master/        MasterDbContext.cs, Tenant.cs, UserAccount.cs, MasterDbContextFactory.cs, Migrations/
  Tenancy/       TenancyOptions.cs, TenantContext.cs, MasterTenantCatalog.cs
  Identity/      MasterSignIn.cs
  Audit/         EfAuditTrail.cs
  Explanation/   TemplateExplanationGenerator.cs, EfExplanationRepository.cs
  Common/        SystemClock.cs
  Seed/          SpringfieldData.cs, RuleSeed.cs, MasterSeed.cs, TenantSeeder.cs
  Startup/       DatabaseInitializer.cs, DatabaseSecurity.cs, DemoReset.cs
  Extensions/    ServiceCollectionExtensions.cs
src/GovErp.Web/  Program.cs, Extensions/ServiceCollectionExtensions.cs, appsettings.json, Dockerfile
docker-compose.yml, .env.example, .dockerignore
tests/GovErp.Application.Web.Tests/
  Support/InMemoryRepositories.cs, FixedClock.cs, SqlServerFixture.cs, TenantDriver.cs
  ValidationSubjectAssemblerTests.cs, EnumMappingTests.cs, JsonRoundTripTests.cs
  Integration/ConcurrencyAndAtomicityTests.cs, Integration/LifecycleTests.cs, Integration/PlatformTests.cs
```

---

### Task 1: Application — общие типы, команды и порты

**Files:**
- Create: всё из `Common/`, `Commands/`, `Tenancy/`, `Identity/`, `Audit/` (кроме `AuditEventVm`), `Explanation/` (порты и записи), `Validation/PostingOptions.cs`
- Modify: `Directory.Build.props` — глобальный `using GovErp.Domain.Shared.ValueObjects` также для `GovErp.Application.*` и `GovErp.Infrastructure`; `src/GovErp.Application.Web/GovErp.Application.Web.csproj` — `PackageReference` на `Microsoft.Extensions.DependencyInjection` и `Microsoft.Extensions.Options.ConfigurationExtensions`
- Test: `tests/GovErp.Application.Web.Tests/CommandResultTests.cs`

**Interfaces (Produces):**

```csharp
// Common/ActorContext.cs
namespace GovErp.Application.Web.Common;
/// <summary>Кто выполняет сценарий. Строится Web из аутентифицированного principal; сценарии получают его параметром (NM-16).</summary>
public sealed record ActorContext(TenantId TenantId, UserId UserId, string UserName, IReadOnlySet<string> Roles, string? DepartmentCode)
{
    public bool IsInRole(string role) => Roles.Contains(role);
    public bool IsInAnyRole(params string[] roles) => roles.Any(IsInRole);
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
    public static readonly string[] Overriders = [BudgetOfficer, FinanceDirector];
    public static readonly string[] Posters = [BudgetOfficer, FinanceDirector];
}

// Common/RoleMapping.cs — у каждого контекста свой enum ролей с теми же именами (EnumMappingTests сверяет множества)
public static class RoleMapping
{
    public static Domain.Validation.ValueObjects.ApproverRole ToValidation(Domain.Payables.Entities.ApproverRole r) =>
        Enum.Parse<Domain.Validation.ValueObjects.ApproverRole>(r.ToString());
    public static Domain.Payables.Entities.ApproverRole ToPayables(Domain.Validation.ValueObjects.ApproverRole r) =>
        Enum.Parse<Domain.Payables.Entities.ApproverRole>(r.ToString());
    public static string ToRoleName(Domain.Validation.ValueObjects.ApproverRole r) => r.ToString();
    public static Domain.Payables.Entities.ApproverRole? PayablesRoleOf(ActorContext actor) =>
        Enum.GetValues<Domain.Payables.Entities.ApproverRole>().Cast<Domain.Payables.Entities.ApproverRole?>()
            .FirstOrDefault(r => actor.IsInRole(r!.Value.ToString()));
}

// Common/IClock.cs
public interface IClock
{
    DateTimeOffset Now { get; }
    /// <summary>Бизнес-дата демо (2026-06-15) — из конфигурации, не из системных часов (spec §5, правило 9).</summary>
    DateOnly BusinessDate { get; }
}

// Common/AuthorizationException.cs, Common/NotFoundException.cs
public sealed class AuthorizationException(string message) : Exception(message);
public sealed class NotFoundException(string message) : Exception(message);

// Commands/CommandEnvelope.cs
/// <summary>CommandId удерживается формой до ответа; ExpectedRowVersion — base64 RowVersion агрегата, который видел пользователь (null для Create).</summary>
public sealed record CommandEnvelope(Guid CommandId, string? ExpectedRowVersion);

// Commands/CommandStatus.cs
public enum CommandStatus { Accepted, Refused, Conflict, Forbidden, NotFound }

// Commands/CommandResult.cs
public sealed record CommandResult<T>(CommandStatus Status, T? Value, string? Reason, bool Retryable)
{
    public bool IsAccepted => Status == CommandStatus.Accepted;
    public static CommandResult<T> Accepted(T value) => new(CommandStatus.Accepted, value, null, false);
    /// <summary>Бизнес-отказ: команда не изменила финансовое состояние; Value несёт актуальную оценку.</summary>
    public static CommandResult<T> Refused(T? value, string reason) => new(CommandStatus.Refused, value, reason, false);
    public static CommandResult<T> Conflict(string reason) => new(CommandStatus.Conflict, default, reason, true);
    public static CommandResult<T> Forbidden(string reason) => new(CommandStatus.Forbidden, default, reason, false);
    public static CommandResult<T> NotFound(string reason) => new(CommandStatus.NotFound, default, reason, false);
}

// Commands/CommandReceipt.cs — запись ap.ProcessedCommands (GE-16)
public sealed record CommandReceipt(Guid CommandId, UserId ActorId, string CommandType, string RequestHash, string ResultJson, DateTimeOffset At);

// Commands/ICommandReceipts.cs
public interface ICommandReceipts
{
    Task<CommandReceipt?> FindAsync(Guid commandId, CancellationToken ct = default);
    void Add(CommandReceipt receipt);
}

// Commands/IConcurrencyGuard.cs
/// <summary>Связывает агрегат с RowVersion, который видел пользователь: устаревшая форма даёт Conflict, а не тихую перезапись.</summary>
public interface IConcurrencyGuard
{
    void Expect(object aggregate, string? rowVersion);
    string? VersionOf(object aggregate);
}

// Commands/DuplicateKeyException.cs — Infrastructure переводит нарушение уникального индекса в этот тип
public sealed class DuplicateKeyException(string message) : Exception(message);

// Commands/ITenantOperationRunner.cs
public interface ITenantOperationRunner
{
    /// <summary>
    /// Мутирующая команда: новый scope и DbContext тенанта актора, транзакция с заданной изоляцией, receipt по CommandId,
    /// один SaveChanges + commit; конфликт rowversion — rollback и один повтор в новом scope, затем retryable Conflict.
    /// Тело не вызывает SaveChanges. Результат Refused тоже фиксируется (оценка, аудит, receipt) — финансовые изменения тело в этом случае не делает.
    /// </summary>
    Task<CommandResult<T>> ExecuteAsync<T>(ActorContext actor, CommandEnvelope envelope, string commandType, object request,
        Func<IServiceProvider, CancellationToken, Task<CommandResult<T>>> body,
        System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default);

    /// <summary>Чтение: новый scope тенанта актора, без транзакции и без сохранения.</summary>
    Task<T> QueryAsync<T>(ActorContext actor, Func<IServiceProvider, CancellationToken, Task<T>> body, CancellationToken ct = default);
}

// Tenancy
public interface ITenantContext { TenantId TenantId { get; } string ConnectionString { get; } bool IsInitialized { get; } }
public interface ITenantContextInitializer { void Initialize(TenantId tenantId, string connectionString); }
public sealed record TenantInfo(TenantId Id, string Name, string DatabaseName, bool IsDemo);
public interface ITenantCatalog
{
    Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default);
    Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default);
    /// <summary>Строка подключения runtime-пользователя тенанта; секрет не покидает Infrastructure.</summary>
    string RuntimeConnectionString(TenantInfo tenant);
}

// Identity/ISignIn.cs
public interface ISignIn { Task<ActorContext?> AuthenticateAsync(string userName, string password, CancellationToken ct = default); }

// Audit
public sealed record AuditEvent(Guid Id, TenantId TenantId, DateTimeOffset OccurredAt, UserId Actor, string ActorName,
    string Action, string SubjectRef, string CorrelationId, string PayloadJson);
public interface IAuditTrail
{
    void Record(ActorContext actor, string action, string subjectRef, string correlationId, object payload);
    Task<IReadOnlyList<AuditEvent>> ListBySubjectAsync(string subjectRef, CancellationToken ct = default);
}

// Explanation
public enum ExplanationAudience { FinanceUser, DepartmentManager, Auditor, Public }
public sealed record ExplanationResult(string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason);
public interface IExplanationGenerator
{
    Task<ExplanationResult> ExplainAsync(Domain.Validation.Entities.EvaluationRecord record, ExplanationAudience audience, CancellationToken ct = default);
}
public sealed record ExplanationRecord(Guid Id, Guid EvaluationId, string TransactionRef, ExplanationAudience Audience, string Text,
    string Provider, string? Model, string PromptVersion, string? FallbackReason, DateTimeOffset GeneratedAt);
public interface IExplanationRepository
{
    void Add(ExplanationRecord record);
    Task<IReadOnlyList<ExplanationRecord>> ListByEvaluationAsync(Guid evaluationId, CancellationToken ct = default);
}

// Validation/PostingOptions.cs
public sealed class PostingOptions
{
    public string AccountsPayableObject { get; set; } = "2100";
    public string ReserveForEncumbrancesObject { get; set; } = "2900";
    public string EncumbrancesObject { get; set; } = "5900";
    public string BalanceSheetDepartment { get; set; } = "0000";
    public Domain.Validation.ValueObjects.PostingAccounts ToPostingAccounts() =>
        new(new ObjectCode(AccountsPayableObject), new ObjectCode(ReserveForEncumbrancesObject),
            new ObjectCode(EncumbrancesObject), new DepartmentCode(BalanceSheetDepartment));
}
```

- [ ] **Step 1: Создать тестовый проект Application**

```powershell
dotnet new xunit -n GovErp.Application.Web.Tests -o tests/GovErp.Application.Web.Tests
Remove-Item tests/GovErp.Application.Web.Tests/UnitTest1.cs
dotnet sln add tests/GovErp.Application.Web.Tests
foreach ($p in "Shared","ChartOfAccounts","Ledger","Payables","Validation") { dotnet add tests/GovErp.Application.Web.Tests reference "src/GovErp.Domain.$p" }
dotnet add tests/GovErp.Application.Web.Tests reference src/GovErp.Application.Web
dotnet add tests/GovErp.Application.Web.Tests reference src/GovErp.Infrastructure
```
csproj — CPM-вариант (план 1, задача 1) плюс `Microsoft.Extensions.DependencyInjection`, `Microsoft.Extensions.Options.ConfigurationExtensions`, `Microsoft.EntityFrameworkCore.SqlServer`, `Testcontainers.MsSql`.

- [ ] **Step 2: Тест**

`tests/GovErp.Application.Web.Tests/CommandResultTests.cs`:
```csharp
using GovErp.Application.Web.Commands;

namespace GovErp.Application.Web.Tests;

public class CommandResultTests
{
    [Fact]
    public void Only_conflict_is_retryable()
    {
        CommandResult<int>.Conflict("x").Retryable.Should().BeTrue();
        CommandResult<int>.Refused(1, "x").Retryable.Should().BeFalse();
        CommandResult<int>.Accepted(1).IsAccepted.Should().BeTrue();
        CommandResult<int>.Forbidden("x").Status.Should().Be(CommandStatus.Forbidden);
    }
}
```

- [ ] **Step 3:** Создать все файлы из блока выше (по одному публичному типу на файл, пространство имён = папка).
- [ ] **Step 4:** `dotnet test tests/GovErp.Application.Web.Tests --filter CommandResultTests` — Expected: 1 passed.
- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Application: actor context, command envelope and result, ports for operations, receipts, tenancy, audit, explanation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: Данные Springfield и ValidationSubjectAssembler

**Files:**
- Create: `src/GovErp.Infrastructure/Seed/SpringfieldData.cs`, `src/GovErp.Infrastructure/Seed/RuleSeed.cs`
- Create: `src/GovErp.Application.Web/Validation/ValidationSubjectAssembler.cs`
- Test: `tests/GovErp.Application.Web.Tests/Support/InMemoryRepositories.cs`, `Support/FixedClock.cs`, `ValidationSubjectAssemblerTests.cs`, `EnumMappingTests.cs`

**Interfaces:**
- Produces: `SpringfieldData.Create()` — свежий набор доменных объектов seed (spec §2.2–2.5): `Funds`, `Departments`, `Objects`, `Grants`, `Combinations`, `OpeningBalances`, `BudgetLines`, `Encumbrances`, `PurchaseOrders`, `Vendors`, `Periods`, `Rules`, константы `ClerkId`, `SystemUserId`, `AcmeId`, `ShadyId`, `DormantId`, `Jun15`, `Dates`; фабрики черновиков `NonPoExerciseInvoice(string number = "V-7781", InvoiceDates? dates = null)`, `PoBackedInvoice(decimal amount = 160_000m, string number = "V-0451-1")`, `MultiFundInvoice(string number = "V-MF-1")`. `RuleSeed.All(DateOnly effectiveFrom)` — 12 определений spec §4.2 (те же, что `DemoRules` плана 2, с сообщениями и resolution).
- Produces: `ValidationSubjectAssembler.BuildAsync(VendorInvoice, CancellationToken) → ValidationSubject`. Конструктор: `IFundRepository, IGrantRepository, IAccountCombinationRepository, IBudgetLineRepository, IEncumbranceRepository, IFiscalPeriodRepository, IVendorRepository, IPurchaseOrderRepository, IVendorInvoiceRepository, IEvaluationRecordRepository, IOptions<PostingOptions>`.
- Даты (spec §5, правило 6): бюджетный год и период — `PostingDate`; грант — `ServiceDate`; комбинация — `InvoiceDate`. Собственные резервы и claims — `InvoiceId + ContentVersion`; `ApprovalBaseline` — из оценки последнего согласования активного цикла.

- [ ] **Step 1: SpringfieldData и RuleSeed**

`src/GovErp.Infrastructure/Seed/SpringfieldData.cs`:
```csharp
using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.Entities;
using LedgerControl = GovErp.Domain.Ledger.Entities.BudgetControlMode;
using CoaControl = GovErp.Domain.ChartOfAccounts.Entities.BudgetControlMode;

namespace GovErp.Infrastructure.Seed;

/// <summary>Seed Springfield (spec §2.2–2.5). Каждый вызов Create() — новые объекты: тесты не делят изменяемое состояние.</summary>
public sealed class SpringfieldData
{
    public static readonly UserId ClerkId = new(Guid.Parse("10000000-0000-0000-0000-000000000001"));
    public static readonly UserId SystemUserId = new(Guid.Parse("10000000-0000-0000-0000-0000000000ff"));
    public static readonly Guid AcmeId = Guid.Parse("20000000-0000-0000-0000-000000000001");
    public static readonly Guid ShadyId = Guid.Parse("20000000-0000-0000-0000-000000000002");
    public static readonly Guid DormantId = Guid.Parse("20000000-0000-0000-0000-000000000003");
    public static readonly DateOnly Jun15 = new(2026, 6, 15);
    public static readonly InvoiceDates Dates = new(Jun15, Jun15, Jun15, new DateOnly(2026, 7, 15));
    private static readonly DateOnly Fy2026Start = new(2025, 7, 1);
    private static readonly DateTimeOffset LoadedAt = new(2026, 6, 1, 0, 0, 0, TimeSpan.Zero);

    public List<Fund> Funds { get; } = [];
    public List<Department> Departments { get; } = [];
    public List<ObjectCodeDefinition> Objects { get; } = [];
    public List<Grant> Grants { get; } = [];
    public List<AccountCombination> Combinations { get; } = [];
    public List<OpeningBalance> OpeningBalances { get; } = [];
    public List<BudgetLine> BudgetLines { get; } = [];
    public List<Encumbrance> Encumbrances { get; } = [];
    public List<PurchaseOrder> PurchaseOrders { get; } = [];
    public List<Vendor> Vendors { get; } = [];
    public List<FiscalPeriod> Periods { get; } = [];
    public List<RuleDefinition> Rules { get; } = [];

    private static DepartmentCode D(string c) => new(c);
    private static ObjectCode O(string c) => new(c);
    private static AccountCode A(string c) => AccountCode.Parse(c);

    public static SpringfieldData Create()
    {
        var d = new SpringfieldData();
        ObjectCode[] spend = [O("53100"), O("54000"), O("55000")];

        d.Funds.AddRange(
        [
            new Fund(new FundCode("101"), "General Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual, CoaControl.Soft, GrantPolicy.Forbidden, [], []),
            new Fund(new FundCode("202"), "Street Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual, CoaControl.Hard, GrantPolicy.Forbidden, [D("4000")], spend),
            new Fund(new FundCode("501"), "Water Enterprise Fund", FundType.Enterprise, AccountingBasis.FullAccrual, CoaControl.Soft, GrantPolicy.Forbidden, [D("5000")], []),
            new Fund(new FundCode("701"), "Grants Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual, CoaControl.Hard, GrantPolicy.Required, [], []),
        ]);
        d.Departments.AddRange(
        [
            new Department(D("0000"), "Balance sheet"), new Department(D("3000"), "Police"), new Department(D("4000"), "Public Works"),
            new Department(D("5000"), "Water Utility"), new Department(D("6000"), "Fire"),
        ]);
        d.Objects.AddRange(
        [
            new ObjectCodeDefinition(O("53100"), "Professional Services", ObjectCategory.Expenditure),
            new ObjectCodeDefinition(O("54000"), "Supplies", ObjectCategory.Expenditure),
            new ObjectCodeDefinition(O("55000"), "Capital Outlay", ObjectCategory.Expenditure),
            new ObjectCodeDefinition(O("2100"), "Accounts Payable", ObjectCategory.Liability),
            new ObjectCodeDefinition(O("1010"), "Cash", ObjectCategory.Asset),
            new ObjectCodeDefinition(O("2900"), "Reserve for Encumbrances", ObjectCategory.Budgetary),
            new ObjectCodeDefinition(O("5900"), "Encumbrances", ObjectCategory.Budgetary),
        ]);
        d.Grants.AddRange(
        [
            new Grant(new GrantCode("G-COPS-26"), "COPS program (exercise assumption)", "Federal sponsor (assumed)", isFederal: true,
                new DatePeriod(Fy2026Start, new DateOnly(2027, 6, 30)), [D("3000"), D("6000")], [O("53100"), O("54000")]),
            new Grant(new GrantCode("G-FEMA-24"), "Closed grant (demo)", "Federal sponsor (assumed)", isFederal: true,
                new DatePeriod(new DateOnly(2023, 7, 1), new DateOnly(2025, 6, 30)), [], [], GrantStatus.Closed),
        ]);

        string[] active =
        [
            "701-6000-53100-G-COPS-26", "701-3000-53100-G-COPS-26", "701-3000-54000-G-COPS-26",
            "101-6000-53100", "202-4000-53100", "501-5000-53100",
            "101-0000-2100", "202-0000-2100", "501-0000-2100", "701-0000-2100",
            "701-0000-2900-G-COPS-26", "701-3000-5900-G-COPS-26", "701-6000-5900-G-COPS-26",
        ];
        foreach (var code in active)
        {
            var c = AccountCombination.Request(A(code), Fy2026Start, SystemUserId, LoadedAt, CombinationSource.Generated);
            c.Approve(SystemUserId, LoadedAt);
            d.Combinations.Add(c);
        }

        var fema = AccountCombination.Request(A("701-6000-53100-G-FEMA-24"), new DateOnly(2023, 7, 1), SystemUserId, LoadedAt, CombinationSource.Generated);
        fema.Approve(SystemUserId, LoadedAt);
        fema.Deactivate(new DateOnly(2025, 6, 30));
        d.Combinations.Add(fema);

        var fy = new FiscalYear(2026);
        void Budget(string account, decimal adopted, decimal actuals, decimal encumbered, LedgerControl mode)
        {
            var opening = new OpeningBalance(A(account), fy, new DateOnly(2026, 6, 1), Money.Of(actuals), Money.Of(encumbered), "FY2026 opening load (demo)");
            d.OpeningBalances.Add(opening);
            d.BudgetLines.Add(BudgetLine.Open(opening, mode, Money.Of(adopted)));
        }

        Budget("701-6000-53100-G-COPS-26", 375_000m, 132_000m, 96_000m, LedgerControl.Hard);
        Budget("701-3000-53100-G-COPS-26", 500_000m, 100_000m, 160_000m, LedgerControl.Hard);
        Budget("101-6000-53100", 50_000m, 40_000m, 0m, LedgerControl.Soft);
        Budget("202-4000-53100", 25_000m, 5_000m, 0m, LedgerControl.Hard);
        Budget("501-5000-53100", 60_000m, 30_000m, 0m, LedgerControl.Soft);

        d.Vendors.AddRange(
        [
            new Vendor(AcmeId, "ACME", "Acme Consulting", VendorStatus.Active, samRegistered: true),
            new Vendor(ShadyId, "SHADY", "Shady LLC", VendorStatus.Debarred, samRegistered: false),
            new Vendor(DormantId, "DORMANT", "Dormant Supply", VendorStatus.Inactive, samRegistered: true),
        ]);

        void Po(string number, string account, decimal amount)
        {
            d.PurchaseOrders.Add(new PurchaseOrder(number, AcmeId, [(1, A(account), Money.Of(amount))]));
            d.Encumbrances.Add(new Encumbrance($"{number}/1", A(account), fy, Money.Of(amount)));
        }

        Po("PO-2026-0449", "701-6000-53100-G-COPS-26", 36_000m);
        Po("PO-2026-0450", "701-6000-53100-G-COPS-26", 60_000m);
        Po("PO-2026-0451", "701-3000-53100-G-COPS-26", 160_000m);

        for (var m = 0; m < 12; m++)
        {
            var date = Fy2026Start.AddMonths(m);
            var period = new FiscalPeriod(date.Year, date.Month);
            if (date.Year == 2026 && date.Month <= 5)
            {
                period.Close();
            }

            d.Periods.Add(period);
        }

        d.Rules.AddRange(RuleSeed.All(Fy2026Start));
        return d;
    }

    public VendorInvoice NonPoExerciseInvoice(string number = "V-7781", InvoiceDates? dates = null) =>
        new(number, AcmeId, dates ?? Dates, Money.Of(160_000m), null,
            [new DistributionInput(A("701-6000-53100-G-COPS-26"), Money.Of(160_000m), null)], ClerkId, LoadedAt);

    public VendorInvoice PoBackedInvoice(decimal amount = 160_000m, string number = "V-0451-1") =>
        new(number, AcmeId, Dates, Money.Of(amount), "PO-2026-0451",
            [new DistributionInput(A("701-3000-53100-G-COPS-26"), Money.Of(amount), 1)], ClerkId, LoadedAt);

    public VendorInvoice MultiFundInvoice(string number = "V-MF-1") =>
        new(number, AcmeId, Dates, Money.Of(30_000m), null,
        [
            new DistributionInput(A("101-6000-53100"), Money.Of(12_000m), null),
            new DistributionInput(A("202-4000-53100"), Money.Of(8_000m), null),
            new DistributionInput(A("501-5000-53100"), Money.Of(10_000m), null),
        ], ClerkId, LoadedAt);
}
```

Периоды: 2025-07…2026-04 закрываются вместе с 2026-05 — открыт только июнь 2026, что соответствует демо-дате; негативный тест использует май.

`src/GovErp.Infrastructure/Seed/RuleSeed.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Infrastructure.Seed;

/// <summary>12 определений spec §4.2. Пороги и слои — допущения демо, не юридические нормы.</summary>
public static class RuleSeed
{
    public static IReadOnlyList<RuleDefinition> All(DateOnly from)
    {
        RuleDefinition R(string id, ValidationStep step, RuleLayer layer, bool adjustable, Severity? severity, string message, string resolution,
            Dictionary<string, string>? p = null, ApproverRole[]? overridable = null) =>
            new(id, 1, step, layer, adjustable, severity, p ?? [], overridable ?? [], from, null, message, resolution);

        return
        [
            R("SEG_REQUIRED", ValidationStep.RequiredSegments, RuleLayer.Core, false, Severity.HardStop,
                "A required chart-of-accounts segment is missing.", "Add the missing segment."),
            R("SEG_GRANT_FORBIDDEN", ValidationStep.RequiredSegments, RuleLayer.Core, false, Severity.HardStop,
                "The fund does not accept a grant segment.", "Remove the grant segment or use a grant fund."),
            R("COA_COMBINATION_ACTIVE", ValidationStep.ValidCombination, RuleLayer.Core, false, Severity.HardStop,
                "The account combination is not active.", "Use an active combination or request a new one from Finance."),
            R("FUND_DEPT_OBJECT_ALLOWED", ValidationStep.FundAndGrantRestrictions, RuleLayer.Tenant, false, Severity.HardStop,
                "The department or object is not an allowed use of the fund.", "Recode to a fund that permits this use."),
            R("GRANT_ELIGIBLE", ValidationStep.FundAndGrantRestrictions, RuleLayer.Federal, false, Severity.HardStop,
                "The expense is not eligible under the grant.", "Recode to an eligible grant or to a non-grant fund."),
            R("VENDOR_ELIGIBLE", ValidationStep.TransactionPurpose, RuleLayer.Federal, false, Severity.HardStop,
                "The vendor is not eligible for payment.", "Resolve vendor status before submitting."),
            R("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.State, true, Severity.SoftStop,
                "Non-PO invoice meets the procurement threshold.", "Attach a purchase order or a documented procurement exception.",
                new() { ["threshold"] = "25000" }, [ApproverRole.FinanceDirector]),
            R("INVOICE_DUPLICATE", ValidationStep.TransactionPurpose, RuleLayer.Core, false, Severity.HardStop,
                "Duplicate invoice number for this vendor.", "Verify the invoice was not already entered."),
            R("BUDGET_AVAILABILITY", ValidationStep.BudgetAvailability, RuleLayer.Core, false, null,
                "The invoice exceeds available budget.", "Budget amendment, budget transfer, or authorized coding change.",
                overridable: [ApproverRole.BudgetOfficer, ApproverRole.FinanceDirector]),
            R("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, true, Severity.Warning,
                "Little budget remains after this invoice.", "No action required; consider a budget review.",
                new() { ["pct"] = "0.10" }),
            R("PO_LIQUIDATION", ValidationStep.EncumbranceImpact, RuleLayer.Core, true, null,
                "PO liquidation and tolerance check.", "Within tolerance no action is required; above it a change order is required.",
                new() { ["tolerance_pct"] = "0.05" }),
            R("APPROVAL_ROUTE", ValidationStep.ApprovalRequirements, RuleLayer.Tenant, true, null,
                "Approval route parameters.", "—",
                new() { ["finance_director_threshold"] = "50000" }),
        ];
    }
}
```

- [ ] **Step 2: Фейки репозиториев и часы**

`tests/GovErp.Application.Web.Tests/Support/InMemoryRepositories.cs` — по одному классу на порт, над `List<T>`, конструктор принимает `IEnumerable<T>`. Образец (остальные — тем же способом, методы порта — LINQ по списку):
```csharp
public sealed class InMemoryBudgetLineRepository(IEnumerable<BudgetLine> lines) : IBudgetLineRepository
{
    public List<BudgetLine> Items { get; } = [.. lines];
    public Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fy, CancellationToken ct = default) =>
        Task.FromResult(Items.SingleOrDefault(l => l.Account == account && l.FiscalYear == fy));
    public Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fy, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BudgetLine>>(Items.Where(l => l.FiscalYear == fy).ToList());
    public Task<IReadOnlyList<BudgetLine>> ListHeldForInvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<BudgetLine>>(Items.Where(l => l.Reservations.Any(r => r.InvoiceId == invoiceId && r.Status == ReservationStatus.Held)).ToList());
    public Task AddAsync(BudgetLine line, CancellationToken ct = default) { Items.Add(line); return Task.CompletedTask; }
}
```
Нужны: `InMemoryFundRepository`, `InMemoryGrantRepository`, `InMemoryAccountCombinationRepository`, `InMemoryBudgetLineRepository`, `InMemoryEncumbranceRepository`, `InMemoryFiscalPeriodRepository`, `InMemoryVendorRepository`, `InMemoryPurchaseOrderRepository`, `InMemoryVendorInvoiceRepository` (`ExistsDuplicateAsync` — `Items.Any(i => i.VendorId == v && i.NormalizedNumber == n && i.Id != excluding)`), `InMemoryEvaluationRecordRepository`. `Support/FixedClock.cs`: `public sealed class FixedClock(DateTimeOffset now, DateOnly businessDate) : IClock`.

- [ ] **Step 3: Тесты**

`ValidationSubjectAssemblerTests.cs`:
```csharp
using GovErp.Application.Web.Validation;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.Options;

namespace GovErp.Application.Web.Tests;

public class ValidationSubjectAssemblerTests
{
    private static readonly AccountCode Fire = AccountCode.Parse("701-6000-53100-G-COPS-26");
    private static readonly AccountCode Police = AccountCode.Parse("701-3000-53100-G-COPS-26");

    private static ValidationSubjectAssembler Assembler(SpringfieldData d, IEnumerable<VendorInvoice>? invoices = null,
        IEnumerable<Domain.Validation.Entities.EvaluationRecord>? evaluations = null) =>
        new(new InMemoryFundRepository(d.Funds), new InMemoryGrantRepository(d.Grants), new InMemoryAccountCombinationRepository(d.Combinations),
            new InMemoryBudgetLineRepository(d.BudgetLines), new InMemoryEncumbranceRepository(d.Encumbrances),
            new InMemoryFiscalPeriodRepository(d.Periods), new InMemoryVendorRepository(d.Vendors),
            new InMemoryPurchaseOrderRepository(d.PurchaseOrders), new InMemoryVendorInvoiceRepository(invoices ?? []),
            new InMemoryEvaluationRecordRepository(evaluations ?? []), Options.Create(new PostingOptions()));

    [Fact]
    public async Task Exercise_invoice_yields_exercise_snapshot()
    {
        var d = SpringfieldData.Create();
        var s = await Assembler(d).BuildAsync(d.NonPoExerciseInvoice());
        s.Transaction.Total.Should().Be(Money.Of(160_000m));
        s.Transaction.IsPoBacked.Should().BeFalse();
        s.Transaction.ContentVersion.Should().Be(1);
        var dist = s.Distributions.Single();
        dist.Fund!.Control.Should().Be(BudgetControl.Hard);
        dist.Fund.GrantRule.Should().Be(GrantRule.Required);
        dist.Grant!.Eligibility.Should().Be(GrantEligibilityResult.Eligible);
        dist.Combination.IsActiveOnDate.Should().BeTrue();
        s.BudgetFor(Fire).Available.Should().Be(Money.Of(147_000m));
        s.BudgetFor(Fire).OwnHeld.Should().Be(Money.Zero);
        s.PeriodIsOpen.Should().BeTrue();
        s.ApprovalBaseline.Should().BeNull();
    }

    [Fact]
    public async Task Own_reservation_is_reported_as_own_held()
    {
        var d = SpringfieldData.Create();
        var inv = d.NonPoExerciseInvoice();
        d.BudgetLines.Single(l => l.Account == Fire).Reserve(inv.Id, inv.ContentVersion, Money.Of(100_000m));
        var s = await Assembler(d).BuildAsync(inv);
        s.BudgetFor(Fire).Held.Should().Be(Money.Of(100_000m));
        s.BudgetFor(Fire).OwnHeld.Should().Be(Money.Of(100_000m));
    }

    [Fact]
    public async Task Po_backed_invoice_carries_po_line_and_claimable_encumbrance()
    {
        var d = SpringfieldData.Create();
        var other = Guid.NewGuid();
        d.Encumbrances.Single(e => e.PoLineRef == "PO-2026-0451/1").Claim(other, 1, Money.Of(10_000m));
        d.PurchaseOrders.Single(p => p.Number == "PO-2026-0451").ClaimBilling(1, other, 1, Money.Of(10_000m));
        var s = await Assembler(d).BuildAsync(d.PoBackedInvoice());
        var po = s.PoLines.Single();
        po.PoLineRef.Should().Be("PO-2026-0451/1");
        po.AuthorizedAmount.Should().Be(Money.Of(160_000m));
        po.OtherActiveClaims.Should().Be(Money.Of(10_000m));
        po.Encumbrance!.ClaimableForInvoice.Should().Be(Money.Of(150_000m));
        s.Distributions.Single().PoLineRef.Should().Be("PO-2026-0451/1");
        s.RequiredNewBudget(Police).Should().Be(Money.Of(10_000m));
    }

    [Fact]
    public async Task Missing_combination_and_budget_are_reported_not_thrown()
    {
        var d = SpringfieldData.Create();
        var inv = new VendorInvoice("X-1", SpringfieldData.AcmeId, SpringfieldData.Dates, Money.Of(10m), null,
            [new DistributionInput(AccountCode.Parse("101-3000-54000"), Money.Of(10m), null)], SpringfieldData.ClerkId, DateTimeOffset.UtcNow);
        var s = await Assembler(d).BuildAsync(inv);
        s.Distributions.Single().Combination.Exists.Should().BeFalse();
        s.BudgetFor(AccountCode.Parse("101-3000-54000")).Exists.Should().BeFalse();
    }

    [Fact]
    public async Task Duplicate_and_closed_period_are_detected()
    {
        var d = SpringfieldData.Create();
        var first = d.NonPoExerciseInvoice(" v-7781 ");
        (await Assembler(d, [first]).BuildAsync(d.NonPoExerciseInvoice("V-7781"))).Transaction.IsDuplicate.Should().BeTrue();

        var may = new InvoiceDates(new(2026, 5, 20), new(2026, 5, 20), new(2026, 5, 20), new(2026, 6, 20));
        (await Assembler(d).BuildAsync(d.NonPoExerciseInvoice(dates: may))).PeriodIsOpen.Should().BeFalse();
    }

    [Fact]
    public async Task Approvals_of_the_active_cycle_and_their_baseline_are_included()
    {
        var d = SpringfieldData.Create();
        var inv = d.NonPoExerciseInvoice();
        var assembler = Assembler(d);
        var subject = await assembler.BuildAsync(inv);
        var rules = Domain.Validation.DomainServices.RuleResolution.Resolve(d.Rules, inv.Dates.Invoice);
        var eval = new Domain.Validation.DomainServices.ValidationPipeline(Domain.Validation.DomainServices.RuleCatalog.Default)
            .Evaluate(subject, rules, EvaluationTrigger.Approve, SpringfieldData.ClerkId, DateTimeOffset.UtcNow);
        inv.Submit(eval.Id);
        inv.RecordApproval(Domain.Payables.Entities.ApproverRole.DepartmentHead, "6000", UserId.New(), eval.Id, DateTimeOffset.UtcNow);

        var s = await Assembler(d, evaluations: [eval]).BuildAsync(inv);
        s.ActiveApprovals.Should().ContainSingle(a => a.Role == ApproverRole.DepartmentHead && a.Department == "6000");
        s.ApprovalBaseline.Should().Be(new ApprovalBaseline(1, rules.Fingerprint));
    }
}
```

`EnumMappingTests.cs` — для пар (`ChartOfAccounts.FundRestrictionCheck` ↔ `Validation.FundRestriction`), (`GrantEligibility` ↔ `GrantEligibilityResult`), (`GrantPolicy` ↔ `GrantRule`), (`Payables.ApproverRole` ↔ `Validation.ApproverRole`), (`ChartOfAccounts.BudgetControlMode` ↔ `Validation.BudgetControl`), (`FundType` ↔ `FundKind`, с отображением `Governmental→Governmental`, `Enterprise→Enterprise`) сравнить `Enum.GetNames` как множества — расхождение ловится тестом, а не в рантайме:
```csharp
[Theory]
[InlineData(typeof(Domain.ChartOfAccounts.Entities.FundRestrictionCheck), typeof(Domain.Validation.ValueObjects.FundRestriction))]
[InlineData(typeof(Domain.ChartOfAccounts.Entities.GrantEligibility), typeof(Domain.Validation.ValueObjects.GrantEligibilityResult))]
[InlineData(typeof(Domain.ChartOfAccounts.Entities.GrantPolicy), typeof(Domain.Validation.ValueObjects.GrantRule))]
[InlineData(typeof(Domain.Payables.Entities.ApproverRole), typeof(Domain.Validation.ValueObjects.ApproverRole))]
[InlineData(typeof(Domain.ChartOfAccounts.Entities.BudgetControlMode), typeof(Domain.Validation.ValueObjects.BudgetControl))]
[InlineData(typeof(Domain.ChartOfAccounts.Entities.FundType), typeof(Domain.Validation.ValueObjects.FundKind))]
[InlineData(typeof(Domain.ChartOfAccounts.Entities.BudgetControlMode), typeof(Domain.Ledger.Entities.BudgetControlMode))]
public void Enum_names_match(Type a, Type b) => Enum.GetNames(a).Should().BeEquivalentTo(Enum.GetNames(b));
```

- [ ] **Step 4: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Application.Web.Tests --filter "FullyQualifiedName~Assembler|FullyQualifiedName~EnumMapping"`

- [ ] **Step 5: Реализация**

`src/GovErp.Application.Web/Validation/ValidationSubjectAssembler.cs`:
```csharp
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

/// <summary>Единственное место, где встречаются четыре контекста (GE-9): читает агрегаты и строит снимки.</summary>
public sealed class ValidationSubjectAssembler(
    IFundRepository funds, IGrantRepository grants, IAccountCombinationRepository combinations,
    IBudgetLineRepository budgetLines, IEncumbranceRepository encumbrances, IFiscalPeriodRepository periods,
    IVendorRepository vendors, IPurchaseOrderRepository purchaseOrders, IVendorInvoiceRepository invoices,
    IEvaluationRecordRepository evaluations, IOptions<PostingOptions> posting)
{
    public async Task<ValidationSubject> BuildAsync(VendorInvoice invoice, CancellationToken ct = default)
    {
        var vendor = await vendors.FindAsync(invoice.VendorId, ct) ?? throw new NotFoundException($"Vendor {invoice.VendorId} not found.");
        var fy = FiscalYear.FromDate(invoice.Dates.Posting);
        var (py, pm) = FiscalPeriod.KeyFor(invoice.Dates.Posting);
        var period = await periods.FindAsync(py, pm, ct);
        var isDuplicate = await invoices.ExistsDuplicateAsync(invoice.VendorId, invoice.NormalizedNumber, invoice.Id, ct);

        var tx = new TransactionSnapshot(invoice.Reference, invoice.Id, invoice.ContentVersion, invoice.ApprovalCycleId, "AP_INVOICE",
            invoice.Dates.Invoice, invoice.Dates.Service, invoice.Dates.Posting, invoice.Total,
            new VendorSnapshot(vendor.Id, vendor.Name, vendor.IsActive, vendor.Status == VendorStatus.Debarred, vendor.SamRegistered),
            invoice.PoNumber, isDuplicate, invoice.CreatedBy);

        var distributions = new List<DistributionSnapshot>();
        foreach (var d in invoice.Distributions)
        {
            distributions.Add(await DistributionAsync(invoice, d, ct));
        }

        var budgets = new List<BudgetSnapshot>();
        foreach (var key in invoice.Distributions.Select(d => d.Account).Distinct())
        {
            var line = await budgetLines.FindAsync(key, fy, ct);
            budgets.Add(line is null
                ? BudgetSnapshot.Missing(key)
                : new BudgetSnapshot(key, true, line.Amended, line.Actuals, line.Encumbered, line.Held, line.HeldFor(invoice.Id, invoice.ContentVersion)));
        }

        var poLines = await PoLinesAsync(invoice, ct);
        var approvals = invoice.ActiveApprovals.Select(a => new ApprovalSnapshot(RoleMapping.ToValidation(a.Role), a.Department)).ToList();
        var overrides = invoice.ActiveOverrides
            .Select(o => new OverrideSnapshot(o.EvaluationId, o.RuleId, o.RuleVersion, o.DistributionLine, o.UserId, o.Reason)).ToList();

        ApprovalBaseline? baseline = null;
        if (invoice.ActiveApprovals.LastOrDefault() is { EvaluationId: { } evalId }
            && await evaluations.FindAsync(evalId, ct) is { } basis)
        {
            baseline = new ApprovalBaseline(basis.ContentVersion, basis.RuleSetFingerprint);
        }

        return new ValidationSubject(tx, distributions, budgets, poLines, approvals, overrides,
            period?.IsOpen ?? false, baseline, posting.Value.ToPostingAccounts());
    }

    private async Task<DistributionSnapshot> DistributionAsync(VendorInvoice invoice, InvoiceDistribution d, CancellationToken ct)
    {
        var fund = await funds.FindAsync(d.Account.Fund, ct);
        var combination = await combinations.FindAsync(d.Account, ct);
        var grant = d.Account.Grant is null ? null : await grants.FindAsync(d.Account.Grant, ct);

        FundSnapshot? fundSnapshot = fund is null ? null : new(fund.Code.Value, fund.Name,
            Enum.Parse<FundKind>(fund.Type.ToString()),
            Enum.Parse<BudgetControl>(fund.ControlMode.ToString()),
            Enum.Parse<GrantRule>(fund.GrantPolicy.ToString()),
            Enum.Parse<FundRestriction>(fund.Check(d.Account.Department, d.Account.Object).ToString()),
            fund.IsActive);

        GrantSnapshot? grantSnapshot = d.Account.Grant is null ? null
            : grant is null ? new(d.Account.Grant.Value, false, GrantEligibilityResult.GrantNotActive, "Missing")
            : new(grant.Code.Value, grant.IsFederal,
                Enum.Parse<GrantEligibilityResult>(grant.CheckEligibility(invoice.Dates.Service, d.Account.Department, d.Account.Object).ToString()),
                grant.Status.ToString());

        var combinationSnapshot = combination is null
            ? new CombinationSnapshot(false, false, "Missing")
            : new CombinationSnapshot(true, combination.IsActiveOn(invoice.Dates.Invoice), combination.Status.ToString());

        var poLineRef = d.PoLineNo is { } no && invoice.PoNumber is { } po ? $"{po}/{no}" : null;
        return new DistributionSnapshot(d.LineNo, d.Account, d.Amount, poLineRef, combinationSnapshot, fundSnapshot, grantSnapshot);
    }

    private async Task<IReadOnlyList<PoLineSnapshot>> PoLinesAsync(VendorInvoice invoice, CancellationToken ct)
    {
        if (invoice.PoNumber is null)
        {
            return [];
        }

        var po = await purchaseOrders.FindByNumberAsync(invoice.PoNumber, ct);
        if (po is null)
        {
            return [];   // правило PO_LIQUIDATION сообщит «PO line not found»
        }

        var result = new List<PoLineSnapshot>();
        foreach (var no in invoice.Distributions.Where(d => d.PoLineNo is not null).Select(d => d.PoLineNo!.Value).Distinct())
        {
            var line = po.Lines.SingleOrDefault(l => l.LineNo == no);
            if (line is null)
            {
                continue;
            }

            var lineRef = po.LineRef(no);
            var enc = await encumbrances.FindByPoLineAsync(lineRef, ct);
            var own = enc?.ClaimOf(invoice.Id, invoice.ContentVersion)?.Amount ?? Money.Zero;
            EncumbranceSnapshot? encSnapshot = enc is null ? null
                : new(enc.Remaining, enc.HeldClaims - own, enc.Status == EncumbranceStatus.Open);
            result.Add(new PoLineSnapshot(lineRef, line.Account, po.Status == PurchaseOrderStatus.Open,
                line.AuthorizedAmount, line.PostedAmount, po.OtherActiveBillingClaims(no, invoice.Id), encSnapshot));
        }

        return result;
    }
}
```

- [ ] **Step 6: Прогнать**

Run: `dotnet test tests/GovErp.Application.Web.Tests --filter "FullyQualifiedName~Assembler|FullyQualifiedName~EnumMapping|FullyQualifiedName~CommandResult"`
Expected: 14 passed (1 + 6 + 7).

- [ ] **Step 7: Commit**

```bash
git add -A
git commit -m "Application: Springfield seed data and validation subject assembler

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: Infrastructure — EF-модель, схемы, миграции, Master

**Files:**
- Modify: `Directory.Packages.props`, `src/GovErp.Infrastructure/GovErp.Infrastructure.csproj`
- Create: `Persistence/Conversions.cs`, `JsonColumn.cs`, `GovErpDbContext.cs`, `GovErpDbContextFactory.cs`, `Persistence/Configurations/{Coa,Ledger,Ap,Validation,Audit}/*Configuration.cs`
- Create: `Master/Tenant.cs`, `UserAccount.cs`, `MasterDbContext.cs`, `MasterDbContextFactory.cs`
- Generate: `Persistence/Migrations/*_Initial*`, `Master/Migrations/*_Initial*`
- Test: `tests/GovErp.Application.Web.Tests/JsonRoundTripTests.cs`

**Interfaces:**
- Produces: `GovErpDbContext` с `DbSet` для `Fund, Department, ObjectCodeDefinition, Grant, AccountCombination, OpeningBalance, BudgetLine, Encumbrance, JournalEntry, FiscalPeriod, Vendor, PurchaseOrder, VendorInvoice, RuleDefinition, EvaluationRecord, ExplanationRecord, AuditEvent, CommandReceipt`; `MasterDbContext` с `Tenants`, `Users`; `JsonColumn.Options` (используют аудит и receipts).
- Модель (spec §6.2): FK только внутри схемы; `rowversion` на `BudgetLines`, `Encumbrances`, `PurchaseOrders`, `VendorInvoices`; `ChangeStamp` — обычная колонка (домен меняет её при любом изменении owned-коллекций, что гарантирует UPDATE корня); резервы, claims, billing claims, распределения, согласования, overrides, строки журнала — **owned-таблицы**, а не JSON (по ним ищут: `ListHeldForInvoiceAsync`, `ListClaimedByInvoiceAsync`); уникальные индексы `ap.VendorInvoices (VendorId, NormalizedNumber)`, `ledger.JournalEntries (SourceRef, Kind)`, `ap.ProcessedCommands (CommandId)`, `ledger.BudgetLines (Account, FiscalYear)`, `ledger.Encumbrances (PoLineRef)`, `ap.PurchaseOrders (Number)`, `validation.RuleDefinitions (RuleId, Layer, Version)`.

- [ ] **Step 1: Пакеты**

`Directory.Packages.props` — добавить (последняя 10.0.x / 4.x):
```xml
<PackageVersion Include="Microsoft.EntityFrameworkCore.SqlServer" Version="10.0.0" />
<PackageVersion Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.0" />
<PackageVersion Include="Microsoft.Extensions.DependencyInjection" Version="10.0.0" />
<PackageVersion Include="Microsoft.Extensions.Options.ConfigurationExtensions" Version="10.0.0" />
<PackageVersion Include="Microsoft.Extensions.Identity.Core" Version="10.0.0" />
<PackageVersion Include="Testcontainers.MsSql" Version="4.3.0" />
```
`GovErp.Infrastructure.csproj`:
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.EntityFrameworkCore.SqlServer" />
  <PackageReference Include="Microsoft.EntityFrameworkCore.Design"><PrivateAssets>all</PrivateAssets></PackageReference>
  <PackageReference Include="Microsoft.Extensions.Identity.Core" />
  <PackageReference Include="Microsoft.Extensions.Options.ConfigurationExtensions" />
</ItemGroup>
```
`dotnet tool install --global dotnet-ef` (или `dotnet tool update --global dotnet-ef`).

- [ ] **Step 2: Конверсии и JSON**

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

public static class JsonColumn
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter(), new MoneyJsonConverter(), new AccountCodeJsonConverter() },
    };

    public static PropertyBuilder<T> AsJson<T>(this PropertyBuilder<T> builder) =>
        builder.HasConversion(
                new ValueConverter<T, string>(v => JsonSerializer.Serialize(v, Options), s => JsonSerializer.Deserialize<T>(s, Options)!),
                new ValueComparer<T>(
                    (a, b) => JsonSerializer.Serialize(a, Options) == JsonSerializer.Serialize(b, Options),
                    v => JsonSerializer.Serialize(v, Options).GetHashCode(),
                    v => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Options), Options)!))
            .HasColumnType("nvarchar(max)");

    private sealed class MoneyJsonConverter : JsonConverter<Money>
    {
        public override Money Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => new(r.GetDecimal());
        public override void Write(Utf8JsonWriter w, Money v, JsonSerializerOptions o) => w.WriteNumberValue(v.Amount);
    }

    private sealed class AccountCodeJsonConverter : JsonConverter<AccountCode>
    {
        public override AccountCode Read(ref Utf8JsonReader r, Type t, JsonSerializerOptions o) => AccountCode.Parse(r.GetString()!);
        public override void Write(Utf8JsonWriter w, AccountCode v, JsonSerializerOptions o) => w.WriteStringValue(v.ToString());
    }
}
```

`tests/GovErp.Application.Web.Tests/JsonRoundTripTests.cs` — снимок и запись оценки переживают сериализацию (они хранятся JSON-колонками):
```csharp
using System.Text.Json;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Persistence;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.Options;

namespace GovErp.Application.Web.Tests;

public class JsonRoundTripTests
{
    [Fact]
    public async Task Validation_subject_and_outcomes_round_trip()
    {
        var d = SpringfieldData.Create();
        var inv = d.PoBackedInvoice(164_800m);
        var subject = await new Validation.ValidationSubjectAssembler(
            new InMemoryFundRepository(d.Funds), new InMemoryGrantRepository(d.Grants), new InMemoryAccountCombinationRepository(d.Combinations),
            new InMemoryBudgetLineRepository(d.BudgetLines), new InMemoryEncumbranceRepository(d.Encumbrances),
            new InMemoryFiscalPeriodRepository(d.Periods), new InMemoryVendorRepository(d.Vendors),
            new InMemoryPurchaseOrderRepository(d.PurchaseOrders), new InMemoryVendorInvoiceRepository([]),
            new InMemoryEvaluationRecordRepository([]), Options.Create(new Validation.PostingOptions())).BuildAsync(inv);
        var record = new ValidationPipeline(RuleCatalog.Default)
            .Evaluate(subject, RuleResolution.Resolve(d.Rules, inv.Dates.Invoice), EvaluationTrigger.Manual, SpringfieldData.ClerkId, DateTimeOffset.UtcNow);

        var back = JsonSerializer.Deserialize<ValidationSubject>(JsonSerializer.Serialize(subject, JsonColumn.Options), JsonColumn.Options)!;
        back.Should().BeEquivalentTo(subject);
        back.RequiredNewBudget(AccountCode.Parse("701-3000-53100-G-COPS-26")).Should().Be(Money.Of(4_800m));

        var outcomes = JsonSerializer.Deserialize<List<RuleOutcome>>(JsonSerializer.Serialize(record.Outcomes, JsonColumn.Options), JsonColumn.Options)!;
        outcomes.Should().BeEquivalentTo(record.Outcomes);
    }
}
```
Если STJ не сопоставит параметр конструктора наследника `SegmentCode` (`value`) со свойством `Value` — добавить по конвертеру на тип кода, как `AccountCodeJsonConverter`.

- [ ] **Step 3: DbContext**

`Persistence/GovErpDbContext.cs`:
```csharp
using GovErp.Application.Web.Audit;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Explanation;
using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Validation.Entities;
using Microsoft.EntityFrameworkCore;

namespace GovErp.Infrastructure.Persistence;

public sealed class GovErpDbContext(DbContextOptions<GovErpDbContext> options) : DbContext(options)
{
    public DbSet<Fund> Funds => Set<Fund>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ObjectCodeDefinition> ObjectCodes => Set<ObjectCodeDefinition>();
    public DbSet<Grant> Grants => Set<Grant>();
    public DbSet<AccountCombination> AccountCombinations => Set<AccountCombination>();
    public DbSet<OpeningBalance> OpeningBalances => Set<OpeningBalance>();
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
    public DbSet<CommandReceipt> CommandReceipts => Set<CommandReceipt>();

    protected override void OnModelCreating(ModelBuilder b) =>
        b.ApplyConfigurationsFromAssembly(typeof(GovErpDbContext).Assembly,
            t => t.Namespace?.StartsWith("GovErp.Infrastructure.Persistence.Configurations", StringComparison.Ordinal) == true);
}
```

- [ ] **Step 4: Конфигурации**

Общие приёмы: `ToTable("X", "schema")`; перечисления — `HasConversion<string>().HasMaxLength(30)`; `Money` — `HasConversion(Conversions.Money).HasPrecision(18, 2)`; коллекции с приватным полем — `b.Navigation(x => x.Prop).HasField("_field").UsePropertyAccessMode(PropertyAccessMode.Field)`; вычисляемые свойства — `Ignore`.

Образец — `Configurations/Coa/FundConfiguration.cs`:
```csharp
using GovErp.Domain.ChartOfAccounts.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace GovErp.Infrastructure.Persistence.Configurations.Coa;

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
        b.Property(x => x.AllowedDepartments).HasConversion(CodeList.Converter<DepartmentCode>(s => new DepartmentCode(s)), CodeList.Comparer<DepartmentCode>()).HasMaxLength(500);
        b.Property(x => x.AllowedObjects).HasConversion(CodeList.Converter<ObjectCode>(s => new ObjectCode(s)), CodeList.Comparer<ObjectCode>()).HasMaxLength(500);
    }
}
```

`Persistence/CodeList.cs` (список кодов сегментов ↔ строка «a,b,c»):
```csharp
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace GovErp.Infrastructure.Persistence;

internal static class CodeList
{
    public static ValueConverter<IReadOnlyList<T>, string> Converter<T>(Func<string, T> parse) where T : SegmentCode =>
        new(v => string.Join(',', v.Select(c => c.Value)),
            s => s.Length == 0 ? new List<T>() : s.Split(',', StringSplitOptions.None).Select(parse).ToList());

    public static ValueComparer<IReadOnlyList<T>> Comparer<T>() =>
        new((a, b) => a!.SequenceEqual(b!), v => v.Aggregate(0, (h, x) => HashCode.Combine(h, x)), v => v.ToList());
}
```

Остальные конфигурации — по образцу, точные требования:

| Тип | Таблица | Ключ | Особенности |
|---|---|---|---|
| `Department` | `coa.Departments` | `Code` | конверсия `Department` |
| `ObjectCodeDefinition` | `coa.ObjectCodes` | `Code` | `Category` string |
| `Grant` | `coa.Grants` | `Code` | `OwnsOne(Period)` → колонки `PeriodFrom`, `PeriodTo`; списки — `CodeList`; `Status` string |
| `AccountCombination` | `coa.AccountCombinations` | `Id` | `Code` — `Account`, `HasMaxLength(64)`, уникальный индекс; `CreatedBy`/`ApprovedBy` — `User`; `Status`, `Source` string |
| `OpeningBalance` | `ledger.OpeningBalances` | `Id` | `Account`, `FiscalYear`; `InitialActuals`, `InitialEncumbered` — Money; уникальный индекс `(Account, FiscalYear)` |
| `BudgetLine` | `ledger.BudgetLines` | `Id` | см. ниже |
| `Encumbrance` | `ledger.Encumbrances` | `Id` | уникальный `PoLineRef`; `Account`, `FiscalYear`, `Original`, `Liquidated`, `Released`; `Claims` — `OwnsMany` в `ledger.EncumbranceClaims` (ключ `Id`, `InvoiceId` + индекс, `ContentVersion`, `Amount`, `Status`), `HasField("_claims")`; `Ignore(Remaining, HeldClaims)`; `RowVersion` |
| `JournalEntry` | `ledger.JournalEntries` | `Id` | `Kind` string; `PostingDate`; `PostedBy` — `User`; уникальный `(SourceRef, Kind)`; `Lines` — `OwnsMany` в `ledger.JournalLines` (shadow `Id`, `Account`, `Family` string, `Debit`/`Credit` — Money), `HasField("_lines")` |
| `FiscalPeriod` | `ledger.FiscalPeriods` | `(Year, Month)` | `Status` string; `Ignore(IsOpen)` |
| `Vendor` | `ap.Vendors` | `Id` | `Status` string; `Ignore(IsActive)` |
| `PurchaseOrder` | `ap.PurchaseOrders` | `Id` | уникальный `Number`; `Status` string; `RowVersion`; `Lines` — `OwnsMany` в `ap.PurchaseOrderLines` (shadow `Id`, `LineNo`, `Account`, `AuthorizedAmount`, `PostedAmount`), внутри — `OwnsMany(l => l.BillingClaims)` в `ap.PoBillingClaims` (ключ `Id`, `InvoiceId` + индекс, `ContentVersion`, `Amount`, `Status`), поля `_lines` и `_billingClaims` |
| `VendorInvoice` | `ap.VendorInvoices` | `Id` | см. ниже |
| `RuleDefinition` | `validation.RuleDefinitions` | `Id` | `Parameters`, `OverridableBy` — `AsJson()`; `Step`/`Layer`/`Severity` string; `Ignore(CanonicalParameters)`; уникальный `(RuleId, Layer, Version)` |
| `EvaluationRecord` | `validation.EvaluationRecords` | `Id` | `AppliedRules`, `Outcomes`, `Capabilities`, `ApprovalRoute`, `PostingPreview`, `PostingCheck`, `InputSnapshot` — `AsJson()`; `Trigger`/`Overall` string; `EvaluatedBy` — `User`; индексы `TransactionRef`, `InvoiceId` |
| `ExplanationRecord` | `validation.Explanations` | `Id` | `Audience` string; индекс `EvaluationId` |
| `AuditEvent` | `audit.Events` | `Id` | `TenantId` — `Tenant`, `Actor` — `User`; `PayloadJson` nvarchar(max); индекс `SubjectRef` |
| `CommandReceipt` | `ap.ProcessedCommands` | `CommandId` | `ActorId` — `User`; `RequestHash` nvarchar(64); `ResultJson` nvarchar(max) |

`Configurations/Ledger/BudgetLineConfiguration.cs`:
```csharp
public void Configure(EntityTypeBuilder<BudgetLine> b)
{
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
        o.HasIndex(r => r.InvoiceId);
        o.Property(r => r.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
        o.Property(r => r.Status).HasConversion<string>().HasMaxLength(10);
    });
    b.Navigation(x => x.Reservations).HasField("_reservations").UsePropertyAccessMode(PropertyAccessMode.Field);
}
```

`Configurations/Ap/VendorInvoiceConfiguration.cs`:
```csharp
public void Configure(EntityTypeBuilder<VendorInvoice> b)
{
    b.ToTable("VendorInvoices", "ap");
    b.HasKey(x => x.Id);
    b.Property(x => x.Number).HasMaxLength(50);
    b.Property(x => x.NormalizedNumber).HasMaxLength(50);
    b.HasIndex(x => new { x.VendorId, x.NormalizedNumber }).IsUnique().HasDatabaseName("UX_VendorInvoices_Vendor_Number");
    b.OwnsOne(x => x.Dates, o =>
    {
        o.Property(d => d.Invoice).HasColumnName("InvoiceDate");
        o.Property(d => d.Service).HasColumnName("ServiceDate");
        o.Property(d => d.Posting).HasColumnName("PostingDate");
        o.Property(d => d.Due).HasColumnName("DueDate");
    });
    b.Property(x => x.Total).HasConversion(Conversions.Money).HasPrecision(18, 2);
    b.Property(x => x.PoNumber).HasMaxLength(50);
    b.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
    b.Property(x => x.CreatedBy).HasConversion(Conversions.User);
    b.Property<byte[]>("RowVersion").IsRowVersion();
    b.Ignore(x => x.Reference).Ignore(x => x.IsPoBacked).Ignore(x => x.DistributedTotal)
     .Ignore(x => x.ActiveApprovals).Ignore(x => x.ActiveOverrides);

    b.OwnsMany(x => x.Distributions, o =>
    {
        o.ToTable("InvoiceDistributions", "ap");
        o.WithOwner().HasForeignKey("InvoiceId");
        o.Property<int>("Id").ValueGeneratedOnAdd();
        o.HasKey("Id");
        o.Property(d => d.Account).HasConversion(Conversions.Account).HasMaxLength(64);
        o.Property(d => d.Amount).HasConversion(Conversions.Money).HasPrecision(18, 2);
    });
    b.Navigation(x => x.Distributions).HasField("_distributions").UsePropertyAccessMode(PropertyAccessMode.Field);

    b.OwnsMany(x => x.Approvals, o =>
    {
        o.ToTable("InvoiceApprovals", "ap");
        o.WithOwner().HasForeignKey("InvoiceId");
        o.Property<int>("Id").ValueGeneratedOnAdd();
        o.HasKey("Id");
        o.Property(a => a.Role).HasConversion<string>().HasMaxLength(30);
        o.Property(a => a.Decision).HasConversion<string>().HasMaxLength(10);
        o.Property(a => a.UserId).HasConversion(Conversions.User);
        o.Property(a => a.Department).HasMaxLength(4);
        o.Property(a => a.Reason).HasMaxLength(1000);
    });
    b.Navigation(x => x.Approvals).HasField("_approvals").UsePropertyAccessMode(PropertyAccessMode.Field);

    b.OwnsMany(x => x.Overrides, o =>
    {
        o.ToTable("InvoiceOverrides", "ap");
        o.WithOwner().HasForeignKey("InvoiceId");
        o.Property<int>("Id").ValueGeneratedOnAdd();
        o.HasKey("Id");
        o.Property(v => v.RuleId).HasMaxLength(50);
        o.Property(v => v.UserId).HasConversion(Conversions.User);
        o.Property(v => v.Reason).HasMaxLength(1000);
    });
    b.Navigation(x => x.Overrides).HasField("_overrides").UsePropertyAccessMode(PropertyAccessMode.Field);
}
```

`ReplaceContent` очищает и заново заполняет `_distributions`: EF удалит старые owned-строки и вставит новые — это ожидаемо (содержание Draft изменилось, ContentVersion вырос).

- [ ] **Step 5: Master**

```csharp
namespace GovErp.Infrastructure.Master;

public sealed class Tenant
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string DatabaseName { get; set; } = "";
    /// <summary>Ключ секции конфигурации с учётными данными runtime-пользователя; сам секрет в БД не хранится.</summary>
    public string CredentialKey { get; set; } = "";
    public bool IsDemo { get; set; }
}

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
Анемичные классы с публичными сеттерами — инфраструктурная модель, не домен (`DDD-7`).

Фабрики для `dotnet ef` (`IDesignTimeDbContextFactory<...>`): `UseSqlServer("Server=localhost;Database=GovErp_Design;Trusted_Connection=True;TrustServerCertificate=True")` — строка используется только генератором миграций.

- [ ] **Step 6: Миграции**

```powershell
dotnet ef migrations add Initial --project src/GovErp.Infrastructure --startup-project src/GovErp.Infrastructure --context GovErpDbContext --output-dir Persistence/Migrations
dotnet ef migrations add Initial --project src/GovErp.Infrastructure --startup-project src/GovErp.Infrastructure --context MasterDbContext --output-dir Master/Migrations
```
Проверить сгенерированную миграцию `GovErpDbContext`: пять `EnsureSchema` (`coa`, `ledger`, `ap`, `validation`, `audit`); `rowversion` на четырёх таблицах; все уникальные индексы из блока Interfaces; FK только внутри схем (owned-таблицы → свой корень).

- [ ] **Step 7:** `dotnet build && dotnet test tests/GovErp.Application.Web.Tests --filter JsonRoundTrip` — 1 passed.
- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "Infrastructure: EF model with schema per context, owned claims and reservations, unique indexes, master context, migrations

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Infrastructure — runner команд, receipts, репозитории, tenancy, sign-in, шаблонное объяснение

**Files:**
- Create: `Operations/EfTenantOperationRunner.cs`, `Persistence/EfCommandReceipts.cs`, `Persistence/EfConcurrencyGuard.cs`, `Persistence/AppendOnlyInterceptor.cs`, `Persistence/Repositories/Ef*Repository.cs` (14 файлов), `Audit/EfAuditTrail.cs`, `Explanation/EfExplanationRepository.cs`, `Explanation/TemplateExplanationGenerator.cs`, `Tenancy/TenancyOptions.cs`, `Tenancy/TenantContext.cs`, `Tenancy/MasterTenantCatalog.cs`, `Identity/MasterSignIn.cs`, `Common/SystemClock.cs`, `Common/ClockOptions.cs`, `Extensions/ServiceCollectionExtensions.cs`
- Test: `tests/GovErp.Application.Web.Tests/TemplateExplanationTests.cs` (поведение runner проверяется на SQL Server в задачах 8–9)

**Interfaces:**
- Produces: `services.AddInfrastructure(IConfiguration)`; `TenancyOptions` (`RuntimeConnectionTemplate` = `"Server=…;Database={0};User Id={1};Password={2};TrustServerCertificate=True"`, `MigrationConnectionTemplate` = `"Server=…;Database={0};User Id=sa;Password=…;TrustServerCertificate=True"`, `Credentials` — словарь `CredentialKey → (User, Password)`); `ClockOptions.BusinessDate` (по умолчанию `2026-06-15`).
- Runner (Application `ITenantOperationRunner`):
  1. новый async-scope; тенант — `ITenantCatalog.FindAsync(actor.TenantId)`, строка подключения runtime-пользователя; `TenantContext.Initialize`;
  2. `BeginTransactionAsync(isolation)`;
  3. receipt по `CommandId`: чужой актор → `Forbidden`; другой тип/хеш запроса → `Conflict` **без** retry; тот же → сохранённый результат;
  4. тело; receipt с результатом (в т.ч. `Refused`); один `SaveChangesAsync`; `CommitAsync`;
  5. `DbUpdateConcurrencyException` или deadlock/serialization failure (SQL 1205, 3960) на первой попытке → откат, новый scope, повтор; на второй — `Conflict` retryable;
  6. нарушение `UX_VendorInvoices_Vendor_Number` → `Refused` «invoice number already exists for this vendor»; нарушение PK `ProcessedCommands` (две вкладки с одним CommandId) → повтор, который вернёт сохранённый receipt; нарушение уникальности журнала → `Conflict` без retry;
  7. `AuthorizationException` → `Forbidden`; `NotFoundException` → `NotFound`; доменные исключения (`PayablesException`, `LedgerException`, `ValidationException`) → `Refused` с сообщением; всё несохранённое откатывается.
- `EfConcurrencyGuard.Expect(aggregate, base64)` выставляет `OriginalValue` теневого `RowVersion`: устаревшая форма даёт `DbUpdateConcurrencyException` → `Conflict`.

- [ ] **Step 1: Репозитории**

По одному `Ef*Repository` на порт планов 1–2 (14: Fund, Grant, AccountCombination, ReferenceData, OpeningBalance, BudgetLine, Encumbrance, Journal, FiscalPeriod, Vendor, PurchaseOrder, VendorInvoice, RuleDefinition, EvaluationRecord). Правила: owned-коллекции загружаются автоматически (EF включает owned-сущности); `Add*` — `db.Set<T>().AddAsync`; ни один репозиторий не вызывает `SaveChanges`. Нетривиальные запросы:
```csharp
// EfBudgetLineRepository
public async Task<IReadOnlyList<BudgetLine>> ListHeldForInvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
    await db.BudgetLines.Where(l => l.Reservations.Any(r => r.InvoiceId == invoiceId && r.Status == ReservationStatus.Held))
        .OrderBy(l => l.Id).ToListAsync(ct);   // стабильный порядок блокировок

// EfEncumbranceRepository
public async Task<IReadOnlyList<Encumbrance>> ListClaimedByInvoiceAsync(Guid invoiceId, CancellationToken ct = default) =>
    await db.Encumbrances.Where(e => e.Claims.Any(c => c.InvoiceId == invoiceId && c.Status == ClaimStatus.Held))
        .OrderBy(e => e.PoLineRef).ToListAsync(ct);

// EfVendorInvoiceRepository — предварительная проверка; гонку закрывает уникальный индекс
public Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid excludingInvoiceId, CancellationToken ct = default) =>
    db.VendorInvoices.AnyAsync(i => i.VendorId == vendorId && i.NormalizedNumber == normalizedNumber && i.Id != excludingInvoiceId, ct);

// EfJournalRepository
public Task<bool> ExistsAsync(string sourceRef, PostingKind kind, CancellationToken ct = default) =>
    db.JournalEntries.AnyAsync(j => j.SourceRef == sourceRef && j.Kind == kind, ct);

// EfEvaluationRecordRepository
public async Task<IReadOnlyList<EvaluationRecord>> ListByTransactionAsync(string transactionRef, CancellationToken ct = default) =>
    await db.EvaluationRecords.Where(e => e.TransactionRef == transactionRef).OrderBy(e => e.EvaluatedAt).ToListAsync(ct);
```

- [ ] **Step 2: Receipts, concurrency guard, append-only**

```csharp
public sealed class EfCommandReceipts(GovErpDbContext db) : ICommandReceipts
{
    public Task<CommandReceipt?> FindAsync(Guid commandId, CancellationToken ct = default) =>
        db.CommandReceipts.SingleOrDefaultAsync(r => r.CommandId == commandId, ct);
    public void Add(CommandReceipt receipt) => db.CommandReceipts.Add(receipt);
}

public sealed class EfConcurrencyGuard(GovErpDbContext db) : IConcurrencyGuard
{
    public void Expect(object aggregate, string? rowVersion)
    {
        if (rowVersion is not null)
        {
            db.Entry(aggregate).Property("RowVersion").OriginalValue = Convert.FromBase64String(rowVersion);
        }
    }

    public string? VersionOf(object aggregate) =>
        db.Entry(aggregate).Property("RowVersion").CurrentValue is byte[] v ? Convert.ToBase64String(v) : null;
}

public sealed class AppendOnlyInterceptor : SaveChangesInterceptor
{
    private static readonly Type[] AppendOnly = [typeof(EvaluationRecord), typeof(ExplanationRecord), typeof(JournalEntry), typeof(AuditEvent)];

    public override InterceptionResult<int> SavingChanges(DbContextEventData e, InterceptionResult<int> r) { Check(e.Context!); return r; }
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData e, InterceptionResult<int> r, CancellationToken ct = default)
    { Check(e.Context!); return ValueTask.FromResult(r); }

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

- [ ] **Step 3: Runner**

`Operations/EfTenantOperationRunner.cs`:
```csharp
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Tenancy;
using GovErp.Domain.Ledger.Exceptions;
using GovErp.Domain.Payables.Exceptions;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Operations;

public sealed class EfTenantOperationRunner(IServiceScopeFactory scopes) : ITenantOperationRunner
{
    public async Task<CommandResult<T>> ExecuteAsync<T>(ActorContext actor, CommandEnvelope envelope, string commandType, object request,
        Func<IServiceProvider, CancellationToken, Task<CommandResult<T>>> body, IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        var requestHash = Hash(commandType, request);
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            if (!await InitializeTenantAsync(sp, actor, ct))
            {
                return CommandResult<T>.NotFound($"Tenant {actor.TenantId} is not registered.");
            }

            var db = sp.GetRequiredService<GovErpDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            await using var tx = await db.Database.BeginTransactionAsync(isolation, ct);
            try
            {
                var receipts = sp.GetRequiredService<ICommandReceipts>();
                if (await receipts.FindAsync(envelope.CommandId, ct) is { } existing)
                {
                    if (existing.ActorId != actor.UserId)
                    {
                        return CommandResult<T>.Forbidden("This command id belongs to another user.");
                    }

                    return existing.CommandType == commandType && existing.RequestHash == requestHash
                        ? JsonSerializer.Deserialize<CommandResult<T>>(existing.ResultJson, JsonColumn.Options)!
                        : new CommandResult<T>(CommandStatus.Conflict, default, "This command id was already used for a different request.", false);
                }

                var result = await body(sp, ct);
                receipts.Add(new CommandReceipt(envelope.CommandId, actor.UserId, commandType, requestHash,
                    JsonSerializer.Serialize(result, JsonColumn.Options), clock.Now));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            catch (Exception ex) when (IsTransient(ex) && attempt == 1)
            {
                // rollback — при dispose транзакции; повтор в новом scope на свежих данных
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                return CommandResult<T>.Conflict("The data was changed by another user. Reload and try again.");
            }
            catch (DbUpdateException ex) when (UniqueIndexOf(ex) is { } index)
            {
                if (index.Contains("ProcessedCommands", StringComparison.OrdinalIgnoreCase) && attempt == 1)
                {
                    continue;   // параллельный дубль той же команды: следующая попытка вернёт сохранённый receipt
                }

                return index.Contains("UX_VendorInvoices_Vendor_Number", StringComparison.OrdinalIgnoreCase)
                    ? CommandResult<T>.Refused(default, "An invoice with this number already exists for this vendor.")
                    : new CommandResult<T>(CommandStatus.Conflict, default, $"The operation was already applied ({index}).", false);
            }
            catch (AuthorizationException ex) { return CommandResult<T>.Forbidden(ex.Message); }
            catch (NotFoundException ex) { return CommandResult<T>.NotFound(ex.Message); }
            catch (Exception ex) when (ex is PayablesException or LedgerException or ValidationException or DuplicateKeyException)
            {
                return CommandResult<T>.Refused(default, ex.Message);
            }
        }
    }

    public async Task<T> QueryAsync<T>(ActorContext actor, Func<IServiceProvider, CancellationToken, Task<T>> body, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (!await InitializeTenantAsync(scope.ServiceProvider, actor, ct))
        {
            throw new NotFoundException($"Tenant {actor.TenantId} is not registered.");
        }

        return await body(scope.ServiceProvider, ct);
    }

    private static async Task<bool> InitializeTenantAsync(IServiceProvider sp, ActorContext actor, CancellationToken ct)
    {
        var catalog = sp.GetRequiredService<ITenantCatalog>();
        var tenant = await catalog.FindAsync(actor.TenantId, ct);
        if (tenant is null)
        {
            return false;
        }

        sp.GetRequiredService<ITenantContextInitializer>().Initialize(tenant.Id, catalog.RuntimeConnectionString(tenant));
        return true;
    }

    private static bool IsTransient(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex.GetBaseException() is SqlException { Number: 1205 or 3960 };   // deadlock victim, snapshot/serializable conflict

    private static string? UniqueIndexOf(DbUpdateException ex) =>
        ex.GetBaseException() is SqlException { Number: 2601 or 2627 } sql ? sql.Message : null;

    private static string Hash(string commandType, object request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commandType + "|" + JsonSerializer.Serialize(request, request.GetType(), JsonColumn.Options))))
            .ToLowerInvariant();
}
```
Первые два `catch` с фильтром `IsTransient` стоят раньше `DbUpdateException`: `DbUpdateConcurrencyException` — её наследник. Цикл `for (;;)` завершается: каждая ветка второй попытки возвращает результат.

- [ ] **Step 4: Tenancy, sign-in, часы, аудит, объяснения**

```csharp
public sealed class TenancyOptions
{
    public string RuntimeConnectionTemplate { get; set; } = "";
    public string MigrationConnectionTemplate { get; set; } = "";
    public Dictionary<string, TenantCredential> Credentials { get; set; } = [];
}
public sealed class TenantCredential { public string User { get; set; } = ""; public string Password { get; set; } = ""; }

public sealed class TenantContext : ITenantContext, ITenantContextInitializer
{
    private TenantId? _tenantId;
    private string? _connectionString;
    public TenantId TenantId => _tenantId ?? throw new InvalidOperationException("Tenant context is not initialized.");
    public string ConnectionString => _connectionString ?? throw new InvalidOperationException("Tenant context is not initialized.");
    public bool IsInitialized => _tenantId is not null;

    /// <summary>Один раз на scope операции; повторная инициализация другим тенантом — ошибка.</summary>
    public void Initialize(TenantId tenantId, string connectionString)
    {
        if (_tenantId is not null && _tenantId != tenantId)
        {
            throw new InvalidOperationException("Tenant context is already initialized for another tenant.");
        }

        _tenantId = tenantId;
        _connectionString = connectionString;
    }
}

public sealed class MasterTenantCatalog(MasterDbContext master, IOptions<TenancyOptions> options) : ITenantCatalog
{
    public async Task<TenantInfo?> FindAsync(TenantId id, CancellationToken ct = default) =>
        await master.Tenants.Where(t => t.Id == id.Value)
            .Select(t => new TenantInfo(new TenantId(t.Id), t.Name, t.DatabaseName, t.IsDemo)).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<TenantInfo>> ListAsync(CancellationToken ct = default) =>
        await master.Tenants.OrderBy(t => t.Name).Select(t => new TenantInfo(new TenantId(t.Id), t.Name, t.DatabaseName, t.IsDemo)).ToListAsync(ct);

    public string RuntimeConnectionString(TenantInfo tenant)
    {
        var key = master.Tenants.Where(t => t.Id == tenant.Id.Value).Select(t => t.CredentialKey).Single();
        var credential = options.Value.Credentials.GetValueOrDefault(key)
            ?? throw new InvalidOperationException($"No runtime credential configured for tenant {tenant.Id}.");
        return string.Format(CultureInfo.InvariantCulture, options.Value.RuntimeConnectionTemplate, tenant.DatabaseName, credential.User, credential.Password);
    }
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

public sealed class ClockOptions { public DateOnly BusinessDate { get; set; } = new(2026, 6, 15); }
public sealed class SystemClock(IOptions<ClockOptions> options) : IClock
{
    public DateTimeOffset Now => DateTimeOffset.UtcNow;
    public DateOnly BusinessDate => options.Value.BusinessDate;
}
```

`EfAuditTrail(GovErpDbContext db, ITenantContext tenant, IClock clock)`: `Record` → `db.AuditEvents.Add(new AuditEvent(Guid.NewGuid(), tenant.TenantId, clock.Now, actor.UserId, actor.UserName, action, subjectRef, correlationId, JsonSerializer.Serialize(payload, JsonColumn.Options)))`; `ListBySubjectAsync` — по `SubjectRef`, сортировка по `OccurredAt`. `EfExplanationRepository` — `Add` / `ListByEvaluationAsync` (сортировка по `GeneratedAt`).

- [ ] **Step 5: Шаблонное объяснение — тест**

`tests/GovErp.Application.Web.Tests/TemplateExplanationTests.cs`:
```csharp
using GovErp.Application.Web.Explanation;
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;
using GovErp.Infrastructure.Explanation;
using GovErp.Infrastructure.Seed;
using Microsoft.Extensions.Options;

namespace GovErp.Application.Web.Tests;

public class TemplateExplanationTests
{
    private static async Task<Domain.Validation.Entities.EvaluationRecord> ExerciseRecord()
    {
        var d = SpringfieldData.Create();
        var inv = d.NonPoExerciseInvoice();
        var subject = await new Validation.ValidationSubjectAssembler(
            new InMemoryFundRepository(d.Funds), new InMemoryGrantRepository(d.Grants), new InMemoryAccountCombinationRepository(d.Combinations),
            new InMemoryBudgetLineRepository(d.BudgetLines), new InMemoryEncumbranceRepository(d.Encumbrances),
            new InMemoryFiscalPeriodRepository(d.Periods), new InMemoryVendorRepository(d.Vendors),
            new InMemoryPurchaseOrderRepository(d.PurchaseOrders), new InMemoryVendorInvoiceRepository([]),
            new InMemoryEvaluationRecordRepository([]), Options.Create(new Validation.PostingOptions())).BuildAsync(inv);
        return new ValidationPipeline(RuleCatalog.Default).Evaluate(subject, RuleResolution.Resolve(d.Rules, inv.Dates.Invoice),
            EvaluationTrigger.Manual, SpringfieldData.ClerkId, new DateTimeOffset(2026, 6, 15, 10, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task Auditor_text_explains_decision_funds_checks_and_versions()
    {
        var result = await new TemplateExplanationGenerator().ExplainAsync(await ExerciseRecord(), ExplanationAudience.Auditor);
        result.Provider.Should().Be("Template");
        result.PromptVersion.Should().Be("template-1");
        result.Text.Should().Contain("HARD STOP").And.Contain("13,000.00").And.Contain("701-6000-53100-G-COPS-26")
            .And.Contain("BUDGET_AVAILABILITY").And.Contain("engine-1.0.0").And.Contain("Budget amendment");
    }

    [Fact]
    public async Task Public_text_has_no_user_ids_or_raw_inputs()
    {
        var text = (await new TemplateExplanationGenerator().ExplainAsync(await ExerciseRecord(), ExplanationAudience.Public)).Text;
        text.Should().NotContain(SpringfieldData.ClerkId.ToString()).And.NotContain("amended=");
        text.Should().Contain("HARD STOP");
    }
}
```

- [ ] **Step 6: Шаблонное объяснение — реализация**

`Explanation/TemplateExplanationGenerator.cs` — детерминированный текст только из `EvaluationRecord` (`Provider = "Template"`, `Model = null`, `PromptVersion = "template-1"`). Структура (строки через `\n`, `StringBuilder`):
```text
Invoice {TransactionRef} (content v{ContentVersion}) for {Total} from {Vendor.Name} was evaluated on {EvaluatedAt:yyyy-MM-dd} ({Trigger}). Result: {HARD STOP|SOFT STOP|WARNING|ALLOWED}.
Funds charged: line {n} — {amount} to {account} ({fund name}, {hard|soft} budget control){, grant {code}}.
Checks performed: steps 1–{last executed step}; {k} rules applied.
Findings: {RuleId} [{Severity}] — {Message} Required action: {Resolution}.   (по каждому outcome, кроме Allowed; для Allowed-информационных — одна строка без «Required action»)
Approvals required: {Role (dept)} …; recorded in this cycle: {…|none}.
Accounting entries to be created: {Dr account amount / Cr account amount …} | none until the stop is resolved.
Rule set: {EngineVersion}, fingerprint {first 12 chars}.
```
Аудитории: `Auditor` — плюс строки `inputs: k=v; …` и `computed: k=v; …` под каждым finding'ом и полный fingerprint; `FinanceUser` / `DepartmentManager` — без inputs, с сообщениями и resolution; `Public` — без имён и идентификаторов пользователей, без inputs, без fingerprint. «Последний выполненный шаг» = `max(Outcomes.Step)`, если среди outcome'ов есть HardStop, иначе 6.

- [ ] **Step 7: Регистрация**

`Extensions/ServiceCollectionExtensions.cs`:
```csharp
public static IServiceCollection AddInfrastructure(this IServiceCollection s, IConfiguration cfg)
{
    s.Configure<TenancyOptions>(cfg.GetSection("Tenancy"));
    s.Configure<ClockOptions>(cfg.GetSection("Clock"));
    s.AddDbContext<MasterDbContext>(o => o.UseSqlServer(cfg.GetConnectionString("Master")));
    s.AddScoped<TenantContext>();
    s.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
    s.AddScoped<ITenantContextInitializer>(sp => sp.GetRequiredService<TenantContext>());
    s.AddSingleton<AppendOnlyInterceptor>();
    s.AddDbContext<GovErpDbContext>((sp, o) => o
        .UseSqlServer(sp.GetRequiredService<ITenantContext>().ConnectionString)   // без EnableRetryOnFailure: повторы — забота runner'а
        .AddInterceptors(sp.GetRequiredService<AppendOnlyInterceptor>()));
    s.AddSingleton<ITenantOperationRunner, EfTenantOperationRunner>();
    s.AddScoped<ICommandReceipts, EfCommandReceipts>();
    s.AddScoped<IConcurrencyGuard, EfConcurrencyGuard>();
    s.AddScoped<IFundRepository, EfFundRepository>();
    s.AddScoped<IGrantRepository, EfGrantRepository>();
    s.AddScoped<IAccountCombinationRepository, EfAccountCombinationRepository>();
    s.AddScoped<IReferenceDataRepository, EfReferenceDataRepository>();
    s.AddScoped<IOpeningBalanceRepository, EfOpeningBalanceRepository>();
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
    s.AddScoped<IExplanationGenerator, TemplateExplanationGenerator>();   // план 4 заменит выбором провайдера
    return s;
}
```
`EnableRetryOnFailure` не включается: стратегия повторов EF несовместима с явной пользовательской транзакцией, а повтор команды целиком делает runner.

- [ ] **Step 8:** `dotnet build && dotnet test tests/GovErp.Application.Web.Tests --filter TemplateExplanation` — 2 passed.
- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Infrastructure: operation runner with receipts and retry, concurrency guard, repositories, tenancy, sign-in, template explanation

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Application — контракты и атомарные сценарии жизненного цикла

**Files:**
- Create: `Validation/Contracts/EvaluationVm.cs`, `Validation/EvaluationMapping.cs`
- Create: `Invoices/Contracts/{InvoiceVm,DistributionVm,ApprovalVm,OverrideVm,InvoiceListItemVm}.cs`, `Invoices/Commands/{DistributionCommand,CreateInvoiceCommand,UpdateInvoiceCommand,InvoiceActionCommand,ReasonedActionCommand,OverrideCommand,PaymentHoldCommand,InvoicePreset}.cs`, `Invoices/Mapping/InvoiceMapping.cs`, `Invoices/InvoiceWorkspace.cs`, `Invoices/Presets.cs`, `Invoices/IInvoiceAppService.cs`, `Invoices/InvoiceAppService.cs`
- Create: `Approvals/Contracts/ApprovalQueueItemVm.cs`, `Approvals/IApprovalAppService.cs`, `Approvals/ApprovalAppService.cs`
- Create: `Posting/IPostingAppService.cs`, `Posting/PostingAppService.cs`
- Create: `Budget/Contracts/BudgetLineVm.cs`, `Budget/Commands/AmendBudgetCommand.cs`, `Budget/IBudgetAppService.cs`, `Budget/BudgetAppService.cs`
- Create: `Reference/Contracts/{SegmentValueVm,SegmentsVm,CombinationVm,RuleVm,VendorVm,PurchaseOrderVm}.cs`, `Reference/IReferenceAppService.cs`, `Reference/ReferenceAppService.cs`
- Create: `Explanation/ExplanationVm.cs`, `Explanation/IExplanationAppService.cs`, `Explanation/ExplanationAppService.cs`, `Audit/AuditEventVm.cs`, `Extensions/ServiceCollectionExtensions.cs`
- Modify: `tests/GovErp.Architecture.Tests/DomainPurityTests.cs` — тест `CA-10` для UI-facing сервисов
- Test: поведение сценариев — интеграционно в задачах 8–9 (сценариям нужен настоящий SQL Server с `rowversion`); здесь — архитектурный тест и сборка

**Interfaces (Produces):**
```csharp
// Validation/Contracts/EvaluationVm.cs
public sealed record EvaluationVm(Guid Id, string TransactionRef, int ContentVersion, Guid ApprovalCycleId, string Trigger, DateTimeOffset EvaluatedAt,
    Guid EvaluatedBy, string Overall, CapabilitiesVm Capabilities, string EngineVersion, string RuleSetFingerprint,
    IReadOnlyList<AppliedRuleVm> AppliedRules, IReadOnlyList<OutcomeVm> Outcomes, IReadOnlyList<RouteStepVm> ApprovalRoute,
    IReadOnlyList<PreviewLineVm> PostingPreview, PostingCheckVm? PostingCheck);
public sealed record OutcomeVm(string RuleId, int RuleVersion, int Step, string StepName, string Layer, int? Line, string? BudgetKey,
    string Severity, IReadOnlyDictionary<string, string> Inputs, IReadOnlyDictionary<string, string> Computed, string Message,
    string Resolution, IReadOnlyList<string> OverridableBy, Guid? OverriddenBy, string? OverrideReason);
public sealed record AppliedRuleVm(string RuleId, string Layer, int Version);
public sealed record RouteStepVm(string Role, string? Department, string Reason, bool IsSatisfied);
public sealed record PreviewLineVm(string Account, string Family, decimal Debit, decimal Credit, string Description);
public sealed record CapabilitiesVm(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost);
public sealed record PostingCheckVm(bool Passed, IReadOnlyList<string> Failures);

// Invoices/Contracts
/// <summary>RowVersion заполнен только в ответах чтения (GetAsync); после команды UI перечитывает инвойс.</summary>
public sealed record InvoiceVm(Guid Id, string Number, string Reference, Guid VendorId, string VendorName,
    DateOnly InvoiceDate, DateOnly ServiceDate, DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoNumber,
    string Status, int ContentVersion, Guid ApprovalCycleId, string? RowVersion, Guid CreatedBy, bool PaymentHold,
    bool ReadyForPaymentHandoff, IReadOnlyList<DistributionVm> Distributions, IReadOnlyList<ApprovalVm> Approvals,
    IReadOnlyList<OverrideVm> Overrides, EvaluationVm? LastEvaluation);
public sealed record DistributionVm(int LineNo, string Account, decimal Amount, int? PoLineNo);
public sealed record ApprovalVm(Guid CycleId, bool IsActiveCycle, string Role, string? Department, Guid UserId, string Decision, string? Reason, DateTimeOffset At);
public sealed record OverrideVm(Guid CycleId, bool IsActive, Guid EvaluationId, string RuleId, int RuleVersion, int? Line, Guid UserId, string Reason, DateTimeOffset At);
public sealed record InvoiceListItemVm(Guid Id, string Reference, string VendorName, decimal Total, string Status, string? LastOverall, DateOnly PostingDate);

// Invoices/Commands — все мутирующие команды несут CommandEnvelope (GE-16)
public sealed record DistributionCommand(string Account, decimal Amount, int? PoLineNo);
public sealed record CreateInvoiceCommand(CommandEnvelope Envelope, string Number, Guid VendorId, DateOnly InvoiceDate, DateOnly ServiceDate,
    DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoNumber, IReadOnlyList<DistributionCommand> Distributions);
public sealed record UpdateInvoiceCommand(CommandEnvelope Envelope, Guid InvoiceId, Guid VendorId, DateOnly InvoiceDate, DateOnly ServiceDate,
    DateOnly PostingDate, DateOnly DueDate, decimal Total, string? PoNumber, IReadOnlyList<DistributionCommand> Distributions);
public sealed record InvoiceActionCommand(CommandEnvelope Envelope, Guid InvoiceId);
public sealed record ReasonedActionCommand(CommandEnvelope Envelope, Guid InvoiceId, string Reason);
public sealed record OverrideCommand(CommandEnvelope Envelope, Guid InvoiceId, Guid EvaluationId, string RuleId, int? DistributionLine, string Reason);
public sealed record PaymentHoldCommand(CommandEnvelope Envelope, Guid InvoiceId, bool Hold);
public enum InvoicePreset { NonPoGrant, PoBackedGrant, MultiFund }

public interface IInvoiceAppService
{
    Task<IReadOnlyList<InvoiceListItemVm>> ListAsync(ActorContext actor, CancellationToken ct = default);
    Task<InvoiceVm> GetAsync(Guid id, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> CreateDraftAsync(CreateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> CreateFromPresetAsync(InvoicePreset preset, CommandEnvelope envelope, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> UpdateDraftAsync(UpdateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> ValidateAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> SubmitAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> WithdrawAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> SetPaymentHoldAsync(PaymentHoldCommand cmd, ActorContext actor, CancellationToken ct = default);
}

public sealed record ApprovalQueueItemVm(Guid InvoiceId, string Reference, string VendorName, decimal Total, string Overall, string Role, string? Department, string Reason);
public interface IApprovalAppService
{
    Task<IReadOnlyList<ApprovalQueueItemVm>> GetQueueAsync(ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> ApproveAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> RejectAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<InvoiceVm>> OverrideAsync(OverrideCommand cmd, ActorContext actor, CancellationToken ct = default);
}

public interface IPostingAppService
{
    Task<CommandResult<InvoiceVm>> PostAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PreviewLineVm>> GetJournalAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
}

public sealed record BudgetLineVm(string Account, int FiscalYear, string ControlMode, decimal OpeningActuals, decimal OpeningEncumbered,
    decimal Adopted, decimal Amended, decimal Actuals, decimal Encumbered, decimal Held, decimal Available, IReadOnlyList<AmendmentVm> Amendments);
public sealed record AmendmentVm(decimal Amount, string Reference, DateOnly EffectiveDate);
public sealed record AmendBudgetCommand(CommandEnvelope Envelope, string Account, int FiscalYear, decimal Amount, string Reference, DateOnly EffectiveDate);
public interface IBudgetAppService
{
    Task<IReadOnlyList<BudgetLineVm>> ListAsync(int fiscalYear, ActorContext actor, CancellationToken ct = default);
    Task<CommandResult<BudgetLineVm>> AmendAsync(AmendBudgetCommand cmd, ActorContext actor, CancellationToken ct = default);
}

public sealed record SegmentValueVm(string Code, string Name, bool IsActive, IReadOnlyDictionary<string, string> Attributes);
public sealed record SegmentsVm(IReadOnlyList<SegmentValueVm> Funds, IReadOnlyList<SegmentValueVm> Departments, IReadOnlyList<SegmentValueVm> Objects, IReadOnlyList<SegmentValueVm> Grants);
public sealed record CombinationVm(string Code, string Status, DateOnly EffectiveFrom, DateOnly? EffectiveTo, string Source);
public sealed record RuleVm(string RuleId, int Version, int Step, string Layer, bool IsLocallyAdjustable, string? Severity,
    IReadOnlyDictionary<string, string> Parameters, IReadOnlyList<string> OverridableBy, DateOnly EffectiveFrom, DateOnly? EffectiveTo, bool IsEnabled, string Message);
public sealed record RuleSetVm(IReadOnlyList<RuleVm> Rules, string CurrentFingerprint, string EngineVersion);
public sealed record VendorVm(Guid Id, string Code, string Name, string Status, bool SamRegistered);
public sealed record PurchaseOrderLineVm(int LineNo, string Account, decimal AuthorizedAmount, decimal PostedAmount);
public sealed record PurchaseOrderVm(string Number, Guid VendorId, string Status, IReadOnlyList<PurchaseOrderLineVm> Lines);
public interface IReferenceAppService
{
    Task<SegmentsVm> GetSegmentsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<CombinationVm>> GetCombinationsAsync(ActorContext actor, CancellationToken ct = default);
    Task<RuleSetVm> GetRulesAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<VendorVm>> GetVendorsAsync(ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<PurchaseOrderVm>> GetPurchaseOrdersAsync(ActorContext actor, CancellationToken ct = default);
}

public sealed record ExplanationVm(Guid EvaluationId, string Audience, string Text, string Provider, string? Model, string PromptVersion, string? FallbackReason, DateTimeOffset GeneratedAt);
public sealed record AuditEventVm(DateTimeOffset OccurredAt, string ActorName, string Action, string SubjectRef, string CorrelationId, string PayloadJson);
public interface IExplanationAppService
{
    Task<CommandResult<ExplanationVm>> ExplainAsync(Guid evaluationId, ExplanationAudience audience, CommandEnvelope envelope, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<ExplanationVm>> ListAsync(Guid evaluationId, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationVm>> GetEvaluationHistoryAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
    Task<IReadOnlyList<AuditEventVm>> GetAuditTrailAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default);
}
```

- [ ] **Step 1: EvaluationMapping и InvoiceMapping**

`EvaluationMapping.ToVm(EvaluationRecord)` — поле в поле: enum → `ToString()`, `Step` → `(int)` и `ToString()`, `Money` → `.Amount`, `AppliedRules` → `AppliedRuleVm`, `OverriddenBy` → `UserId.Value` и `Reason`. `InvoiceMapping.ToVm(VendorInvoice, Vendor, EvaluationRecord? last, string? rowVersion, DateOnly businessDate)` — `ReadyForPaymentHandoff = invoice.IsReadyForPaymentHandoff(vendor.IsActive, businessDate)`; `ApprovalVm.IsActiveCycle = a.CycleId == invoice.ApprovalCycleId`; `OverrideVm.IsActive = invoice.ActiveOverrides.Contains(o)`. `InvoiceMapping.ToListItem(invoice, vendorName, lastOverall)`.

- [ ] **Step 2: InvoiceWorkspace — общие шаги сценариев в одном scope**

`Invoices/InvoiceWorkspace.cs` (scoped; живёт в scope операции runner'а):
```csharp
public sealed class InvoiceWorkspace(
    IVendorInvoiceRepository invoices, IVendorRepository vendors, IPurchaseOrderRepository purchaseOrders,
    IBudgetLineRepository budgetLines, IEncumbranceRepository encumbrances, IFiscalPeriodRepository periods,
    IJournalRepository journal, IEvaluationRecordRepository evaluations, IRuleDefinitionRepository rules,
    ValidationSubjectAssembler assembler, IAuditTrail audit, IConcurrencyGuard concurrency, IClock clock)
{
    private static readonly ValidationPipeline Pipeline = new(RuleCatalog.Default);

    public IVendorInvoiceRepository Invoices => invoices;
    public IVendorRepository Vendors => vendors;
    public IPurchaseOrderRepository PurchaseOrders => purchaseOrders;
    public IBudgetLineRepository BudgetLines => budgetLines;
    public IEncumbranceRepository Encumbrances => encumbrances;
    public IFiscalPeriodRepository Periods => periods;
    public IJournalRepository Journal => journal;
    public IEvaluationRecordRepository Evaluations => evaluations;
    public IAuditTrail Audit => audit;
    public IConcurrencyGuard Concurrency => concurrency;
    public IClock Clock => clock;

    public async Task<VendorInvoice> LoadAsync(Guid id, CancellationToken ct) =>
        await invoices.FindAsync(id, ct) ?? throw new NotFoundException($"Invoice {id} not found.");

    /// <summary>Снимок → правила на InvoiceDate → оценка; запись добавляется в хранилище, инвойс запоминает её id.</summary>
    public async Task<(EvaluationRecord Record, ValidationSubject Subject, EffectiveRuleSet Rules)> EvaluateAsync(
        VendorInvoice invoice, EvaluationTrigger trigger, ActorContext actor, CancellationToken ct)
    {
        var subject = await assembler.BuildAsync(invoice, ct);
        var ruleSet = RuleResolution.Resolve(await rules.ListAsync(ct), invoice.Dates.Invoice);
        var record = Pipeline.Evaluate(subject, ruleSet, trigger, actor.UserId, clock.Now);
        await evaluations.AddAsync(record, ct);
        invoice.RecordEvaluation(record.Id);
        return (record, subject, ruleSet);
    }

    /// <summary>Reject / Withdraw: освобождает бюджетные резервы, encumbrance claims и PO billing claims инвойса.</summary>
    public async Task ReleaseAllAsync(VendorInvoice invoice, CancellationToken ct)
    {
        foreach (var line in await budgetLines.ListHeldForInvoiceAsync(invoice.Id, ct))
        {
            line.ReleaseAllFor(invoice.Id);
        }

        foreach (var enc in await encumbrances.ListClaimedByInvoiceAsync(invoice.Id, ct))
        {
            enc.ReleaseAllFor(invoice.Id);
        }

        if (invoice.PoNumber is not null && await purchaseOrders.FindByNumberAsync(invoice.PoNumber, ct) is { } po)
        {
            po.ReleaseBillingClaimsFor(invoice.Id);
        }
    }

    public async Task<InvoiceVm> ToVmAsync(VendorInvoice invoice, CancellationToken ct, bool withRowVersion = false)
    {
        var vendor = await vendors.FindAsync(invoice.VendorId, ct) ?? throw new NotFoundException($"Vendor {invoice.VendorId} not found.");
        var last = invoice.LastEvaluationId is { } id ? await evaluations.FindAsync(id, ct) : null;
        return InvoiceMapping.ToVm(invoice, vendor, last, withRowVersion ? concurrency.VersionOf(invoice) : null, clock.BusinessDate);
    }

    public static string ReasonOf(EvaluationRecord record) =>
        string.Join(" ", record.Outcomes.Where(o => o.Severity >= Severity.SoftStop && !o.IsOverridden).Select(o => o.Message));
}
```

- [ ] **Step 3: InvoiceAppService**

```csharp
public sealed class InvoiceAppService(ITenantOperationRunner runner) : IInvoiceAppService
{
    public Task<IReadOnlyList<InvoiceListItemVm>> ListAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<InvoiceListItemVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var result = new List<InvoiceListItemVm>();
            foreach (var inv in (await ws.Invoices.ListAsync(token)).OrderByDescending(i => i.CreatedAt))
            {
                var vendor = await ws.Vendors.FindAsync(inv.VendorId, token);
                var last = inv.LastEvaluationId is { } id ? await ws.Evaluations.FindAsync(id, token) : null;
                result.Add(InvoiceMapping.ToListItem(inv, vendor?.Name ?? "?", last?.Overall.ToString()));
            }

            return result;
        }, ct);

    public Task<InvoiceVm> GetAsync(Guid id, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            return await ws.ToVmAsync(await ws.LoadAsync(id, token), token, withRowVersion: true);
        }, ct);

    public Task<CommandResult<InvoiceVm>> CreateDraftAsync(CreateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "CreateInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInRole(Roles.ApClerk))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only AP clerks create invoices.");
            }

            if (DemoDatesProblem(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate) is { } problem)
            {
                return CommandResult<InvoiceVm>.Refused(null, problem);
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            _ = await ws.Vendors.FindAsync(cmd.VendorId, token) ?? throw new NotFoundException($"Vendor {cmd.VendorId} not found.");
            if (cmd.PoNumber is not null && await ws.PurchaseOrders.FindByNumberAsync(cmd.PoNumber, token) is null)
            {
                return CommandResult<InvoiceVm>.Refused(null, $"Purchase order {cmd.PoNumber} not found.");
            }

            if (await ws.Invoices.ExistsDuplicateAsync(cmd.VendorId, VendorInvoice.Normalize(cmd.Number), Guid.Empty, token))
            {
                return CommandResult<InvoiceVm>.Refused(null, "An invoice with this number already exists for this vendor.");
            }

            var invoice = new VendorInvoice(cmd.Number, cmd.VendorId,
                new InvoiceDates(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate, cmd.DueDate), Money.Of(cmd.Total), cmd.PoNumber,
                cmd.Distributions.Select(ToInput).ToList(), actor.UserId, ws.Clock.Now);
            await ws.Invoices.AddAsync(invoice, token);
            ws.Audit.Record(actor, "InvoiceCreated", invoice.Reference, cmd.Envelope.CommandId.ToString(),
                new { invoice.Number, cmd.Total, Lines = cmd.Distributions.Count, cmd.PoNumber });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public async Task<CommandResult<InvoiceVm>> CreateFromPresetAsync(InvoicePreset preset, CommandEnvelope envelope, ActorContext actor, CancellationToken ct = default)
    {
        var vendorId = await runner.QueryAsync(actor, async (sp, token) =>
            (await sp.GetRequiredService<IVendorRepository>().ListAsync(token)).Single(v => v.Code == Presets.VendorCode).Id, ct);
        return await CreateDraftAsync(Presets.For(preset, envelope, vendorId), actor, ct);
    }

    public Task<CommandResult<InvoiceVm>> UpdateDraftAsync(UpdateInvoiceCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "UpdateInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy != actor.UserId)
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the author edits a draft.");
            }

            if (DemoDatesProblem(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate) is { } problem)
            {
                return CommandResult<InvoiceVm>.Refused(null, problem);
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var changed = invoice.ReplaceContent(cmd.VendorId, new InvoiceDates(cmd.InvoiceDate, cmd.ServiceDate, cmd.PostingDate, cmd.DueDate),
                Money.Of(cmd.Total), cmd.PoNumber, cmd.Distributions.Select(ToInput).ToList());
            ws.Audit.Record(actor, "InvoiceUpdated", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { changed, invoice.ContentVersion });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> ValidateAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "ValidateInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            var (record, _, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Manual, actor, token);
            ws.Audit.Record(actor, "InvoiceValidated", invoice.Reference, record.Id.ToString(), new { Overall = record.Overall.ToString(), record.RuleSetFingerprint });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    /// <summary>Атомарный Submit (spec §5.3): оценка; при HardStop — отказ без резервов; иначе резервы, encumbrance и billing claims, статус — в одной транзакции.</summary>
    public Task<CommandResult<InvoiceVm>> SubmitAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "SubmitInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy != actor.UserId || !actor.IsInRole(Roles.ApClerk))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the author submits the invoice.");
            }

            if (invoice.Status != InvoiceStatus.Draft)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (record, subject, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Submit, actor, token);
            if (!record.Capabilities.CanSubmit)
            {
                ws.Audit.Record(actor, "InvoiceSubmitRefused", invoice.Reference, record.Id.ToString(), new { Overall = record.Overall.ToString() });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), InvoiceWorkspace.ReasonOf(record));
            }

            var cv = invoice.ContentVersion;
            var fy = FiscalYear.FromDate(invoice.Dates.Posting);
            foreach (var key in subject.BudgetKeys.OrderBy(k => k.ToString(), StringComparer.Ordinal))
            {
                var required = subject.RequiredNewBudget(key);
                if (required.IsZero)
                {
                    continue;
                }

                var line = await ws.BudgetLines.FindAsync(key, fy, token)
                    ?? throw new InvalidOperationException($"Budget line {key} vanished after evaluation.");
                if (!line.Reserve(invoice.Id, cv, required).IsReserved)
                {
                    throw new InvalidOperationException($"Reservation on {key} refused after a passing evaluation.");
                }
            }

            foreach (var po in subject.PoLines.OrderBy(p => p.PoLineRef, StringComparer.Ordinal))
            {
                var liquidation = subject.EligibleLiquidation(po.PoLineRef);
                if (!liquidation.IsZero)
                {
                    var enc = await ws.Encumbrances.FindByPoLineAsync(po.PoLineRef, token)
                        ?? throw new InvalidOperationException($"Encumbrance {po.PoLineRef} vanished after evaluation.");
                    enc.Claim(invoice.Id, cv, liquidation);
                }
            }

            if (invoice.PoNumber is not null)
            {
                var order = await ws.PurchaseOrders.FindByNumberAsync(invoice.PoNumber, token)
                    ?? throw new InvalidOperationException($"PO {invoice.PoNumber} vanished after evaluation.");
                foreach (var no in invoice.Distributions.Where(d => d.PoLineNo is not null).Select(d => d.PoLineNo!.Value).Distinct().Order())
                {
                    order.ClaimBilling(no, invoice.Id, cv, subject.PoAmount(order.LineRef(no)));
                }
            }

            invoice.Submit(record.Id);
            ws.Audit.Record(actor, "InvoiceSubmitted", invoice.Reference, record.Id.ToString(),
                new { Overall = record.Overall.ToString(), cv, Reserved = subject.BudgetKeys.Sum(k => subject.RequiredNewBudget(k).Amount) });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> WithdrawAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "WithdrawInvoice", cmd, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy != actor.UserId)
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the author withdraws the invoice.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            await ws.ReleaseAllAsync(invoice, token);
            invoice.Withdraw(actor.UserId, cmd.Reason, ws.Clock.Now);   // Posted → PayablesException → Refused, ничего не сохраняется
            ws.Audit.Record(actor, "InvoiceWithdrawn", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { cmd.Reason });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> SetPaymentHoldAsync(PaymentHoldCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "SetPaymentHold", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Posters))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the budget officer or finance director sets a payment hold.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            invoice.SetPaymentHold(cmd.Hold);
            ws.Audit.Record(actor, "PaymentHoldChanged", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { cmd.Hold });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    private static DistributionInput ToInput(DistributionCommand d) => new(AccountCode.Parse(d.Account), Money.Of(d.Amount), d.PoLineNo);

    /// <summary>Spec §5, правило 6: демо поддерживает только InvoiceDate = ServiceDate = PostingDate; иное отклоняется с объяснением.</summary>
    private static string? DemoDatesProblem(DateOnly invoice, DateOnly service, DateOnly posting) =>
        invoice == service && service == posting
            ? null
            : "This demo supports only InvoiceDate = ServiceDate = PostingDate; accrual across periods is out of scope.";
}
```

`Invoices/Presets.cs` — `public const string VendorCode = "ACME";` и `Presets.For(InvoicePreset, CommandEnvelope, Guid vendorId) → CreateInvoiceCommand`: три команды по spec §2.3, даты 2026-06-15 (due 2026-07-15), **уникальный и идемпотентный** номер `$"{prefix}-{envelope.CommandId:N}"[..16]` (тот же CommandId → тот же номер; префиксы `NPO`, `PO`, `MF`). `NonPoGrant` — 160,000 на `701-6000-53100-G-COPS-26`; `PoBackedGrant` — `PO-2026-0451`, 160,000 на `701-3000-53100-G-COPS-26`, строка PO 1; `MultiFund` — 30,000 = 12,000 `101-6000-53100` + 8,000 `202-4000-53100` + 10,000 `501-5000-53100`. Vendor id приходит параметром: Application не ссылается на `SpringfieldData`.

- [ ] **Step 4: ApprovalAppService**

```csharp
public sealed class ApprovalAppService(ITenantOperationRunner runner) : IApprovalAppService
{
    public Task<IReadOnlyList<ApprovalQueueItemVm>> GetQueueAsync(ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<ApprovalQueueItemVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var items = new List<ApprovalQueueItemVm>();
            foreach (var inv in (await ws.Invoices.ListAsync(token)).Where(i => i.Status == InvoiceStatus.Submitted))
            {
                if (inv.LastEvaluationId is not { } id || await ws.Evaluations.FindAsync(id, token) is not { } last)
                {
                    continue;
                }

                var vendor = await ws.Vendors.FindAsync(inv.VendorId, token);
                items.AddRange(last.ApprovalRoute
                    .Where(r => !r.IsSatisfied && actor.IsInRole(r.Role.ToString()) && (r.Department is null || r.Department == actor.DepartmentCode))
                    .Select(r => new ApprovalQueueItemVm(inv.Id, inv.Reference, vendor?.Name ?? "?", inv.Total.Amount, last.Overall.ToString(),
                        r.Role.ToString(), r.Department, r.Reason)));
            }

            return items;
        }, ct);

    /// <summary>Spec §5, правило 2: смена fingerprint или версии содержания открывает новый цикл; новый HardStop или неснятый SoftStop блокируют согласование.</summary>
    public Task<CommandResult<InvoiceVm>> ApproveAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "ApproveInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Approvers))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only approvers approve.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.CreatedBy == actor.UserId)
            {
                return CommandResult<InvoiceVm>.Forbidden("The author cannot approve their own invoice (separation of duties).");
            }

            if (invoice.Status != InvoiceStatus.Submitted)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (record, subject, rules) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Approve, actor, token);
            if (subject.ApprovalBaseline is { } baseline
                && (baseline.RuleSetFingerprint != rules.Fingerprint || baseline.ContentVersion != invoice.ContentVersion))
            {
                invoice.OpenNewApprovalCycle();
                ws.Audit.Record(actor, "ApprovalCycleRestarted", invoice.Reference, record.Id.ToString(),
                    new { Previous = baseline.RuleSetFingerprint, Current = rules.Fingerprint });
                (record, _, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Approve, actor, token);
            }

            if (!record.Capabilities.CanApprove)
            {
                ws.Audit.Record(actor, "ApprovalRefused", invoice.Reference, record.Id.ToString(), new { Overall = record.Overall.ToString() });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), InvoiceWorkspace.ReasonOf(record));
            }

            var step = record.ApprovalRoute.FirstOrDefault(r => !r.IsSatisfied && actor.IsInRole(r.Role.ToString())
                && (r.Department is null || r.Department == actor.DepartmentCode));
            if (step is null)
            {
                return CommandResult<InvoiceVm>.Forbidden($"{actor.UserName} is not a pending approver for {invoice.Reference}.");
            }

            invoice.RecordApproval(RoleMapping.ToPayables(step.Role), step.Department, actor.UserId, record.Id, ws.Clock.Now);
            if (record.ApprovalRoute.Count(r => !r.IsSatisfied) == 1)
            {
                invoice.MarkApproved();
            }

            ws.Audit.Record(actor, "InvoiceApproved", invoice.Reference, record.Id.ToString(),
                new { Role = step.Role.ToString(), step.Department, invoice.ApprovalCycleId, Status = invoice.Status.ToString() });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    public Task<CommandResult<InvoiceVm>> RejectAsync(ReasonedActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "RejectInvoice", cmd, async (sp, token) =>
        {
            if (RoleMapping.PayablesRoleOf(actor) is not { } role)
            {
                return CommandResult<InvoiceVm>.Forbidden("Only approvers reject.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            await ws.ReleaseAllAsync(invoice, token);
            invoice.Reject(role, actor.UserId, cmd.Reason, ws.Clock.Now);
            ws.Audit.Record(actor, "InvoiceRejected", invoice.Reference, cmd.Envelope.CommandId.ToString(), new { cmd.Reason, Role = role.ToString() });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);

    /// <summary>Override привязан к оценке, правилу, его версии и строке (spec §4.1); чужую или устаревшую оценку не принимает.</summary>
    public Task<CommandResult<InvoiceVm>> OverrideAsync(OverrideCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "OverrideRule", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Overriders))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the budget officer or finance director overrides.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.Status != InvoiceStatus.Submitted)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var basis = await ws.Evaluations.FindAsync(cmd.EvaluationId, token);
            if (basis is null || basis.InvoiceId != invoice.Id || basis.ContentVersion != invoice.ContentVersion || basis.ApprovalCycleId != invoice.ApprovalCycleId)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), "The evaluation is not current for this invoice; reload and retry.");
            }

            var outcome = basis.Outcomes.FirstOrDefault(o => o.RuleId == cmd.RuleId && o.DistributionLine == cmd.DistributionLine
                && o.Severity == Severity.SoftStop && !o.IsOverridden);
            if (outcome is null)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"No open soft stop {cmd.RuleId} on that line.");
            }

            if (!outcome.OverridableBy.Any(r => actor.IsInRole(r.ToString())))
            {
                return CommandResult<InvoiceVm>.Forbidden($"{cmd.RuleId} cannot be overridden by {actor.UserName}.");
            }

            invoice.Override(basis.Id, outcome.RuleId, outcome.RuleVersion, outcome.DistributionLine, actor.UserId, cmd.Reason, ws.Clock.Now);
            var (record, _, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Override, actor, token);
            ws.Audit.Record(actor, "OverrideRecorded", invoice.Reference, record.Id.ToString(),
                new { outcome.RuleId, outcome.RuleVersion, outcome.DistributionLine, cmd.Reason, Overall = record.Overall.ToString() });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, ct: ct);
}
```

- [ ] **Step 5: PostingAppService**

```csharp
public sealed class PostingAppService(ITenantOperationRunner runner) : IPostingAppService
{
    /// <summary>Spec §5.4: Serializable-транзакция — перевалидация, погашение своих резервов и claims, журнал, статус, аудит, receipt.</summary>
    public Task<CommandResult<InvoiceVm>> PostAsync(InvoiceActionCommand cmd, ActorContext actor, CancellationToken ct = default) =>
        runner.ExecuteAsync(actor, cmd.Envelope, "PostInvoice", cmd, async (sp, token) =>
        {
            if (!actor.IsInAnyRole(Roles.Posters))
            {
                return CommandResult<InvoiceVm>.Forbidden("Only the budget officer or finance director posts.");
            }

            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(cmd.InvoiceId, token);
            if (invoice.Status != InvoiceStatus.Approved)
            {
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), $"Invoice is {invoice.Status}.");
            }

            ws.Concurrency.Expect(invoice, cmd.Envelope.ExpectedRowVersion);
            var (record, subject, _) = await ws.EvaluateAsync(invoice, EvaluationTrigger.Post, actor, token);
            if (record.PostingCheck is not { Passed: true } check)
            {
                if (record.PostingCheck?.Failures.Any(f => f.StartsWith(PostingEligibility.RevalidationRequired, StringComparison.Ordinal)) == true)
                {
                    invoice.OpenNewApprovalCycle();   // повторное согласование по актуальным правилам — затем Post возможен
                    ws.Audit.Record(actor, "ApprovalCycleRestarted", invoice.Reference, record.Id.ToString(), new { Reason = "rule set or content changed" });
                    // Последняя оценка должна отражать новый цикл: её маршрут — основа для очереди согласующих.
                    await ws.EvaluateAsync(invoice, EvaluationTrigger.Manual, actor, token);
                }

                ws.Audit.Record(actor, "PostRefused", invoice.Reference, record.Id.ToString(), new { record.PostingCheck?.Failures });
                return CommandResult<InvoiceVm>.Refused(await ws.ToVmAsync(invoice, token), string.Join(" ", record.PostingCheck?.Failures ?? []));
            }

            if (await ws.Journal.ExistsAsync(invoice.Reference, PostingKind.InvoicePosting, token))
            {
                return new CommandResult<InvoiceVm>(CommandStatus.Conflict, null, "The invoice has already been posted.", false);
            }

            var (py, pm) = FiscalPeriod.KeyFor(invoice.Dates.Posting);
            var period = await ws.Periods.FindAsync(py, pm, token) ?? throw new NotFoundException($"Fiscal period {py}-{pm:00} not found.");
            var cv = invoice.ContentVersion;
            var charged = Money.Zero;

            foreach (var line in await ws.BudgetLines.ListHeldForInvoiceAsync(invoice.Id, token))
            {
                foreach (var reservation in line.Reservations.Where(r => r.Status == ReservationStatus.Held && r.InvoiceId == invoice.Id && r.ContentVersion == cv).ToList())
                {
                    line.Commit(reservation.Id);
                    charged += reservation.Amount;
                }
            }

            foreach (var enc in await ws.Encumbrances.ListClaimedByInvoiceAsync(invoice.Id, token))
            {
                if (enc.ClaimOf(invoice.Id, cv) is not { } claim)
                {
                    continue;
                }

                var liquidated = enc.ConsumeClaim(claim.Id);
                var line = await ws.BudgetLines.FindAsync(enc.Account, enc.FiscalYear, token)
                    ?? throw new InvalidOperationException($"Budget line {enc.Account} {enc.FiscalYear} not found.");
                line.RecordLiquidation(liquidated);
                charged += liquidated;
            }

            if (invoice.PoNumber is not null && await ws.PurchaseOrders.FindByNumberAsync(invoice.PoNumber, token) is { } order)
            {
                foreach (var claim in order.BillingClaimsOf(invoice.Id, cv))
                {
                    order.ConsumeBillingClaim(claim.Id);
                }
            }

            // Инвариант consistency §4: actuals растут ровно на сумму инвойса.
            if (charged != invoice.Total)
            {
                throw new InvalidOperationException($"Posting would change actuals by {charged}, expected {invoice.Total}.");
            }

            var lines = record.PostingPreview.Select(p => new JournalLine(p.Account,
                p.Family == PostingPreviewComposer.Budgetary ? LedgerFamily.Budgetary : LedgerFamily.Financial, p.Debit, p.Credit, p.Description)).ToList();
            await ws.Journal.AddAsync(JournalEntry.Create(invoice.Reference, PostingKind.InvoicePosting, lines, period, invoice.Dates.Posting, actor.UserId, ws.Clock.Now), token);
            invoice.Post(record.Id, ws.Clock.Now);
            ws.Audit.Record(actor, "InvoicePosted", invoice.Reference, record.Id.ToString(), new { Charged = charged.Amount, Lines = lines.Count });
            return CommandResult<InvoiceVm>.Accepted(await ws.ToVmAsync(invoice, token));
        }, System.Data.IsolationLevel.Serializable, ct);

    public Task<IReadOnlyList<PreviewLineVm>> GetJournalAsync(Guid invoiceId, ActorContext actor, CancellationToken ct = default) =>
        runner.QueryAsync<IReadOnlyList<PreviewLineVm>>(actor, async (sp, token) =>
        {
            var ws = sp.GetRequiredService<InvoiceWorkspace>();
            var invoice = await ws.LoadAsync(invoiceId, token);
            return (await ws.Journal.ListBySourceAsync(invoice.Reference, token))
                .SelectMany(e => e.Lines)
                .Select(l => new PreviewLineVm(l.Account.ToString(), l.Family.ToString(), l.Debit.Amount, l.Credit.Amount, l.Description))
                .ToList();
        }, ct);
}
```
Ошибка на любом шаге (включая журнал) — исключение из тела; runner не вызывает `SaveChanges`, транзакция откатывается: ни бюджет, ни claims, ни статус не меняются (`AtomicPostFailure`).

- [ ] **Step 6: Budget, Reference, Explanation**

`BudgetAppService`:
```csharp
public Task<CommandResult<BudgetLineVm>> AmendAsync(AmendBudgetCommand cmd, ActorContext actor, CancellationToken ct = default) =>
    runner.ExecuteAsync(actor, cmd.Envelope, "AmendBudget", cmd, async (sp, token) =>
    {
        if (!actor.IsInAnyRole(Roles.Overriders))
        {
            return CommandResult<BudgetLineVm>.Forbidden("Only the budget officer or finance director amends budgets.");
        }

        var lines = sp.GetRequiredService<IBudgetLineRepository>();
        var line = await lines.FindAsync(AccountCode.Parse(cmd.Account), new FiscalYear(cmd.FiscalYear), token)
            ?? throw new NotFoundException($"Budget line {cmd.Account} FY{cmd.FiscalYear} not found.");
        line.Amend(Money.Of(cmd.Amount), cmd.Reference, cmd.EffectiveDate);   // дата вне FY → LedgerException → Refused
        sp.GetRequiredService<IAuditTrail>().Record(actor, "BudgetAmended", cmd.Account, cmd.Envelope.CommandId.ToString(),
            new { cmd.FiscalYear, cmd.Amount, cmd.Reference, cmd.EffectiveDate, Amended = line.Amended.Amount });
        return CommandResult<BudgetLineVm>.Accepted(await BudgetMapping.ToVmAsync(line, sp.GetRequiredService<IOpeningBalanceRepository>(), token));
    }, ct: ct);
```
`ListAsync(fiscalYear)` — `QueryAsync`: строки + opening balances (`BudgetMapping.ToVmAsync` находит opening по ключу). `ReferenceAppService` — чтение и маппинг (`Attributes` фонда: `Type`, `Basis`, `ControlMode`, `GrantPolicy`; гранта: `Sponsor`, `IsFederal`, `PeriodFrom`, `PeriodTo`, `Status`); `GetRulesAsync` возвращает `RuleSetVm` с `RuleResolution.Resolve(all, clock.BusinessDate).Fingerprint`.

`ExplanationAppService(ITenantOperationRunner runner, IExplanationGenerator generator)` — **LLM вне транзакции** (consistency §4):
```csharp
public async Task<CommandResult<ExplanationVm>> ExplainAsync(Guid evaluationId, ExplanationAudience audience, CommandEnvelope envelope, ActorContext actor, CancellationToken ct = default)
{
    var record = await runner.QueryAsync(actor, (sp, token) => sp.GetRequiredService<IEvaluationRecordRepository>().FindAsync(evaluationId, token), ct);
    if (record is null)
    {
        return CommandResult<ExplanationVm>.NotFound($"Evaluation {evaluationId} not found.");
    }

    var result = await generator.ExplainAsync(record, audience, ct);           // сеть — до и вне транзакции
    return await runner.ExecuteAsync(actor, envelope, "ExplainEvaluation", new { evaluationId, audience }, (sp, token) =>
    {
        var clock = sp.GetRequiredService<IClock>();
        var saved = new ExplanationRecord(Guid.NewGuid(), record.Id, record.TransactionRef, audience, result.Text, result.Provider,
            result.Model, result.PromptVersion, result.FallbackReason, clock.Now);
        sp.GetRequiredService<IExplanationRepository>().Add(saved);
        sp.GetRequiredService<IAuditTrail>().Record(actor, "ExplanationGenerated", record.TransactionRef, record.Id.ToString(),
            new { audience = audience.ToString(), result.Provider, result.Model, result.FallbackReason });
        return Task.FromResult(CommandResult<ExplanationVm>.Accepted(ExplanationMapping.ToVm(saved)));
    }, ct: ct);
}
```
`ListAsync`, `GetEvaluationHistoryAsync` (по `invoice.Reference`), `GetAuditTrailAsync` (по `invoice.Reference`) — `QueryAsync`.

- [ ] **Step 7: Регистрация и архитектурный тест**

`Application.Web/Extensions/ServiceCollectionExtensions.cs`:
```csharp
public static IServiceCollection AddApplication(this IServiceCollection s, IConfiguration cfg)
{
    s.Configure<PostingOptions>(cfg.GetSection("Posting"));
    s.AddScoped<ValidationSubjectAssembler>();
    s.AddScoped<InvoiceWorkspace>();
    s.AddScoped<IInvoiceAppService, InvoiceAppService>();
    s.AddScoped<IApprovalAppService, ApprovalAppService>();
    s.AddScoped<IPostingAppService, PostingAppService>();
    s.AddScoped<IBudgetAppService, BudgetAppService>();
    s.AddScoped<IReferenceAppService, ReferenceAppService>();
    s.AddScoped<IExplanationAppService, ExplanationAppService>();
    return s;
}
```

`GovErp.Architecture.Tests/DomainPurityTests.cs`:
```csharp
[Fact]
public void Ui_facing_app_services_do_not_expose_entities()
{
    var app = typeof(GovErp.Application.Web.Common.ActorContext).Assembly;
    var entityTypes = ArchitectureFixture.DomainContexts.SelectMany(a => a.GetTypes())
        .Where(t => t.Namespace?.EndsWith(".Entities", StringComparison.Ordinal) == true).ToHashSet();
    var offenders = app.GetTypes()
        .Where(t => t.IsInterface && t.Name.StartsWith('I') && t.Name.EndsWith("AppService", StringComparison.Ordinal))
        .SelectMany(t => t.GetMethods())
        .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
        .SelectMany(Unwrap)
        .Where(entityTypes.Contains).Select(t => t.FullName).Distinct().ToList();
    offenders.Should().BeEmpty(because: "CA-10: UI-facing services return view models");

    static IEnumerable<Type> Unwrap(Type t) => t.IsGenericType ? t.GetGenericArguments().SelectMany(Unwrap).Append(t) : [t];
}
```

- [ ] **Step 8:** `dotnet build && dotnet test tests/GovErp.Architecture.Tests` — успех.
- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "Application: atomic lifecycle commands (submit, approve, override, reject, withdraw, post), budget, reference, explanation services

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Provisioning тенантов, права БД, seed, demo-reset

**Files:**
- Create: `Seed/MasterSeed.cs`, `Seed/TenantSeeder.cs`, `Startup/DatabaseSecurity.cs`, `Startup/TenantProvisioner.cs`, `Startup/DatabaseInitializer.cs`, `Startup/DemoReset.cs`, `Startup/StartupOptions.cs`

**Interfaces:**
- Produces:
  - `TenantProvisioner.ProvisionAsync(string tenantId, string name, string databaseName, string credentialKey, bool isDemo, TenantSeed seed, CancellationToken)` — создаёт/мигрирует БД тенанта **migration-пользователем**, создаёт runtime-login/пользователя с правами spec §6.2, засевает (только пустую БД), регистрирует тенанта в Master. `enum TenantSeed { Springfield, ReferenceOnly }`.
  - `DatabaseSecurity.EnsureRuntimeUserAsync(string migrationConnection, string database, string login, string password, CancellationToken)`.
  - `DatabaseInitializer.InitializeAsync(IServiceProvider, CancellationToken)` — ожидание SQL, миграция Master, master-runtime-пользователь (только чтение), seed Master, провижининг `springfield` и `shelbyville`, проверка `RuleSetGuard` для каждого тенанта (нарушение — исключение при старте).
  - `DemoReset.RunAsync(string[] args, IServiceProvider, IHostEnvironment, CancellationToken) → int` (код выхода).
- Конфигурация (`StartupOptions`, секция `Startup`): `MigrationConnectionTemplate` (`…;Database={0};User Id=sa;Password=…`), `MasterDatabase` (`GovErp_Master`), `MasterRuntimeLogin`/`MasterRuntimePassword`, `DemoResetAllowlist` (`["GovErp_Springfield"]`).

- [ ] **Step 1: Права runtime-пользователя**

`Startup/DatabaseSecurity.cs`:
```csharp
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace GovErp.Infrastructure.Startup;

/// <summary>Runtime-пользователь тенанта: чтение/запись своей БД, запрет UPDATE/DELETE append-only таблиц (GE-12). Миграции — отдельным пользователем.</summary>
public static partial class DatabaseSecurity
{
    private static readonly string[] AppendOnlyTables =
        ["validation.EvaluationRecords", "validation.Explanations", "ledger.JournalEntries", "ledger.JournalLines", "audit.Events", "ap.ProcessedCommands"];

    public static async Task EnsureRuntimeUserAsync(string migrationConnection, string database, string login, string password, CancellationToken ct)
    {
        RequireIdentifier(database);
        RequireIdentifier(login);
        var pwd = password.Replace("'", "''", StringComparison.Ordinal);
        var sql = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{login}')
                CREATE LOGIN [{login}] WITH PASSWORD = N'{pwd}', CHECK_POLICY = OFF;
            USE [{database}];
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{login}')
                CREATE USER [{login}] FOR LOGIN [{login}];
            ALTER ROLE db_datareader ADD MEMBER [{login}];
            ALTER ROLE db_datawriter ADD MEMBER [{login}];
            {string.Join('\n', AppendOnlyTables.Select(t => $"DENY UPDATE, DELETE ON {t} TO [{login}];"))}
            """;
        await using var connection = new SqlConnection(migrationConnection);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Идентификаторы нельзя передать параметром — только проверенные имена.</summary>
    private static void RequireIdentifier(string value)
    {
        if (!Identifier().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a safe SQL identifier.", nameof(value));
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex Identifier();
}
```
`ap.ProcessedCommands` тоже только-вставка: receipt не переписывается.

- [ ] **Step 2: Seed**

`Seed/TenantSeeder.cs`:
```csharp
public static class TenantSeeder
{
    /// <summary>Засевает только пустую БД тенанта. Остатки — OpeningBalance, а не проводки (spec §5, правило 7).</summary>
    public static async Task SeedAsync(GovErpDbContext db, TenantSeed seed, CancellationToken ct)
    {
        if (await db.Funds.AnyAsync(ct))
        {
            return;
        }

        var d = SpringfieldData.Create();
        db.Funds.AddRange(d.Funds);
        db.Departments.AddRange(d.Departments);
        db.ObjectCodes.AddRange(d.Objects);
        db.FiscalPeriods.AddRange(d.Periods);
        db.RuleDefinitions.AddRange(d.Rules);
        if (seed == TenantSeed.Springfield)
        {
            db.Grants.AddRange(d.Grants);
            db.AccountCombinations.AddRange(d.Combinations);
            db.OpeningBalances.AddRange(d.OpeningBalances);
            db.BudgetLines.AddRange(d.BudgetLines);
            db.Encumbrances.AddRange(d.Encumbrances);
            db.PurchaseOrders.AddRange(d.PurchaseOrders);
            db.Vendors.AddRange(d.Vendors);
        }

        await db.SaveChangesAsync(ct);
    }
}
```

`Seed/MasterSeed.cs` — тенанты и 10 пользователей (пароль у всех `Demo!2026`, хеш — `PasswordHasher<UserAccount>`; Id `ap.clerk` = `SpringfieldData.ClerkId`, остальные — фиксированные Guid `10000000-0000-0000-0000-00000000000N`):

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

`MasterSeed.SeedUsersAsync(MasterDbContext, IPasswordHasher<UserAccount>, CancellationToken)` — добавляет только отсутствующих по `UserName`.

- [ ] **Step 3: TenantProvisioner и DatabaseInitializer**

`Startup/TenantProvisioner.cs`:
```csharp
public sealed class TenantProvisioner(IOptions<StartupOptions> startup, IOptions<TenancyOptions> tenancy)
{
    /// <summary>Реестр тенантов пишется migration-пользователем: runtime-пользователь Master только читает.</summary>
    private MasterDbContext MasterForWrite() => new(new DbContextOptionsBuilder<MasterDbContext>()
        .UseSqlServer(string.Format(CultureInfo.InvariantCulture, startup.Value.MigrationConnectionTemplate, startup.Value.MasterDatabase)).Options);

    public async Task ProvisionAsync(string tenantId, string name, string databaseName, string credentialKey, bool isDemo, TenantSeed seed, CancellationToken ct)
    {
        var migration = string.Format(CultureInfo.InvariantCulture, startup.Value.MigrationConnectionTemplate, databaseName);
        await using (var db = new GovErpDbContext(new DbContextOptionsBuilder<GovErpDbContext>().UseSqlServer(migration).Options))
        {
            await db.Database.MigrateAsync(ct);
            await TenantSeeder.SeedAsync(db, seed, ct);
            var violations = RuleSetGuard.FindViolations(await db.RuleDefinitions.ToListAsync(ct), RuleCatalog.Default);
            if (violations.Count > 0)
            {
                throw new InvalidOperationException($"Tenant {tenantId} rule set is invalid: {string.Join("; ", violations)}");
            }
        }

        var credential = tenancy.Value.Credentials[credentialKey];
        await DatabaseSecurity.EnsureRuntimeUserAsync(string.Format(CultureInfo.InvariantCulture, startup.Value.MigrationConnectionTemplate, "master"),
            databaseName, credential.User, credential.Password, ct);

        await using var master = MasterForWrite();
        if (!await master.Tenants.AnyAsync(t => t.Id == tenantId, ct))
        {
            master.Tenants.Add(new Tenant { Id = tenantId, Name = name, DatabaseName = databaseName, CredentialKey = credentialKey, IsDemo = isDemo });
            await master.SaveChangesAsync(ct);
        }
    }
}
```
`ProvisionAsync` не зависит от `MasterDbContext` из DI: тот настроен на read-only runtime-пользователя.

`Startup/DatabaseInitializer.cs` — порядок:
1. до 20 попыток с паузой 3 с открыть соединение по `MigrationConnectionTemplate` с `master` (SQL в контейнере стартует дольше приложения);
2. `MigrateAsync` для `MasterDbContext` на migration-строке `GovErp_Master`;
3. `DatabaseSecurity.EnsureReadOnlyUserAsync(migrationConnection("master"), "GovErp_Master", MasterRuntimeLogin, MasterRuntimePassword, ct)` — тот же скрипт, что `EnsureRuntimeUserAsync`, но только `db_datareader` (без `db_datawriter` и без DENY); добавить этот метод в `DatabaseSecurity`;
4. `MasterSeed.SeedUsersAsync`;
5. `ProvisionAsync("springfield", "City of Springfield", "GovErp_Springfield", "springfield", isDemo: true, TenantSeed.Springfield)`;
6. `ProvisionAsync("shelbyville", "City of Shelbyville", "GovErp_Shelbyville", "shelbyville", isDemo: false, TenantSeed.ReferenceOnly)`.
Шаги идемпотентны: повторный старт не пересоздаёт данные и не засевает непустые БД.

- [ ] **Step 4: demo-reset**

`Startup/DemoReset.cs` (spec §5, правило 10):
```csharp
public static class DemoReset
{
    /// <summary>demo-reset --tenant springfield --confirm springfield. Только Environment=Demo, IsDemo, имя БД в allowlist.</summary>
    public static async Task<int> RunAsync(string[] args, IServiceProvider services, IHostEnvironment env, CancellationToken ct)
    {
        string? Arg(string name) => args.SkipWhile(a => a != name).Skip(1).FirstOrDefault();
        var tenantId = Arg("--tenant");
        if (!env.IsEnvironment("Demo") || tenantId is null || Arg("--confirm") != tenantId)
        {
            Console.Error.WriteLine("demo-reset requires Environment=Demo and --tenant X --confirm X.");
            return 2;
        }

        await using var scope = services.CreateAsyncScope();
        var startup = scope.ServiceProvider.GetRequiredService<IOptions<StartupOptions>>().Value;
        var master = scope.ServiceProvider.GetRequiredService<MasterDbContext>();
        var tenant = await master.Tenants.SingleOrDefaultAsync(t => t.Id == tenantId, ct);
        if (tenant is null || !tenant.IsDemo || !startup.DemoResetAllowlist.Contains(tenant.DatabaseName))
        {
            Console.Error.WriteLine($"Tenant '{tenantId}' is not a resettable demo tenant.");
            return 3;
        }

        Console.WriteLine($"Resetting demo tenant {tenant.Id} ({tenant.DatabaseName}). All its invoices, evaluations and audit history will be deleted.");
        var migration = string.Format(CultureInfo.InvariantCulture, startup.MigrationConnectionTemplate, "master");
        await using (var connection = new SqlConnection(migration))
        {
            await connection.OpenAsync(ct);
            // Имя БД прошло allowlist; SINGLE_USER закрывает активные операции на время сброса.
            await using var drop = new SqlCommand(
                $"IF DB_ID(N'{tenant.DatabaseName}') IS NOT NULL BEGIN ALTER DATABASE [{tenant.DatabaseName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{tenant.DatabaseName}]; END",
                connection);
            await drop.ExecuteNonQueryAsync(ct);
        }

        await scope.ServiceProvider.GetRequiredService<TenantProvisioner>()
            .ProvisionAsync(tenant.Id, tenant.Name, tenant.DatabaseName, tenant.CredentialKey, tenant.IsDemo, TenantSeed.Springfield, ct);
        Console.WriteLine("Demo tenant reset complete.");
        return 0;
    }
}
```
Master и второй тенант не затрагиваются; при старте приложения автоматического сброса нет.

- [ ] **Step 5:** `dotnet build` — успех.
- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Infrastructure: tenant provisioning with runtime DB users, seed, database initializer, demo-reset

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Web host, конфигурация, Docker

**Files:**
- Modify: `src/GovErp.Web/Program.cs`, `src/GovErp.Web/appsettings.json`
- Create: `src/GovErp.Web/Extensions/ServiceCollectionExtensions.cs`, `src/GovErp.Web/Dockerfile`, `docker-compose.yml`, `.env.example`, `.dockerignore`

- [ ] **Step 1: Program.cs** (аутентификация и страницы — план 4)

```csharp
using GovErp.Application.Web.Extensions;
using GovErp.Infrastructure.Extensions;
using GovErp.Infrastructure.Startup;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddApplication(builder.Configuration).AddInfrastructure(builder.Configuration).AddStartup(builder.Configuration);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var app = builder.Build();
if (args.FirstOrDefault() == "demo-reset")
{
    return await DemoReset.RunAsync(args, app.Services, app.Environment, CancellationToken.None);
}

await DatabaseInitializer.InitializeAsync(app.Services, CancellationToken.None);
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.UseStaticFiles();
app.UseAntiforgery();
app.MapRazorComponents<GovErp.Web.Components.App>().AddInteractiveServerRenderMode();
await app.RunAsync();
return 0;
```
`AddStartup` (в `Infrastructure/Extensions`) регистрирует `StartupOptions` (секция `Startup`) и `TenantProvisioner` (scoped). Шаги 2 и 4 `DatabaseInitializer` (миграция и seed Master) тоже используют `MasterDbContext` на migration-строке, созданный так же, как `MasterForWrite`.

- [ ] **Step 2: appsettings.json** — только несекретные значения; секреты — переменные окружения:
```json
{
  "ConnectionStrings": { "Master": "" },
  "Tenancy": {
    "RuntimeConnectionTemplate": "Server=localhost,1433;Database={0};User Id={1};Password={2};TrustServerCertificate=True",
    "Credentials": { "springfield": { "User": "goverp_springfield" }, "shelbyville": { "User": "goverp_shelbyville" } }
  },
  "Startup": {
    "MasterDatabase": "GovErp_Master",
    "MasterRuntimeLogin": "goverp_master_ro",
    "DemoResetAllowlist": [ "GovErp_Springfield" ]
  },
  "Clock": { "BusinessDate": "2026-06-15" },
  "Posting": { "AccountsPayableObject": "2100", "ReserveForEncumbrancesObject": "2900", "EncumbrancesObject": "5900", "BalanceSheetDepartment": "0000" },
  "Explanation": { "Provider": "Template" }
}
```

- [ ] **Step 3: Dockerfile** (`src/GovErp.Web/Dockerfile`, контекст — корень репозитория):
```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY . .
RUN dotnet publish src/GovErp.Web/GovErp.Web.csproj -c Release -o /app/publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app/publish .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "GovErp.Web.dll"]
```
`.dockerignore`: `**/bin`, `**/obj`, `.git`, `.vs`, `**/TestResults`, `.env`.

- [ ] **Step 4: docker-compose.yml**
```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${SA_PASSWORD}"
    ports: ["127.0.0.1:1433:1433"]   # только для локальных инструментов; наружу не публикуется
    volumes: ["sqldata:/var/opt/mssql"]
    healthcheck:
      test: ["CMD-SHELL", "/opt/mssql-tools18/bin/sqlcmd -S localhost -U sa -P \"$$MSSQL_SA_PASSWORD\" -C -Q 'SELECT 1' || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 12
  web:
    build: { context: ., dockerfile: src/GovErp.Web/Dockerfile }
    depends_on: { sqlserver: { condition: service_healthy } }
    environment:
      ASPNETCORE_ENVIRONMENT: "Demo"
      Startup__MigrationConnectionTemplate: "Server=sqlserver;Database={0};User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True"
      Startup__MasterRuntimePassword: "${MASTER_RO_PASSWORD}"
      ConnectionStrings__Master: "Server=sqlserver;Database=GovErp_Master;User Id=goverp_master_ro;Password=${MASTER_RO_PASSWORD};TrustServerCertificate=True"
      Tenancy__RuntimeConnectionTemplate: "Server=sqlserver;Database={0};User Id={1};Password={2};TrustServerCertificate=True"
      Tenancy__Credentials__springfield__Password: "${SPRINGFIELD_DB_PASSWORD}"
      Tenancy__Credentials__shelbyville__Password: "${SHELBYVILLE_DB_PASSWORD}"
      Explanation__Provider: "${EXPLANATION_PROVIDER:-Template}"
      ANTHROPIC_API_KEY: "${ANTHROPIC_API_KEY:-}"
    ports: ["127.0.0.1:8080:8080"]
volumes:
  sqldata:
```
`.env.example`:
```
SA_PASSWORD=GovErp!Migrate2026
MASTER_RO_PASSWORD=GovErp!MasterRo2026
SPRINGFIELD_DB_PASSWORD=GovErp!Springfield2026
SHELBYVILLE_DB_PASSWORD=GovErp!Shelbyville2026
EXPLANATION_PROVIDER=Template
ANTHROPIC_API_KEY=
```
Runtime-приложение использует `sa` только в `Startup__MigrationConnectionTemplate` (миграции и провижининг при старте); все запросы пользователей идут под `goverp_*` (GE-18).

- [ ] **Step 5: Проверка запуска и сохранности данных**

```powershell
Copy-Item .env.example .env
docker compose up --build -d
docker compose logs web --tail 50      # ожидается: миграции, provisioning springfield/shelbyville, "Now listening on: http://[::]:8080"
curl http://localhost:8080/health       # 200 {"status":"ok"}
docker compose restart web
docker compose logs web --tail 20      # seed не повторяется: БД не пустые
```
`docker compose down -v` в штатную проверку не входит — это удаление данных. Проверить сброс демо: `docker compose exec web dotnet GovErp.Web.dll demo-reset --tenant springfield --confirm springfield` → код 0; `--tenant shelbyville --confirm shelbyville` → код 3.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Web host with database initialization and demo-reset; Docker Compose with persistent SQL Server volume

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Интеграционная инфраструктура и гонки/атомарность

**Files:**
- Create: `tests/GovErp.Application.Web.Tests/Support/SqlServerFixture.cs`, `Support/TenantDriver.cs`, `Support/TestHooks.cs`
- Create: `tests/GovErp.Application.Web.Tests/Integration/ConcurrencyAndAtomicityTests.cs`

**Interfaces:**
- `SqlServerFixture : IAsyncLifetime` (xUnit collection fixture `"sql"`): поднимает `MsSqlContainer`, строит `ServiceProvider` из `AddApplication` + `AddInfrastructure` + `AddStartup` на конфигурации, указывающей на контейнер (migration = `sa`; runtime-учётки `test` → `goverp_test`), подменяет декораторы из `TestHooks`, выполняет `DatabaseInitializer`. `CreateTenantAsync()` — **новая** tenant-БД `GovErp_T_{guid:N}` с полным seed Springfield (consistency §7: тесты не делят изменяемые остатки).
- `TenantDriver` — акторы тенанта (`Clerk`, `FireChief`, `PoliceChief`, `PwDirector`, `WaterDirector`, `GrantsManager`, `BudgetOfficer`, `FinanceDirector`, конструируются как `ActorContext` с id из `SpringfieldData`/`MasterSeed`), сервисы и помощники: `Env()`, `CreateAsync(...)`, `SubmitAsync`, `ApproveThroughAsync` (override всех открытых Soft Stop директором, затем согласование каждого шага маршрута подходящим актором), `PostAsync`, `BudgetAsync(account)`, `WithDbAsync(...)`.
- `TestHooks` — синглтон с точками вмешательства: `SyncPoint? AfterBudgetRead`, `SyncPoint? AfterPoRead`, `SyncPoint? AfterDuplicateCheck` (гонки синхронизируются барьером **после чтения**, каждая операция — в своём scope; consistency §7), `string? FailOnSecondLookupOf` (ошибка при втором чтении бюджетной строки с этим счётом в scope), `bool FailJournalWrite`. Декораторы `Hooked*Repository` оборачивают `Ef*Repository`.

- [ ] **Step 1: Фикстура, драйвер, хуки**

`Support/TestHooks.cs`:
```csharp
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Repositories;
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Repositories;

namespace GovErp.Application.Web.Tests;

/// <summary>
/// Точка синхронизации гонки: первые Participants прибытий ждут друг друга (все прочитали данные), остальные проходят сразу —
/// так повтор после конфликта и повторные чтения в той же операции не зависают.
/// </summary>
public sealed class SyncPoint(int participants)
{
    private readonly Barrier _barrier = new(participants);
    private int _arrivals;

    public void Arrive()
    {
        if (Interlocked.Increment(ref _arrivals) <= participants)
        {
            _barrier.SignalAndWait(TimeSpan.FromSeconds(30));
        }
    }
}

public sealed class TestHooks
{
    public SyncPoint? AfterBudgetRead { get; set; }
    public SyncPoint? AfterPoRead { get; set; }
    public SyncPoint? AfterDuplicateCheck { get; set; }
    public string? FailOnSecondLookupOf { get; set; }
    public bool FailJournalWrite { get; set; }

    public void Reset() { AfterBudgetRead = AfterPoRead = AfterDuplicateCheck = null; FailOnSecondLookupOf = null; FailJournalWrite = false; }
}

public sealed class HookedBudgetLineRepository(IBudgetLineRepository inner, TestHooks hooks) : IBudgetLineRepository
{
    private readonly Dictionary<AccountCode, int> _lookups = [];

    public async Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fy, CancellationToken ct = default)
    {
        var line = await inner.FindAsync(account, fy, ct);
        _lookups[account] = _lookups.GetValueOrDefault(account) + 1;
        if (hooks.FailOnSecondLookupOf == account.ToString() && _lookups[account] == 2)
        {
            throw new InvalidOperationException("Injected failure on budget line lookup.");
        }

        hooks.AfterBudgetRead?.Arrive();
        return line;
    }

    public Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fy, CancellationToken ct = default) => inner.ListAsync(fy, ct);
    public Task<IReadOnlyList<BudgetLine>> ListHeldForInvoiceAsync(Guid invoiceId, CancellationToken ct = default) => inner.ListHeldForInvoiceAsync(invoiceId, ct);
    public Task AddAsync(BudgetLine line, CancellationToken ct = default) => inner.AddAsync(line, ct);
}
```
По тому же образцу: `HookedPurchaseOrderRepository` (`hooks.AfterPoRead?.Arrive()` после `FindByNumberAsync`), `HookedVendorInvoiceRepository` (`hooks.AfterDuplicateCheck?.Arrive()` после `ExistsDuplicateAsync`), `HookedJournalRepository` (`AddAsync` бросает `InvalidOperationException`, если `FailJournalWrite`). Первые два прибытия в точку — чтения сборщика снимка у двух участников; повторные чтения (резервирование, повтор после конфликта) проходят без ожидания.

`Support/SqlServerFixture.cs`:
```csharp
using GovErp.Application.Web.Extensions;
using GovErp.Infrastructure.Extensions;
using GovErp.Infrastructure.Persistence.Repositories;
using GovErp.Infrastructure.Startup;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;

namespace GovErp.Application.Web.Tests;

[CollectionDefinition("sql")]
public sealed class SqlCollection : ICollectionFixture<SqlServerFixture>;

public sealed class SqlServerFixture : IAsyncLifetime
{
    private const string RuntimeUser = "goverp_test";
    private const string RuntimePassword = "GovErp!Test2026";
    private readonly MsSqlContainer _sql = new MsSqlBuilder().Build();

    public ServiceProvider Services { get; private set; } = null!;
    public TestHooks Hooks { get; } = new();

    public async Task InitializeAsync()
    {
        await _sql.StartAsync();
        var sa = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(_sql.GetConnectionString());
        string Template(string user, string password) =>
            $"Server={sa.DataSource};Database={{0}};User Id={user};Password={password};TrustServerCertificate=True";

        var cfg = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Startup:MigrationConnectionTemplate"] = Template(sa.UserID, sa.Password),
            ["Startup:MasterDatabase"] = "GovErp_Master",
            ["Startup:MasterRuntimeLogin"] = "goverp_master_ro",
            ["Startup:MasterRuntimePassword"] = "GovErp!MasterRo2026",
            ["Startup:DemoResetAllowlist:0"] = "GovErp_Springfield",
            ["ConnectionStrings:Master"] = string.Format(Template("goverp_master_ro", "GovErp!MasterRo2026"), "GovErp_Master"),
            ["Tenancy:RuntimeConnectionTemplate"] = $"Server={sa.DataSource};Database={{0}};User Id={{1}};Password={{2}};TrustServerCertificate=True",
            ["Tenancy:Credentials:springfield:User"] = "goverp_springfield", ["Tenancy:Credentials:springfield:Password"] = "GovErp!Springfield2026",
            ["Tenancy:Credentials:shelbyville:User"] = "goverp_shelbyville", ["Tenancy:Credentials:shelbyville:Password"] = "GovErp!Shelbyville2026",
            ["Tenancy:Credentials:test:User"] = RuntimeUser, ["Tenancy:Credentials:test:Password"] = RuntimePassword,
            ["Clock:BusinessDate"] = "2026-07-15",
        }).Build();

        var services = new ServiceCollection().AddLogging();
        services.AddApplication(cfg).AddInfrastructure(cfg).AddStartup(cfg);
        services.AddSingleton(Hooks);
        services.AddScoped<EfBudgetLineRepository>().AddScoped<IBudgetLineRepository>(sp =>
            new HookedBudgetLineRepository(sp.GetRequiredService<EfBudgetLineRepository>(), Hooks));
        services.AddScoped<EfPurchaseOrderRepository>().AddScoped<IPurchaseOrderRepository>(sp =>
            new HookedPurchaseOrderRepository(sp.GetRequiredService<EfPurchaseOrderRepository>(), Hooks));
        services.AddScoped<EfVendorInvoiceRepository>().AddScoped<IVendorInvoiceRepository>(sp =>
            new HookedVendorInvoiceRepository(sp.GetRequiredService<EfVendorInvoiceRepository>(), Hooks));
        services.AddScoped<EfJournalRepository>().AddScoped<IJournalRepository>(sp =>
            new HookedJournalRepository(sp.GetRequiredService<EfJournalRepository>(), Hooks));
        Services = services.BuildServiceProvider(validateScopes: true);
        await DatabaseInitializer.InitializeAsync(Services, CancellationToken.None);
    }

    public async Task<TenantDriver> CreateTenantAsync()
    {
        Hooks.Reset();
        var id = $"t{Guid.NewGuid():N}"[..20];
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TenantProvisioner>()
            .ProvisionAsync(id, $"Test {id}", $"GovErp_T_{id}", "test", isDemo: false, TenantSeed.Springfield, CancellationToken.None);
        return new TenantDriver(this, new TenantId(id));
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _sql.DisposeAsync();
    }
}
```
`Clock:BusinessDate = 2026-07-15` — чтобы `ReadyForPaymentHandoff` было достижимо для инвойсов с due 2026-07-15; даты документов остаются 2026-06-15.

`Support/TenantDriver.cs` (ключевые помощники; все вызовы проверяют статус и падают с `Reason` при неожиданном отказе):
```csharp
public sealed class TenantDriver(SqlServerFixture fixture, TenantId tenant)
{
    public TenantId Tenant { get; } = tenant;
    public ActorContext Clerk => Actor(SpringfieldData.ClerkId, "ap.clerk", Roles.ApClerk);
    public ActorContext FireChief => Actor(MasterSeed.FireChiefId, "fire.chief", Roles.DepartmentHead, "6000");
    public ActorContext PoliceChief => Actor(MasterSeed.PoliceChiefId, "police.chief", Roles.DepartmentHead, "3000");
    public ActorContext PwDirector => Actor(MasterSeed.PwDirectorId, "pw.director", Roles.DepartmentHead, "4000");
    public ActorContext WaterDirector => Actor(MasterSeed.WaterDirectorId, "water.director", Roles.DepartmentHead, "5000");
    public ActorContext GrantsManager => Actor(MasterSeed.GrantsManagerId, "grants.manager", Roles.GrantsManager);
    public ActorContext BudgetOfficer => Actor(MasterSeed.BudgetOfficerId, "budget.officer", Roles.BudgetOfficer);
    public ActorContext FinanceDirector => Actor(MasterSeed.FinanceDirectorId, "finance.director", Roles.FinanceDirector);
    public ActorContext[] Approvers => [FireChief, PoliceChief, PwDirector, WaterDirector, GrantsManager, BudgetOfficer, FinanceDirector];

    public TestHooks Hooks => fixture.Hooks;
    public T Service<T>() where T : notnull => fixture.Services.GetRequiredService<T>();
    public static CommandEnvelope Env(string? rowVersion = null) => new(Guid.NewGuid(), rowVersion);

    private ActorContext Actor(UserId id, string name, string role, string? dept = null) => new(Tenant, id, name, new HashSet<string> { role }, dept);

    public Task<InvoiceVm> GetAsync(Guid id) => Service<IInvoiceAppService>().GetAsync(id, Clerk);

    public async Task<InvoiceVm> CreateAsync(decimal total, string? po, params (string Account, decimal Amount, int? PoLine)[] lines)
    {
        var r = await Service<IInvoiceAppService>().CreateDraftAsync(new CreateInvoiceCommand(Env(), $"T-{Guid.NewGuid():N}"[..12],
            SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, new DateOnly(2026, 7, 15), total, po,
            lines.Select(l => new DistributionCommand(l.Account, l.Amount, l.PoLine)).ToList()), Clerk);
        r.IsAccepted.Should().BeTrue(r.Reason);
        return r.Value!;
    }

    public async Task<CommandResult<InvoiceVm>> SubmitAsync(Guid id) =>
        await Service<IInvoiceAppService>().SubmitAsync(new InvoiceActionCommand(Env((await GetAsync(id)).RowVersion), id), Clerk);

    /// <summary>Override всех открытых Soft Stop директором, затем согласование каждого шага подходящим актором — до статуса Approved.</summary>
    public async Task ApproveThroughAsync(Guid id)
    {
        for (var guard = 0; guard < 20; guard++)
        {
            var inv = await GetAsync(id);
            if (inv.Status == "Approved") { return; }
            var eval = inv.LastEvaluation!;
            var soft = eval.Outcomes.FirstOrDefault(o => o.Severity == "SoftStop" && o.OverriddenBy is null);
            if (soft is not null)
            {
                var o = await Service<IApprovalAppService>().OverrideAsync(new OverrideCommand(Env(inv.RowVersion), id, eval.Id, soft.RuleId, soft.Line, "test justification"), FinanceDirector);
                o.IsAccepted.Should().BeTrue(o.Reason);
                continue;
            }

            var step = eval.ApprovalRoute.First(r => !r.IsSatisfied);
            var actor = Approvers.First(a => a.IsInRole(step.Role) && (step.Department is null || a.DepartmentCode == step.Department));
            var r = await Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(Env(inv.RowVersion), id), actor);
            r.IsAccepted.Should().BeTrue(r.Reason);
        }

        throw new InvalidOperationException("Approval loop did not converge.");
    }

    public async Task<CommandResult<InvoiceVm>> PostAsync(Guid id, CommandEnvelope? env = null) =>
        await Service<IPostingAppService>().PostAsync(new InvoiceActionCommand(env ?? Env((await GetAsync(id)).RowVersion), id), FinanceDirector);

    public async Task<BudgetLineVm> BudgetAsync(string account) =>
        (await Service<IBudgetAppService>().ListAsync(2026, BudgetOfficer)).Single(b => b.Account == account);

    public Task<T> WithDbAsync<T>(Func<GovErpDbContext, Task<T>> query) =>
        Service<ITenantOperationRunner>().QueryAsync(Clerk, (sp, _) => query(sp.GetRequiredService<GovErpDbContext>()));
}
```
`MasterSeed` публикует id пользователей константами (`FireChiefId`, …), чтобы тесты и seed совпадали.

- [ ] **Step 2: Тесты гонок и атомарности**

`Integration/ConcurrencyAndAtomicityTests.cs`:
```csharp
namespace GovErp.Application.Web.Tests.Integration;

[Collection("sql")]
public sealed class ConcurrencyAndAtomicityTests(SqlServerFixture fixture)
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private const string Police = "701-3000-53100-G-COPS-26";

    [Fact]
    public async Task OwnReservationIsNotChargedTwice()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(100_000m, null, (Fire, 100_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.BudgetAsync(Fire)).Held.Should().Be(100_000m);
        await t.ApproveThroughAsync(inv.Id);                          // перевалидации на Approve не видят дефицита
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Fire);
        (line.Available, line.Held, line.Actuals).Should().Be((47_000m, 0m, 232_000m));
    }

    [Fact]
    public async Task SameBudgetAcrossDistributions()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, null, (Fire, 80_000m, null), (Fire, 80_000m, null));
        var r = await t.SubmitAsync(inv.Id);
        r.Status.Should().Be(CommandStatus.Refused);
        r.Value!.LastEvaluation!.Outcomes.Single(o => o.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
        (await t.BudgetAsync(Fire)).Held.Should().Be(0m);
    }

    [Fact]
    public async Task ParallelBudgetSubmits()
    {
        var t = await fixture.CreateTenantAsync();
        var a = await t.CreateAsync(100_000m, null, (Fire, 100_000m, null));
        var b = await t.CreateAsync(100_000m, null, (Fire, 100_000m, null));
        t.Hooks.AfterBudgetRead = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => t.SubmitAsync(a.Id)), Task.Run(() => t.SubmitAsync(b.Id)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        results.Single(r => !r.IsAccepted).Status.Should().BeOneOf(CommandStatus.Refused, CommandStatus.Conflict);
        (await t.BudgetAsync(Fire)).Held.Should().Be(100_000m);
    }

    [Fact]
    public async Task ParallelPoClaims()
    {
        var t = await fixture.CreateTenantAsync();
        var amend = await t.Service<IBudgetAppService>().AmendAsync(
            new AmendBudgetCommand(TenantDriver.Env(), Police, 2026, -240_000m, "BA-T-ZERO", SpringfieldData.Jun15), t.BudgetOfficer);
        amend.IsAccepted.Should().BeTrue(amend.Reason);                // свободный бюджет Police = 0
        var a = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        var b = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        t.Hooks.AfterPoRead = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => t.SubmitAsync(a.Id)), Task.Run(() => t.SubmitAsync(b.Id)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        var claims = await t.WithDbAsync(db => db.Encumbrances.Where(e => e.PoLineRef == "PO-2026-0451/1").SelectMany(e => e.Claims)
            .CountAsync(c => c.Status == Domain.Ledger.Entities.ClaimStatus.Held));
        claims.Should().Be(1);
    }

    [Fact]
    public async Task PoPostingTotals()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Police);
        (line.Actuals, line.Encumbered, line.Available).Should().Be((260_000m, 0m, 240_000m));
        (await t.Service<IPostingAppService>().GetJournalAsync(inv.Id, t.Clerk)).Should().HaveCount(4);
    }

    [Fact]
    public async Task MixedPostingTotals()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, "PO-2026-0451", (Police, 164_800m, 1));   // 3% сверх PO — в допуске
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Police);
        (line.Actuals, line.Encumbered, line.Available).Should().Be((264_800m, 0m, 235_200m));
    }

    [Fact]
    public async Task AtomicSubmitFailure()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(30_000m, null, ("101-6000-53100", 12_000m, null), ("202-4000-53100", 8_000m, null), ("501-5000-53100", 10_000m, null));
        t.Hooks.FailOnSecondLookupOf = "202-4000-53100";               // первое чтение — сборщик снимка, второе — резервирование
        await FluentActions.Awaiting(() => t.SubmitAsync(inv.Id)).Should().ThrowAsync<InvalidOperationException>();
        t.Hooks.Reset();
        (await t.GetAsync(inv.Id)).Status.Should().Be("Draft");
        foreach (var account in new[] { "101-6000-53100", "202-4000-53100", "501-5000-53100" })
        {
            (await t.BudgetAsync(account)).Held.Should().Be(0m);
        }
    }

    [Fact]
    public async Task AtomicPostFailure()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        var before = await t.BudgetAsync(Police);
        t.Hooks.FailJournalWrite = true;
        await FluentActions.Awaiting(() => t.PostAsync(inv.Id)).Should().ThrowAsync<InvalidOperationException>();
        t.Hooks.Reset();
        (await t.GetAsync(inv.Id)).Status.Should().Be("Approved");
        (await t.BudgetAsync(Police)).Should().BeEquivalentTo(before);
        (await t.WithDbAsync(db => db.Encumbrances.Where(e => e.PoLineRef == "PO-2026-0451/1").Select(e => e.Liquidated).SingleAsync()))
            .Should().Be(Money.Zero);
    }

    [Fact]
    public async Task SameCommandRepeated()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        var env = TenantDriver.Env((await t.GetAsync(inv.Id)).RowVersion);
        var first = await t.PostAsync(inv.Id, env);
        var second = await t.PostAsync(inv.Id, env);
        first.IsAccepted.Should().BeTrue();
        second.Should().BeEquivalentTo(first);
        (await t.WithDbAsync(db => db.JournalEntries.CountAsync(j => j.SourceRef == inv.Reference))).Should().Be(1);
        (await t.WithDbAsync(db => db.CommandReceipts.CountAsync(r => r.CommandId == env.CommandId))).Should().Be(1);
    }

    [Fact]
    public async Task SameCommandDifferentPayload()
    {
        var t = await fixture.CreateTenantAsync();
        var svc = t.Service<IInvoiceAppService>();
        var env = TenantDriver.Env();
        CreateInvoiceCommand Cmd(string number) => new(env, number, SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15,
            SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 10m, null, [new DistributionCommand("101-6000-53100", 10m, null)]);
        (await svc.CreateDraftAsync(Cmd("P-1"), t.Clerk)).IsAccepted.Should().BeTrue();
        var second = await svc.CreateDraftAsync(Cmd("P-2"), t.Clerk);
        second.Status.Should().Be(CommandStatus.Conflict);
        second.Retryable.Should().BeFalse();
    }
}
```

- [ ] **Step 3:** Запустить Docker, затем `dotnet test tests/GovErp.Application.Web.Tests --filter "FullyQualifiedName~ConcurrencyAndAtomicity"` — Expected: 10 passed. Первый запуск дольше (образ SQL Server). Тесты гонок прогнать 5 раз подряд (`--filter "FullyQualifiedName~Parallel"`, повторить) — все зелёные: барьер делает гонку детерминированной.
- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "Integration tests: budget and PO races, atomic submit and post, idempotent commands

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 9: Интеграционные тесты жизненного цикла и платформы

**Files:**
- Create: `tests/GovErp.Application.Web.Tests/Integration/LifecycleTests.cs`, `Integration/PlatformTests.cs`

- [ ] **Step 1: Жизненный цикл**

`Integration/LifecycleTests.cs`:
```csharp
namespace GovErp.Application.Web.Tests.Integration;

[Collection("sql")]
public sealed class LifecycleTests(SqlServerFixture fixture)
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private const string Police = "701-3000-53100-G-COPS-26";

    [Fact]
    public async Task ExerciseEndToEnd_HardStop_Amend_Override_Warning_Post()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, null, (Fire, 160_000m, null));
        var refused = await t.SubmitAsync(inv.Id);
        refused.Status.Should().Be(CommandStatus.Refused);
        refused.Value!.LastEvaluation!.Overall.Should().Be("HardStop");

        (await t.Service<IBudgetAppService>().AmendAsync(new AmendBudgetCommand(TenantDriver.Env(), Fire, 2026, 13_000m, "BA-2026-14", SpringfieldData.Jun15), t.BudgetOfficer))
            .IsAccepted.Should().BeTrue();
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();   // Soft Stop (procurement) не мешает Submit
        await t.ApproveThroughAsync(inv.Id);                          // override → Warning (остаток 0 < 10%)
        (await t.GetAsync(inv.Id)).LastEvaluation!.Overall.Should().Be("Warning");
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Fire);
        (line.Actuals, line.Held, line.Available).Should().Be((292_000m, 0m, 0m));
    }

    [Fact]
    public async Task OpeningBalancesReconcile()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var line = await t.BudgetAsync(Police);
        line.OpeningActuals.Should().Be(100_000m);                    // снимок не меняется
        line.Actuals.Should().Be(line.OpeningActuals + 160_000m);
    }

    [Fact]
    public async Task ReapprovalAfterRuleChange()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        await t.WithDbAsync(async db =>
        {
            db.RuleDefinitions.Add(new Domain.Validation.Entities.RuleDefinition("BUDGET_LOW_REMAINING", 2,
                Domain.Validation.ValueObjects.ValidationStep.BudgetAvailability, Domain.Validation.ValueObjects.RuleLayer.Tenant, true,
                Domain.Validation.ValueObjects.Severity.Warning, new Dictionary<string, string> { ["pct"] = "0.12" }, [],
                new DateOnly(2025, 7, 1), null, "Little budget remains after this invoice.", "No action required."));
            return await db.SaveChangesAsync();
        });

        var refused = await t.PostAsync(inv.Id);
        refused.Status.Should().Be(CommandStatus.Refused);
        refused.Reason.Should().Contain("REVALIDATION_REQUIRED");
        (await t.GetAsync(inv.Id)).Status.Should().Be("Submitted");  // открыт новый цикл

        await t.ApproveThroughAsync(inv.Id);                          // согласование по актуальному fingerprint
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
    }

    [Fact]
    public async Task ApprovalPreservesContentVersion()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var before = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(TenantDriver.Env(before.RowVersion), inv.Id), t.PoliceChief)).IsAccepted.Should().BeTrue();
        var after = await t.GetAsync(inv.Id);
        after.ContentVersion.Should().Be(before.ContentVersion);
        after.RowVersion.Should().NotBe(before.RowVersion);
        (await t.BudgetAsync(Police)).Held.Should().Be(0m);          // полностью ликвидируемый PO-инвойс резерв не берёт
    }

    [Fact]
    public async Task ResubmitDoesNotReuseOldApprovals()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(TenantDriver.Env(v.RowVersion), inv.Id), t.PoliceChief)).IsAccepted.Should().BeTrue();
        v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(v.RowVersion), inv.Id, "fix"), t.Clerk)).IsAccepted.Should().BeTrue();
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var resubmitted = await t.GetAsync(inv.Id);
        resubmitted.ApprovalCycleId.Should().NotBe(v.ApprovalCycleId);
        resubmitted.Approvals.Should().ContainSingle(a => !a.IsActiveCycle);
        (await t.Service<IApprovalAppService>().GetQueueAsync(t.PoliceChief)).Should().Contain(q => q.InvoiceId == inv.Id);
    }

    [Fact]
    public async Task WithdrawReleasesEverything()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(164_800m, "PO-2026-0451", (Police, 164_800m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.BudgetAsync(Police)).Held.Should().Be(4_800m);
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(v.RowVersion), inv.Id, "wrong amount"), t.Clerk))
            .IsAccepted.Should().BeTrue();
        (await t.BudgetAsync(Police)).Held.Should().Be(0m);
        (await t.WithDbAsync(db => db.Encumbrances.SelectMany(e => e.Claims).CountAsync(c => c.Status == Domain.Ledger.Entities.ClaimStatus.Held))).Should().Be(0);
        (await t.WithDbAsync(db => db.PurchaseOrders.SelectMany(p => p.Lines).SelectMany(l => l.BillingClaims)
            .CountAsync(c => c.Status == Domain.Payables.Entities.ClaimStatus.Held))).Should().Be(0);
        (await t.GetAsync(inv.Id)).Status.Should().Be("Draft");
        (await t.WithDbAsync(db => db.JournalEntries.CountAsync())).Should().Be(0);
        (await t.Service<IExplanationAppService>().GetAuditTrailAsync(inv.Id, t.Clerk)).Should().Contain(e => e.Action == "InvoiceWithdrawn");
    }

    [Fact]
    public async Task PostedInvoiceCannotBeWithdrawn()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        var before = await t.BudgetAsync(Police);
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().WithdrawAsync(new ReasonedActionCommand(TenantDriver.Env(v.RowVersion), inv.Id, "late"), t.Clerk))
            .Status.Should().Be(CommandStatus.Refused);
        (await t.BudgetAsync(Police)).Should().BeEquivalentTo(before);
    }

    [Fact]
    public async Task SoftStopCanHoldButCannotPost()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(12_000m, null, ("101-6000-53100", 12_000m, null));
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.BudgetAsync("101-6000-53100")).Held.Should().Be(12_000m);   // дефицит 2,000 удерживается
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IApprovalAppService>().ApproveAsync(new InvoiceActionCommand(TenantDriver.Env(v.RowVersion), inv.Id), t.FireChief))
            .Status.Should().Be(CommandStatus.Refused);                    // неснятый Soft Stop
        (await t.PostAsync(inv.Id)).Status.Should().Be(CommandStatus.Refused);
    }

    [Fact]
    public async Task CumulativePoToleranceCannotBeSplit()
    {
        var t = await fixture.CreateTenantAsync();
        var full = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.SubmitAsync(full.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(full.Id);
        (await t.PostAsync(full.Id)).IsAccepted.Should().BeTrue();     // PO выставлен полностью

        var a = await t.CreateAsync(5_000m, "PO-2026-0451", (Police, 5_000m, 1));   // по отдельности 3.125% — в допуске
        var b = await t.CreateAsync(5_000m, "PO-2026-0451", (Police, 5_000m, 1));   // вместе 6.25% — сверх 5%
        t.Hooks.AfterPoRead = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => t.SubmitAsync(a.Id)), Task.Run(() => t.SubmitAsync(b.Id)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        results.Single(r => !r.IsAccepted).Status.Should().BeOneOf(CommandStatus.Refused, CommandStatus.Conflict);
    }

    [Fact]
    public async Task DuplicateRaceHasOneWinner()
    {
        var t = await fixture.CreateTenantAsync();
        var svc = t.Service<IInvoiceAppService>();
        CreateInvoiceCommand Cmd(string number) => new(TenantDriver.Env(), number, SpringfieldData.AcmeId, SpringfieldData.Jun15, SpringfieldData.Jun15,
            SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 10m, null, [new DistributionCommand("101-6000-53100", 10m, null)]);
        t.Hooks.AfterDuplicateCheck = new SyncPoint(2);
        var results = await Task.WhenAll(Task.Run(() => svc.CreateDraftAsync(Cmd(" ABC "), t.Clerk)), Task.Run(() => svc.CreateDraftAsync(Cmd("abc"), t.Clerk)));
        t.Hooks.Reset();
        results.Count(r => r.IsAccepted).Should().Be(1);
        results.Single(r => !r.IsAccepted).Reason.Should().Contain("already exists");
    }

    [Fact]
    public async Task PaymentHandoffReadiness()
    {
        var t = await fixture.CreateTenantAsync();                    // BusinessDate фикстуры — 2026-07-15 = DueDate
        var inv = await t.CreateAsync(160_000m, "PO-2026-0451", (Police, 160_000m, 1));
        (await t.GetAsync(inv.Id)).ReadyForPaymentHandoff.Should().BeFalse();   // не Posted
        (await t.SubmitAsync(inv.Id)).IsAccepted.Should().BeTrue();
        await t.ApproveThroughAsync(inv.Id);
        (await t.PostAsync(inv.Id)).IsAccepted.Should().BeTrue();
        (await t.GetAsync(inv.Id)).ReadyForPaymentHandoff.Should().BeTrue();
        var v = await t.GetAsync(inv.Id);
        (await t.Service<IInvoiceAppService>().SetPaymentHoldAsync(new PaymentHoldCommand(TenantDriver.Env(v.RowVersion), inv.Id, true), t.FinanceDirector))
            .IsAccepted.Should().BeTrue();
        (await t.GetAsync(inv.Id)).ReadyForPaymentHandoff.Should().BeFalse();
    }
}
```

- [ ] **Step 2: Платформа**

`Integration/PlatformTests.cs`:
```csharp
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
        (await t.Service<IInvoiceAppService>().ListAsync(shelby)).Should().BeEmpty();
        await FluentActions.Awaiting(() => t.Service<IInvoiceAppService>().GetAsync(inv.Id, shelby)).Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task SignInResolvesTenantRolesAndDepartment()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var signIn = scope.ServiceProvider.GetRequiredService<ISignIn>();
        var chief = await signIn.AuthenticateAsync("fire.chief", "Demo!2026");
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
            db.Entry(record).Property(r => r.Overall).CurrentValue = Domain.Validation.ValueObjects.Severity.Allowed;
            return await db.SaveChangesAsync();
        })).Should().ThrowAsync<InvalidOperationException>().WithMessage("*append-only*");

        await FluentActions.Awaiting(() => t.WithDbAsync(db => db.Database.ExecuteSqlRawAsync("UPDATE audit.Events SET Action = N'x'")))
            .Should().ThrowAsync<Microsoft.Data.SqlClient.SqlException>();   // DENY для runtime-пользователя
    }

    [Fact]
    public async Task StaleFormGetsConflict()
    {
        var t = await fixture.CreateTenantAsync();
        var inv = await t.CreateAsync(10m, null, ("101-6000-53100", 10m, null));
        var stale = (await t.GetAsync(inv.Id)).RowVersion;
        (await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(TenantDriver.Env(stale), inv.Id, SpringfieldData.AcmeId,
            SpringfieldData.Jun15, SpringfieldData.Jun15, SpringfieldData.Jun15, new DateOnly(2026, 7, 15), 20m, null,
            [new DistributionCommand("101-6000-53100", 20m, null)]), t.Clerk)).IsAccepted.Should().BeTrue();
        var second = await t.Service<IInvoiceAppService>().UpdateDraftAsync(new UpdateInvoiceCommand(TenantDriver.Env(stale), inv.Id, SpringfieldData.AcmeId,
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
```
Тест `ExplanationLlmFailureFallsBack` — в плане 4 (там появляется LLM-генератор).

- [ ] **Step 3:** `dotnet test tests/GovErp.Application.Web.Tests` — Expected: все тесты проекта зелёные (unit + 10 + 11 + 5). Если какой-то тест не выявляет отсутствующее поведение при намеренно сломанной реализации (например, закомментировать `ReleaseAllAsync` в Withdraw), тест недостаточен — усилить.
- [ ] **Step 4: Commit и push**

```bash
git add -A
git commit -m "Integration tests: lifecycle (reapproval, withdraw, soft stop hold, cumulative PO tolerance, duplicates, payment handoff) and platform

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

## Self-review

**Покрытие спеки:** §3.6 и порты — задача 1; §3.7 / GE-9 (сборщик, собственные резервы и claims, базовая линия согласования) — задача 2; §6.2 (схемы, owned-таблицы, уникальные индексы, rowversion, ChangeStamp) — задача 3; runner команд, receipts, concurrency guard, tenancy per operation, append-only на EF-пути, шаблонное объяснение — задача 4; §5.1–5.5 и правила жизненного цикла 1–9 (атомарные Submit/Approve/Override/Reject/Withdraw/Post, повторное согласование, SoftStop-резерв, накопительный допуск PO, дубликаты, даты, opening balances, готовность к оплате) — задача 5; правило 10 (demo-reset) и права runtime-пользователей — задача 6; Docker с постоянным томом — задача 7; матрица spec §7 и consistency §7 — задачи 8–9.

**Решения, фиксируемые планом:** (1) все мутации идут через `ITenantOperationRunner`: один `SaveChanges`, receipt в той же транзакции, повтор только целиком в новом scope; (2) `Refused` сохраняет оценку, аудит и receipt, но тело сценария обязано не менять финансовое состояние до решения об отказе; (3) `RowVersion` в ответе команды не заполняется — UI перечитывает инвойс (`GetAsync`); (4) заголовок инвойса и номер после создания не меняются: номер — идентичность для проверки дубликатов; (5) даты документа в демо обязаны совпадать, иначе отказ с объяснением (spec §5, правило 6); (6) `Validate` — тоже команда с receipt: она пишет оценку и аудит.

**Согласованность имён с планами 1–2:** `BudgetLine.Reserve(invoiceId, contentVersion, amount)`, `HeldFor`, `ReleaseAllFor`, `Commit`, `RecordLiquidation`; `Encumbrance.Claim/ClaimOf/ConsumeClaim/ReleaseAllFor`, `HeldClaims`; `PurchaseOrder.ClaimBilling/BillingClaimsOf/ConsumeBillingClaim/ReleaseBillingClaimsFor/OtherActiveBillingClaims/LineRef/Lines`; `VendorInvoice.ReplaceContent/Submit/RecordEvaluation/RecordApproval/MarkApproved/Override/OpenNewApprovalCycle/Reject/Withdraw/Post/SetPaymentHold/IsReadyForPaymentHandoff`, `ActiveApprovals`, `ActiveOverrides`, `Dates`, `NormalizedNumber`, `VendorInvoice.Normalize`; `ValidationSubject.BudgetKeys/RequiredNewBudget/EligibleLiquidation/PoAmount`; `EvaluationRecord.ContentVersion/ApprovalCycleId/RuleSetFingerprint/PostingCheck`; `PostingEligibility.RevalidationRequired`; `PostingPreviewComposer.Budgetary`.

**Сознательные упрощения:** reconciler висящих резервов не нужен — частичных резервов не бывает (атомарный Submit); outbox и доменные события — только на слайде (GE-7); interceptor + DENY защищают append-only, но администратор сервера БД технически может изменить данные — это вне модели угроз прототипа.
