# План 2: Validation — конвейер, правила, result model

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать контекст `GovErp.Domain.Validation`: чистый конвейер из восьми шагов над снимками, типизированные правила с параметрами из `RuleDefinition`, разрешение слоёв с обязательными и адаптируемыми правилами, fingerprint набора правил, result model (`EvaluationRecord`, `Capabilities`), маршрут согласования, posting preview и posting eligibility — всё проверяемо на голых числах без базы.

**Architecture:** Вход конвейера — `ValidationSubject` (value object из снимков, собирается слоем сценариев в плане 3). Бюджет проверяется по бюджетному ключу (счёт), а не по строке инвойса; ликвидация encumbrance считается по PO-строке с учётом чужих claims; собственный резерв инвойса прибавляется к available. Правила — классы `IValidationRule`, читающие параметры из `RuleDefinition`. Конфликты: внутри шага — строжайший; Hard Stop на шагах 1–6 прерывает; обязательное правило не заменяется локальным слоем. Override меняет агрегацию, не outcome. Выход — неизменяемый `EvaluationRecord` с fingerprint применённых правил.

**Tech Stack:** как в плане 1. Ни одного NuGet-пакета в `GovErp.Domain.Validation` (SHA-256 — из BCL).

**Spec:** `docs/superpowers/specs/2026-09-21-validation-engine-design.md` (разделы 3.5, 4.1–4.3, 5 «Завершённые правила жизненного цикла», 7). Манифест: `GE-9`, `GE-10`, `GE-11`, `GE-15`.

**Зависит от:** план 1 (Shared kernel: `Money`, `AccountCode`, `FiscalYear`, `UserId`; проект `GovErp.Domain.Validation` уже существует и ссылается на `GovErp.Domain.Shared`).

## Global Constraints

- Всё из плана 1.
- Уточнения `2026-09-22-plan-consistency.md` (§3, §5) и spec §4–5 уже внесены в задачи: `ContentVersion` (не SQL RowVersion) и `ApprovalCycleId` в снимке и записи; fingerprint вместо «максимума версии по слою»; override привязан к правилу, его версии и строке; DepartmentHead удовлетворяет только шаг своего департамента; готовность к оплате — не capability конвейера, а вычисление на инвойсе (план 1).
- `GovErp.Domain.Validation` не ссылается на другие контексты. Всё, что нужно от ChartOfAccounts/Ledger/Payables, приходит в снимках со **своими** перечислениями Validation (`FundKind`, `BudgetControl`, `GrantRule`, `FundRestriction`, `GrantEligibilityResult`, `ApproverRole`). Маппинг — в плане 3.
- Порядок шагов фиксирован: `ValidationStep` 1..8. Конфигурируются включённость, severity и параметры.
- Денежные значения в `Inputs`/`Computed` outcome'ов — строки в инвариантной культуре (`Money.ToString()`), чтобы JSON-колонки читались человеком.
- Идентификаторы правил — `UPPER_SNAKE_CASE`, ровно как в спеке 4.2.
- Демо-дата — 2026-06-15; в тестах июнь 2026.

---

## Структура файлов

```
src/GovErp.Domain.Validation/
  ValueObjects/
    Severity.cs, RuleLayer.cs, ValidationStep.cs, EvaluationTrigger.cs, ApproverRole.cs
    FundKind.cs, BudgetControl.cs, GrantRule.cs, FundRestriction.cs, GrantEligibilityResult.cs
    VendorSnapshot.cs, TransactionSnapshot.cs, CombinationSnapshot.cs, FundSnapshot.cs, GrantSnapshot.cs,
    BudgetSnapshot.cs, EncumbranceSnapshot.cs, PoLineSnapshot.cs, DistributionSnapshot.cs,
    ApprovalSnapshot.cs, OverrideSnapshot.cs, ApprovalBaseline.cs, PostingAccounts.cs, ValidationSubject.cs
    AppliedRule.cs, EffectiveRuleSet.cs, RuleOutcome.cs, Capabilities.cs, ApprovalRequirement.cs,
    PostingPreviewLine.cs, PostingCheck.cs
  Entities/
    RuleDefinition.cs, EvaluationRecord.cs
  DomainServices/
    IValidationRule.cs, RuleCatalog.cs, RuleResolution.cs, RuleSetGuard.cs, OutcomeAggregation.cs,
    ApprovalRouting.cs, PostingPreviewComposer.cs, PostingEligibility.cs, ValidationPipeline.cs
    Rules/
      RuleSupport.cs, SegRequiredRule.cs, SegGrantForbiddenRule.cs, CoaCombinationActiveRule.cs,
      FundDeptObjectAllowedRule.cs, GrantEligibleRule.cs, VendorEligibleRule.cs,
      ProcurementThresholdRule.cs, InvoiceDuplicateRule.cs, BudgetAvailabilityRule.cs,
      BudgetLowRemainingRule.cs, PoLiquidationRule.cs
  Repositories/
    IRuleDefinitionRepository.cs, IEvaluationRecordRepository.cs
  Exceptions/
    ValidationException.cs
tests/GovErp.Domain.Validation.Tests/
  Support/SubjectBuilder.cs, Support/DemoRules.cs
  ValidationSubjectTests.cs, RuleResolutionTests.cs, OutcomeAggregationTests.cs, CapabilitiesTests.cs
  Rules/Step1To4RulesTests.cs, Rules/BudgetRulesTests.cs, Rules/PoLiquidationRuleTests.cs
  ApprovalRoutingTests.cs, PostingPreviewTests.cs, PostingEligibilityTests.cs
  RuleSetGuardTests.cs, PipelineScenarioTests.cs
```

---

### Task 1: Перечисления, снимки и расчёт по бюджетному ключу

**Files:**
- Create: `src/GovErp.Domain.Validation/ValueObjects/*.cs` (перечисления и снимки, см. ниже)
- Create: `src/GovErp.Domain.Validation/Exceptions/ValidationException.cs`
- Create: `tests/GovErp.Domain.Validation.Tests/` (проект), `Support/SubjectBuilder.cs`
- Test: `tests/GovErp.Domain.Validation.Tests/ValidationSubjectTests.cs`

**Interfaces:**
- Produces: все снимки и `ValidationSubject` с чистыми вычислениями: `BudgetKeys`, `BudgetFor(AccountCode)`, `PoLine(string)`, `RequestedFor(AccountCode)`, `PoAmount(string poLineRef)`, `EligibleLiquidation(string poLineRef)`, `LiquidationFor(AccountCode)`, `RequiredNewBudget(AccountCode)`, `ProjectedAvailable(AccountCode)`, `CumulativeExcessPct(string poLineRef)`. `BudgetSnapshot.Available`, `.AvailableForInvoice`. `EncumbranceSnapshot.ClaimableForInvoice`. Тестовый `SubjectBuilder` — им пользуются все дальнейшие тесты.
- Формулы spec §4.1 и consistency §3:
  ```text
  Available            = Amended − Actuals − Encumbered − Held
  AvailableForInvoice  = Available + OwnHeld
  ClaimableForInvoice  = Remaining − OtherHeldClaims            (0, если encumbrance закрыт)
  EligibleLiquidation  = min(PoAmount, ClaimableForInvoice)     по PO-строке
  RequiredNewBudget    = max(0, RequestedFor(key) − LiquidationFor(key))
  ProjectedAvailable   = AvailableForInvoice − RequiredNewBudget
  CumulativeExcessPct  = max(0, AlreadyPosted + OtherActiveClaims + PoAmount − Authorized) / Authorized
  ```

- [ ] **Step 1: Создать тестовый проект**

```powershell
dotnet new xunit -n GovErp.Domain.Validation.Tests -o tests/GovErp.Domain.Validation.Tests
Remove-Item tests/GovErp.Domain.Validation.Tests/UnitTest1.cs
dotnet sln add tests/GovErp.Domain.Validation.Tests
dotnet add tests/GovErp.Domain.Validation.Tests reference src/GovErp.Domain.Validation
dotnet add tests/GovErp.Domain.Validation.Tests reference src/GovErp.Domain.Shared
```
Заменить csproj на CPM-вариант (как в плане 1, задача 1, шаг 2).

- [ ] **Step 2: Тесты расчётов снимка**

`tests/GovErp.Domain.Validation.Tests/ValidationSubjectTests.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class ValidationSubjectTests
{
    private static readonly AccountCode Fire = AccountCode.Parse("701-6000-53100-G-COPS-26");
    private static readonly AccountCode Police = AccountCode.Parse("701-3000-53100-G-COPS-26");

    [Fact]
    public void Exercise_requires_full_amount_and_projects_13000_deficit()
    {
        var s = SubjectBuilder.Exercise();
        s.BudgetFor(Fire).Available.Should().Be(Money.Of(147_000m));
        s.RequiredNewBudget(Fire).Should().Be(Money.Of(160_000m));
        s.ProjectedAvailable(Fire).Should().Be(Money.Of(-13_000m));
    }

    [Fact]
    public void Two_lines_on_one_budget_key_are_summed()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, Fire.ToString(), 80_000m))
            .With(SubjectBuilder.Distribution(2, Fire.ToString(), 80_000m))
            .Build();
        s.BudgetKeys.Should().ContainSingle();
        s.RequestedFor(Fire).Should().Be(Money.Of(160_000m));
        s.ProjectedAvailable(Fire).Should().Be(Money.Of(-13_000m));
    }

    [Fact]
    public void Own_held_is_added_back_so_a_submitted_invoice_is_not_charged_twice()
    {
        // После Submit 100,000: Held 100,000 (весь — свой). Перевалидация на Approve не должна увидеть дефицит.
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, Fire.ToString(), 100_000m))
            .Budget(SubjectBuilder.Budget(Fire, 375_000m, 132_000m, 96_000m, held: 100_000m, ownHeld: 100_000m))
            .Build();
        s.BudgetFor(Fire).Available.Should().Be(Money.Of(47_000m));
        s.BudgetFor(Fire).AvailableForInvoice.Should().Be(Money.Of(147_000m));
        s.ProjectedAvailable(Fire).Should().Be(Money.Of(47_000m));
    }

    [Fact]
    public void Po_backed_within_claimable_requires_no_new_budget()
    {
        var s = SubjectBuilder.PoBacked(invoiceAmount: 160_000m);
        s.EligibleLiquidation("PO-2026-0451/1").Should().Be(Money.Of(160_000m));
        s.RequiredNewBudget(Police).Should().Be(Money.Zero);
        s.ProjectedAvailable(Police).Should().Be(Money.Of(240_000m));
        s.CumulativeExcessPct("PO-2026-0451/1").Should().Be(0m);
    }

    [Fact]
    public void Po_backed_over_remaining_requires_the_excess_from_budget()
    {
        var s = SubjectBuilder.PoBacked(invoiceAmount: 164_800m);
        s.EligibleLiquidation("PO-2026-0451/1").Should().Be(Money.Of(160_000m));
        s.RequiredNewBudget(Police).Should().Be(Money.Of(4_800m));
        s.CumulativeExcessPct("PO-2026-0451/1").Should().Be(0.03m);
    }

    [Fact]
    public void Claims_of_other_invoices_reduce_what_this_invoice_may_liquidate()
    {
        var s = SubjectBuilder.PoBacked(invoiceAmount: 96_000m, remaining: 96_000m, otherHeldClaims: 96_000m, otherBillingClaims: 96_000m);
        s.EligibleLiquidation("PO-2026-0451/1").Should().Be(Money.Zero);
        s.RequiredNewBudget(Police).Should().Be(Money.Of(96_000m));
    }

    [Fact]
    public void Tolerance_base_is_authorized_amount_not_remaining()
    {
        // Утверждено 160,000; уже проведено 100,000; этот инвойс 64,000 → накопленно 164,000 → 2.5%.
        var s = SubjectBuilder.PoBacked(invoiceAmount: 64_000m, remaining: 60_000m, alreadyPosted: 100_000m);
        s.CumulativeExcessPct("PO-2026-0451/1").Should().Be(0.025m);
    }

    [Fact]
    public void Missing_budget_line_yields_missing_snapshot()
    {
        var other = AccountCode.Parse("101-3000-54000");
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, other.ToString(), 10m)).Build();
        s.BudgetFor(other).Exists.Should().BeFalse();
    }
}
```

- [ ] **Step 3: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Validation.Tests`
Expected: ошибки компиляции — снимки не определены.

- [ ] **Step 4: Реализация перечислений и исключения**

`namespace GovErp.Domain.Validation.ValueObjects;`, по файлу на тип:
```csharp
public enum Severity { Allowed = 0, Warning = 1, SoftStop = 2, HardStop = 3 }
public enum RuleLayer { Core = 0, Federal = 1, State = 2, Tenant = 3 }   // больше = специфичнее
public enum ValidationStep
{
    RequiredSegments = 1, ValidCombination = 2, FundAndGrantRestrictions = 3, TransactionPurpose = 4,
    BudgetAvailability = 5, EncumbranceImpact = 6, ApprovalRequirements = 7, PostingEligibility = 8
}
public enum EvaluationTrigger { Manual, Submit, Approve, Override, Post }
public enum ApproverRole { DepartmentHead, GrantsManager, BudgetOfficer, FinanceDirector }
public enum FundKind { Governmental, Enterprise }
public enum BudgetControl { Hard, Soft }
public enum GrantRule { Required, Forbidden, Optional }
public enum FundRestriction { Allowed, DepartmentNotAllowed, ObjectNotAllowed }
public enum GrantEligibilityResult { Eligible, GrantNotActive, OutsidePeriod, DepartmentNotAllowed, ObjectNotAllowed }
```

`Exceptions/ValidationException.cs`:
```csharp
namespace GovErp.Domain.Validation.Exceptions;

public sealed class ValidationException(string message) : Exception(message);
```

- [ ] **Step 5: Реализация снимков**

`namespace GovErp.Domain.Validation.ValueObjects;`, по файлу на тип:
```csharp
// VendorSnapshot.cs
public sealed record VendorSnapshot(Guid VendorId, string Name, bool IsActive, bool IsDebarred, bool SamRegistered);

// TransactionSnapshot.cs
/// <summary>Снимок документа. IsDuplicate вычисляет слой сценариев (запрос к Payables). ContentVersion — не SQL RowVersion.</summary>
public sealed record TransactionSnapshot(
    string TransactionRef, Guid InvoiceId, int ContentVersion, Guid ApprovalCycleId, string TransactionType,
    DateOnly InvoiceDate, DateOnly ServiceDate, DateOnly PostingDate, Money Total,
    VendorSnapshot Vendor, string? PoNumber, bool IsDuplicate, UserId CreatedBy)
{
    public bool IsPoBacked => PoNumber is not null;
}

// CombinationSnapshot.cs
public sealed record CombinationSnapshot(bool Exists, bool IsActiveOnDate, string Status);

// FundSnapshot.cs
public sealed record FundSnapshot(
    string Code, string Name, FundKind Kind, BudgetControl Control, GrantRule GrantRule,
    FundRestriction Restriction, bool IsActive);

// GrantSnapshot.cs
/// <summary>Eligibility вычислена слоем сценариев на ServiceDate.</summary>
public sealed record GrantSnapshot(string Code, bool IsFederal, GrantEligibilityResult Eligibility, string Status);

// BudgetSnapshot.cs
/// <summary>Снимок бюджетной строки по ключу. OwnHeld — резервы этого инвойса и его текущей версии содержания.</summary>
public sealed record BudgetSnapshot(AccountCode Account, bool Exists, Money Amended, Money Actuals, Money Encumbered, Money Held, Money OwnHeld)
{
    public Money Available => Amended - Actuals - Encumbered - Held;
    public Money AvailableForInvoice => Available + OwnHeld;

    public static BudgetSnapshot Missing(AccountCode account) =>
        new(account, false, Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero);
}

// EncumbranceSnapshot.cs
public sealed record EncumbranceSnapshot(Money Remaining, Money OtherHeldClaims, bool IsOpen)
{
    public Money ClaimableForInvoice => IsOpen ? Money.Max(Money.Zero, Remaining - OtherHeldClaims) : Money.Zero;
}

// PoLineSnapshot.cs
public sealed record PoLineSnapshot(string PoLineRef, AccountCode Account, bool IsOpen, Money AuthorizedAmount,
    Money AlreadyPosted, Money OtherActiveClaims, EncumbranceSnapshot? Encumbrance);

// DistributionSnapshot.cs
public sealed record DistributionSnapshot(
    int LineNo, AccountCode Account, Money Amount, string? PoLineRef,
    CombinationSnapshot Combination, FundSnapshot? Fund, GrantSnapshot? Grant);

// ApprovalSnapshot.cs
/// <summary>Согласование активного цикла. Department — только у DepartmentHead.</summary>
public sealed record ApprovalSnapshot(ApproverRole Role, string? Department);

// OverrideSnapshot.cs
/// <summary>Override активного цикла и текущей версии содержания (фильтрует слой сценариев).</summary>
public sealed record OverrideSnapshot(Guid EvaluationId, string RuleId, int RuleVersion, int? DistributionLine, UserId UserId, string Reason);

// ApprovalBaseline.cs
/// <summary>На чём основано последнее согласование активного цикла: версия содержания и fingerprint правил.</summary>
public sealed record ApprovalBaseline(int ContentVersion, string RuleSetFingerprint);

// PostingAccounts.cs
/// <summary>Object-коды и «балансовый» департамент для строк AP / резерва / encumbrance в posting preview.</summary>
public sealed record PostingAccounts(ObjectCode AccountsPayable, ObjectCode ReserveForEncumbrances,
    ObjectCode Encumbrances, DepartmentCode BalanceSheetDepartment);
```

`ValidationSubject.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Все входы конвейера. Вычисления — чистые функции снимков (spec §4.1).</summary>
public sealed record ValidationSubject(
    TransactionSnapshot Transaction,
    IReadOnlyList<DistributionSnapshot> Distributions,
    IReadOnlyList<BudgetSnapshot> Budgets,
    IReadOnlyList<PoLineSnapshot> PoLines,
    IReadOnlyList<ApprovalSnapshot> ActiveApprovals,
    IReadOnlyList<OverrideSnapshot> ActiveOverrides,
    bool PeriodIsOpen,
    ApprovalBaseline? ApprovalBaseline,
    PostingAccounts PostingAccounts)
{
    public IReadOnlyList<AccountCode> BudgetKeys => Distributions.Select(d => d.Account).Distinct().ToList();

    public BudgetSnapshot BudgetFor(AccountCode key) =>
        Budgets.SingleOrDefault(b => b.Account == key) ?? BudgetSnapshot.Missing(key);

    public PoLineSnapshot? PoLine(string poLineRef) => PoLines.SingleOrDefault(p => p.PoLineRef == poLineRef);

    public int FirstLineOf(AccountCode key) => Distributions.Where(d => d.Account == key).Min(d => d.LineNo);

    public Money RequestedFor(AccountCode key) => Sum(Distributions.Where(d => d.Account == key).Select(d => d.Amount));

    public Money PoAmount(string poLineRef) => Sum(Distributions.Where(d => d.PoLineRef == poLineRef).Select(d => d.Amount));

    public Money EligibleLiquidation(string poLineRef)
    {
        var line = PoLine(poLineRef);
        return line?.Encumbrance is null ? Money.Zero : Money.Min(PoAmount(poLineRef), line.Encumbrance.ClaimableForInvoice);
    }

    public Money LiquidationFor(AccountCode key) =>
        Sum(PoLines.Where(p => p.Account == key).Select(p => EligibleLiquidation(p.PoLineRef)));

    public Money RequiredNewBudget(AccountCode key) => Money.Max(Money.Zero, RequestedFor(key) - LiquidationFor(key));

    public Money ProjectedAvailable(AccountCode key) => BudgetFor(key).AvailableForInvoice - RequiredNewBudget(key);

    /// <summary>Накопленное превышение над утверждённой суммой PO-строки, доля (0.03 = 3%). Нулевая утверждённая сумма — 1.</summary>
    public decimal CumulativeExcessPct(string poLineRef)
    {
        var line = PoLine(poLineRef);
        if (line is null || line.AuthorizedAmount.IsZero)
        {
            return 1m;
        }

        var projected = line.AlreadyPosted + line.OtherActiveClaims + PoAmount(poLineRef);
        var excess = Money.Max(Money.Zero, projected - line.AuthorizedAmount);
        return excess.Amount / line.AuthorizedAmount.Amount;
    }

    private static Money Sum(IEnumerable<Money> values) => values.Aggregate(Money.Zero, (s, m) => s + m);
}
```

- [ ] **Step 6: Тестовый SubjectBuilder**

`tests/GovErp.Domain.Validation.Tests/Support/SubjectBuilder.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

/// <summary>Собирает ValidationSubject для тестов. По умолчанию — сценарий задания: non-PO $160,000 на 701-6000-53100-G-COPS-26.</summary>
public sealed class SubjectBuilder
{
    public static readonly UserId Clerk = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly UserId Director = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    public static readonly Guid InvoiceId = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    public static readonly Guid CycleId = Guid.Parse("cccccccc-0000-0000-0000-000000000001");
    public static readonly DateOnly Jun15 = new(2026, 6, 15);

    public static readonly PostingAccounts Accounts = new(
        new ObjectCode("2100"), new ObjectCode("2900"), new ObjectCode("5900"), new DepartmentCode("0000"));

    public static FundSnapshot Grants701(FundRestriction restriction = FundRestriction.Allowed) =>
        new("701", "Grants Fund", FundKind.Governmental, BudgetControl.Hard, GrantRule.Required, restriction, true);

    public static FundSnapshot General101(FundRestriction restriction = FundRestriction.Allowed) =>
        new("101", "General Fund", FundKind.Governmental, BudgetControl.Soft, GrantRule.Forbidden, restriction, true);

    public static FundSnapshot Street202(FundRestriction restriction = FundRestriction.Allowed) =>
        new("202", "Street Fund", FundKind.Governmental, BudgetControl.Hard, GrantRule.Forbidden, restriction, true);

    public static FundSnapshot Water501() =>
        new("501", "Water Enterprise Fund", FundKind.Enterprise, BudgetControl.Soft, GrantRule.Forbidden, FundRestriction.Allowed, true);

    public static GrantSnapshot Cops(GrantEligibilityResult e = GrantEligibilityResult.Eligible) =>
        new("G-COPS-26", true, e, "Active");

    public static BudgetSnapshot Budget(AccountCode account, decimal amended, decimal actuals, decimal encumbered, decimal held = 0, decimal ownHeld = 0) =>
        new(account, true, Money.Of(amended), Money.Of(actuals), Money.Of(encumbered), Money.Of(held), Money.Of(ownHeld));

    public static BudgetSnapshot Budget(string account, decimal amended, decimal actuals, decimal encumbered, decimal held = 0, decimal ownHeld = 0) =>
        Budget(AccountCode.Parse(account), amended, actuals, encumbered, held, ownHeld);

    /// <summary>Бюджеты seed по умолчанию (spec §2.3); ключи без бюджета — Missing.</summary>
    public static IReadOnlyList<BudgetSnapshot> DefaultBudgets() =>
    [
        Budget("701-6000-53100-G-COPS-26", 375_000m, 132_000m, 96_000m),
        Budget("701-3000-53100-G-COPS-26", 500_000m, 100_000m, 160_000m),
        Budget("101-6000-53100", 50_000m, 40_000m, 0m),
        Budget("202-4000-53100", 25_000m, 5_000m, 0m),
        Budget("501-5000-53100", 60_000m, 30_000m, 0m),
    ];

    public static DistributionSnapshot Distribution(int lineNo, string account, decimal amount,
        FundSnapshot? fund = null, GrantSnapshot? grant = null, CombinationSnapshot? combination = null, string? poLineRef = null)
    {
        var code = AccountCode.Parse(account);
        fund ??= code.Fund.Value switch { "701" => Grants701(), "101" => General101(), "202" => Street202(), "501" => Water501(), _ => null };
        grant ??= code.Grant is null ? null : Cops();
        return new DistributionSnapshot(lineNo, code, Money.Of(amount), poLineRef,
            combination ?? new CombinationSnapshot(true, true, "Active"), fund, grant);
    }

    private readonly List<DistributionSnapshot> _distributions = [];
    private readonly Dictionary<AccountCode, BudgetSnapshot> _budgets = DefaultBudgets().ToDictionary(b => b.Account);
    private readonly List<PoLineSnapshot> _poLines = [];
    private readonly List<ApprovalSnapshot> _approvals = [];
    private readonly List<OverrideSnapshot> _overrides = [];
    private Money? _total;
    private string? _poNumber;
    private bool _isDuplicate;
    private bool _periodOpen = true;
    private VendorSnapshot _vendor = new(Guid.NewGuid(), "Acme Consulting", IsActive: true, IsDebarred: false, SamRegistered: true);
    private ApprovalBaseline? _baseline;
    private int _contentVersion = 1;

    public SubjectBuilder With(DistributionSnapshot d) { _distributions.Add(d); return this; }
    public SubjectBuilder Budget(BudgetSnapshot b) { _budgets[b.Account] = b; return this; }
    public SubjectBuilder NoBudget(string account) { _budgets.Remove(AccountCode.Parse(account)); return this; }
    public SubjectBuilder Po(string poNumber, PoLineSnapshot line) { _poNumber = poNumber; _poLines.Add(line); return this; }
    public SubjectBuilder Total(decimal total) { _total = Money.Of(total); return this; }
    public SubjectBuilder Duplicate() { _isDuplicate = true; return this; }
    public SubjectBuilder PeriodClosed() { _periodOpen = false; return this; }
    public SubjectBuilder Vendor(bool active = true, bool debarred = false, bool sam = true) { _vendor = _vendor with { IsActive = active, IsDebarred = debarred, SamRegistered = sam }; return this; }
    public SubjectBuilder Approved(ApproverRole role, string? department = null) { _approvals.Add(new ApprovalSnapshot(role, department)); return this; }
    public SubjectBuilder Overridden(string ruleId, int? line = null, int ruleVersion = 1, string reason = "justified") { _overrides.Add(new OverrideSnapshot(Guid.NewGuid(), ruleId, ruleVersion, line, Director, reason)); return this; }
    public SubjectBuilder Baseline(string fingerprint, int contentVersion = 1) { _baseline = new ApprovalBaseline(contentVersion, fingerprint); return this; }
    public SubjectBuilder ContentVersion(int v) { _contentVersion = v; return this; }

    public ValidationSubject Build()
    {
        if (_distributions.Count == 0)
        {
            _distributions.Add(Distribution(1, "701-6000-53100-G-COPS-26", 160_000m));
        }

        var total = _total ?? _distributions.Aggregate(Money.Zero, (s, d) => s + d.Amount);
        var tx = new TransactionSnapshot("INV-V-7781", InvoiceId, _contentVersion, CycleId, "AP_INVOICE",
            Jun15, Jun15, Jun15, total, _vendor, _poNumber, _isDuplicate, Clerk);
        var keys = _distributions.Select(d => d.Account).Distinct();
        var budgets = keys.Where(_budgets.ContainsKey).Select(k => _budgets[k]).ToList();
        return new ValidationSubject(tx, _distributions, budgets, _poLines, _approvals, _overrides, _periodOpen, _baseline, Accounts);
    }

    /// <summary>Сценарий задания целиком.</summary>
    public static ValidationSubject Exercise() => new SubjectBuilder().Build();

    /// <summary>PO-backed сценарий на Police-комбинации: утверждено 160,000 (spec §2.3).</summary>
    public static ValidationSubject PoBacked(decimal invoiceAmount, decimal remaining = 160_000m, decimal otherHeldClaims = 0m,
        decimal alreadyPosted = 0m, decimal otherBillingClaims = 0m, bool poOpen = true, decimal authorized = 160_000m) =>
        PoBackedBuilder(invoiceAmount, remaining, otherHeldClaims, alreadyPosted, otherBillingClaims, poOpen, authorized).Build();

    public static SubjectBuilder PoBackedBuilder(decimal invoiceAmount, decimal remaining = 160_000m, decimal otherHeldClaims = 0m,
        decimal alreadyPosted = 0m, decimal otherBillingClaims = 0m, bool poOpen = true, decimal authorized = 160_000m)
    {
        const string police = "701-3000-53100-G-COPS-26";
        var encumbrance = new EncumbranceSnapshot(Money.Of(remaining), Money.Of(otherHeldClaims), IsOpen: remaining > 0);
        return new SubjectBuilder()
            .Po("PO-2026-0451", new PoLineSnapshot("PO-2026-0451/1", AccountCode.Parse(police), poOpen, Money.Of(authorized),
                Money.Of(alreadyPosted), Money.Of(otherBillingClaims), encumbrance))
            .With(Distribution(1, police, invoiceAmount, poLineRef: "PO-2026-0451/1"));
    }
}
```

- [ ] **Step 7: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Validation.Tests`
Expected: 8 passed.

- [ ] **Step 8: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests GovErp.sln
git commit -m "Validation: snapshots with per-budget-key and per-PO-line calculations

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 2: RuleDefinition, разрешение слоёв и fingerprint

**Files:**
- Create: `Entities/RuleDefinition.cs`, `ValueObjects/AppliedRule.cs`, `ValueObjects/EffectiveRuleSet.cs`, `DomainServices/RuleResolution.cs`, `Repositories/IRuleDefinitionRepository.cs`
- Test: `tests/.../RuleResolutionTests.cs`, `tests/.../Support/DemoRules.cs`

**Interfaces:**
- Produces: `RuleDefinition` (`RuleId`, `Version`, `Step`, `Layer`, `IsLocallyAdjustable`, `Severity?` — `null` значит «правило решает само», `Parameters`, `OverridableBy`, `EffectiveFrom/To`, `Message`, `Resolution`, `IsEnabled`, `IsEffectiveOn(DateOnly)`, `Parameter(string)`, `DecimalParameter(string)`, `CanonicalParameters`); `AppliedRule(RuleId, Layer, Version, ParametersHash)`; `EffectiveRuleSet` (`Definitions`, `AppliedRules`, `Fingerprint`, `EngineVersion`, `ForStep(ValidationStep)`, `Find(string ruleId)` — самое специфичное определение); `RuleResolution.Resolve(IReadOnlyList<RuleDefinition>, DateOnly) → EffectiveRuleSet`; `RuleResolution.EngineVersion = "engine-1.0.0"`.
- Правила spec §4.1 п. 3 и `GE-11`, `GE-15`:
  - `IsLocallyAdjustable = false` → в набор попадает старшая версия **каждого** слоя; конвейер выполняет все, итог — строжайший. Локальный слой обязательное ограничение не отменяет.
  - `IsLocallyAdjustable = true` → в набор попадает одно определение: самый специфичный слой, в нём — старшая версия. Ослабление проверяет `RuleSetGuard` (задача 7), а не резолвер.
  - `Fingerprint` — SHA-256 (hex, нижний регистр) по строкам `RuleId|Layer|Version|k=v;k=v` всех попавших в набор определений, отсортированным по `RuleId`, `Layer`, плюс строка `engine=<EngineVersion>`. Порядок входного списка на fingerprint не влияет.

- [ ] **Step 1: Тесты**

`tests/.../Support/DemoRules.cs` — набор определений, совпадающий с seed плана 3:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public static class DemoRules
{
    private static readonly DateOnly From = new(2025, 7, 1);

    public static RuleDefinition Rule(string id, ValidationStep step, RuleLayer layer, Severity? severity,
        Dictionary<string, string>? parameters = null, ApproverRole[]? overridableBy = null, int version = 1,
        DateOnly? from = null, DateOnly? to = null, bool enabled = true, bool adjustable = false) =>
        new(id, version, step, layer, adjustable, severity, parameters ?? [], overridableBy ?? [], from ?? From, to,
            message: $"{id} fired", resolution: $"Resolve {id}", enabled);

    public static IReadOnlyList<RuleDefinition> All() =>
    [
        Rule("SEG_REQUIRED", ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop),
        Rule("SEG_GRANT_FORBIDDEN", ValidationStep.RequiredSegments, RuleLayer.Core, Severity.HardStop),
        Rule("COA_COMBINATION_ACTIVE", ValidationStep.ValidCombination, RuleLayer.Core, Severity.HardStop),
        Rule("FUND_DEPT_OBJECT_ALLOWED", ValidationStep.FundAndGrantRestrictions, RuleLayer.Tenant, Severity.HardStop),
        Rule("GRANT_ELIGIBLE", ValidationStep.FundAndGrantRestrictions, RuleLayer.Federal, Severity.HardStop),
        Rule("VENDOR_ELIGIBLE", ValidationStep.TransactionPurpose, RuleLayer.Federal, Severity.HardStop),
        Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.State, Severity.SoftStop,
            new() { ["threshold"] = "25000" }, [ApproverRole.FinanceDirector], adjustable: true),
        Rule("INVOICE_DUPLICATE", ValidationStep.TransactionPurpose, RuleLayer.Core, Severity.HardStop),
        Rule("BUDGET_AVAILABILITY", ValidationStep.BudgetAvailability, RuleLayer.Core, null,
            overridableBy: [ApproverRole.BudgetOfficer, ApproverRole.FinanceDirector]),
        Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
            new() { ["pct"] = "0.10" }, adjustable: true),
        Rule("PO_LIQUIDATION", ValidationStep.EncumbranceImpact, RuleLayer.Core, null,
            new() { ["tolerance_pct"] = "0.05" }, adjustable: true),
        Rule("APPROVAL_ROUTE", ValidationStep.ApprovalRequirements, RuleLayer.Tenant, null,
            new() { ["finance_director_threshold"] = "50000" }, adjustable: true),
    ];

    public static EffectiveRuleSet Resolved(IReadOnlyList<RuleDefinition>? rules = null) =>
        DomainServices.RuleResolution.Resolve(rules ?? All(), new DateOnly(2026, 6, 15));
}
```

`RuleResolutionTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class RuleResolutionTests
{
    private static readonly DateOnly Jun15 = new(2026, 6, 15);

    [Fact]
    public void Resolves_all_effective_enabled_rules()
    {
        var set = RuleResolution.Resolve(DemoRules.All(), Jun15);
        set.Definitions.Should().HaveCount(12);
        set.AppliedRules.Should().HaveCount(12);
        set.ForStep(ValidationStep.TransactionPurpose).Select(r => r.RuleId)
            .Should().BeEquivalentTo("VENDOR_ELIGIBLE", "PROCUREMENT_THRESHOLD", "INVOICE_DUPLICATE");
        set.EngineVersion.Should().Be(RuleResolution.EngineVersion);
    }

    [Fact]
    public void Excludes_disabled_and_out_of_effective_window()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("FUTURE", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, from: new DateOnly(2027, 1, 1)),
            DemoRules.Rule("EXPIRED", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, to: new DateOnly(2026, 5, 31)),
            DemoRules.Rule("OFF", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, enabled: false),
        };
        var set = RuleResolution.Resolve(rules, Jun15);
        set.Find("FUTURE").Should().BeNull();
        set.Find("EXPIRED").Should().BeNull();
        set.Find("OFF").Should().BeNull();
    }

    [Fact]
    public void Resolution_AdjustableRule_TenantLayerReplacesState()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.SoftStop,
                new() { ["threshold"] = "10000" }, [ApproverRole.FinanceDirector], adjustable: true),
        };
        var set = RuleResolution.Resolve(rules, Jun15);
        set.Definitions.Where(d => d.RuleId == "PROCUREMENT_THRESHOLD").Should().ContainSingle()
            .Which.Layer.Should().Be(RuleLayer.Tenant);
        set.Find("PROCUREMENT_THRESHOLD")!.DecimalParameter("threshold").Should().Be(10_000m);
    }

    [Fact]
    public void Resolution_NonAdjustableRule_AllLayersEvaluated()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("VENDOR_ELIGIBLE", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning),
        };
        var set = RuleResolution.Resolve(rules, Jun15);
        set.Definitions.Where(d => d.RuleId == "VENDOR_ELIGIBLE").Select(d => d.Layer)
            .Should().BeEquivalentTo([RuleLayer.Federal, RuleLayer.Tenant]);
    }

    [Fact]
    public void Same_layer_highest_version_wins()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
                new() { ["pct"] = "0.20" }, version: 2, adjustable: true),
        };
        RuleResolution.Resolve(rules, Jun15).Find("BUDGET_LOW_REMAINING")!.DecimalParameter("pct").Should().Be(0.20m);
    }

    [Fact]
    public void Fingerprint_ChangesWithParameters_NotWithOrder()
    {
        var baseline = RuleResolution.Resolve(DemoRules.All(), Jun15).Fingerprint;
        RuleResolution.Resolve(DemoRules.All().Reverse().ToList(), Jun15).Fingerprint.Should().Be(baseline);
        baseline.Should().MatchRegex("^[0-9a-f]{64}$");

        var changed = DemoRules.All().Select(r => r.RuleId == "BUDGET_LOW_REMAINING"
            ? DemoRules.Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
                new() { ["pct"] = "0.15" }, adjustable: true)
            : r).ToList();
        RuleResolution.Resolve(changed, Jun15).Fingerprint.Should().NotBe(baseline);
    }

    [Fact]
    public void Fingerprint_includes_rules_that_did_not_fire_and_changes_when_a_rule_is_added()
    {
        var more = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("NEW_TENANT_WARNING", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, adjustable: true),
        };
        RuleResolution.Resolve(more, Jun15).Fingerprint.Should().NotBe(RuleResolution.Resolve(DemoRules.All(), Jun15).Fingerprint);
    }

    [Fact]
    public void Missing_parameter_throws() =>
        FluentActions.Invoking(() => DemoRules.Rule("X", ValidationStep.TransactionPurpose, RuleLayer.Core, Severity.Warning).DecimalParameter("nope"))
            .Should().Throw<Exceptions.ValidationException>();
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Validation.Tests`

- [ ] **Step 3: Реализация**

`Entities/RuleDefinition.cs`:
```csharp
using System.Globalization;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

/// <summary>Запись о правиле: включённость, версия, слой, адаптируемость, severity, параметры, effective dating. Логика — в коде (GE-10).</summary>
public sealed class RuleDefinition
{
    public Guid Id { get; private set; }
    public string RuleId { get; private set; }
    public int Version { get; private set; }
    public ValidationStep Step { get; private set; }
    public RuleLayer Layer { get; private set; }
    /// <summary>false — обязательное ограничение: более специфичный слой его не заменяет (GE-11).</summary>
    public bool IsLocallyAdjustable { get; private set; }
    /// <summary>null — правило само определяет severity (например, по режиму контроля фонда).</summary>
    public Severity? Severity { get; private set; }
    public IReadOnlyDictionary<string, string> Parameters { get; private set; }
    public IReadOnlyList<ApproverRole> OverridableBy { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string Message { get; private set; }
    public string Resolution { get; private set; }
    public bool IsEnabled { get; private set; }

    public RuleDefinition(string ruleId, int version, ValidationStep step, RuleLayer layer, bool isLocallyAdjustable, Severity? severity,
        IReadOnlyDictionary<string, string> parameters, IReadOnlyList<ApproverRole> overridableBy,
        DateOnly effectiveFrom, DateOnly? effectiveTo, string message, string resolution, bool enabled = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        if (version < 1)
        {
            throw new ValidationException($"Rule {ruleId}: version must be ≥ 1.");
        }

        if (effectiveTo is { } to && to < effectiveFrom)
        {
            throw new ValidationException($"Rule {ruleId}: EffectiveTo before EffectiveFrom.");
        }

        Id = Guid.NewGuid();
        RuleId = ruleId;
        Version = version;
        Step = step;
        Layer = layer;
        IsLocallyAdjustable = isLocallyAdjustable;
        Severity = severity;
        Parameters = new Dictionary<string, string>(parameters);
        OverridableBy = overridableBy.ToList();
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Message = message;
        Resolution = resolution;
        IsEnabled = enabled;
    }

    private RuleDefinition()
    {
        RuleId = null!;
        Parameters = new Dictionary<string, string>();
        OverridableBy = [];
        Message = null!;
        Resolution = null!;
    }

    public bool IsEffectiveOn(DateOnly date) =>
        IsEnabled && date >= EffectiveFrom && (EffectiveTo is null || date <= EffectiveTo);

    public string Parameter(string name) =>
        Parameters.TryGetValue(name, out var value)
            ? value
            : throw new ValidationException($"Rule {RuleId} v{Version} has no parameter '{name}'.");

    public decimal DecimalParameter(string name) =>
        decimal.Parse(Parameter(name), NumberStyles.Number, CultureInfo.InvariantCulture);

    /// <summary>Параметры в каноническом виде для fingerprint: ключи по возрастанию, «k=v;k=v».</summary>
    public string CanonicalParameters =>
        string.Join(';', Parameters.OrderBy(p => p.Key, StringComparer.Ordinal).Select(p => $"{p.Key}={p.Value}"));
}
```

`ValueObjects/AppliedRule.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Одно применённое определение правила — для аудита «какие правила и в каких версиях действовали».</summary>
public sealed record AppliedRule(string RuleId, RuleLayer Layer, int Version, string ParametersHash);
```

`ValueObjects/EffectiveRuleSet.cs`:
```csharp
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Набор правил, действующих на дату, после разрешения слоёв; с fingerprint (GE-15).</summary>
public sealed record EffectiveRuleSet(IReadOnlyList<RuleDefinition> Definitions, IReadOnlyList<AppliedRule> AppliedRules,
    string Fingerprint, string EngineVersion)
{
    public IReadOnlyList<RuleDefinition> ForStep(ValidationStep step) =>
        Definitions.Where(d => d.Step == step).OrderBy(d => d.RuleId, StringComparer.Ordinal).ThenBy(d => d.Layer).ToList();

    /// <summary>Самое специфичное определение правила (для параметров маршрута и т.п.).</summary>
    public RuleDefinition? Find(string ruleId) =>
        Definitions.Where(d => d.RuleId == ruleId).OrderByDescending(d => d.Layer).FirstOrDefault();
}
```

`DomainServices/RuleResolution.cs`:
```csharp
using System.Security.Cryptography;
using System.Text;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Отбор действующих определений и разрешение слоёв (GE-11), fingerprint набора (GE-15).</summary>
public static class RuleResolution
{
    public const string EngineVersion = "engine-1.0.0";

    public static EffectiveRuleSet Resolve(IReadOnlyList<RuleDefinition> candidates, DateOnly onDate)
    {
        var selected = new List<RuleDefinition>();
        foreach (var group in candidates.Where(r => r.IsEffectiveOn(onDate)).GroupBy(r => r.RuleId))
        {
            var latestPerLayer = group.GroupBy(r => r.Layer)
                .Select(l => l.OrderByDescending(r => r.Version).First())
                .ToList();

            // Адаптируемость определяется базовым (наименее специфичным) слоем правила.
            var baseDefinition = latestPerLayer.OrderBy(r => r.Layer).First();
            if (baseDefinition.IsLocallyAdjustable)
            {
                selected.Add(latestPerLayer.OrderByDescending(r => r.Layer).First());
            }
            else
            {
                selected.AddRange(latestPerLayer);
            }
        }

        var ordered = selected.OrderBy(r => r.Step).ThenBy(r => r.RuleId, StringComparer.Ordinal).ThenBy(r => r.Layer).ToList();
        var applied = ordered.Select(r => new AppliedRule(r.RuleId, r.Layer, r.Version, Hash(r.CanonicalParameters))).ToList();
        return new EffectiveRuleSet(ordered, applied, Fingerprint(ordered), EngineVersion);
    }

    private static string Fingerprint(IEnumerable<RuleDefinition> rules)
    {
        var lines = rules
            .OrderBy(r => r.RuleId, StringComparer.Ordinal).ThenBy(r => r.Layer)
            .Select(r => $"{r.RuleId}|{r.Layer}|{r.Version}|{r.CanonicalParameters}")
            .Append($"engine={EngineVersion}");
        return Hash(string.Join('\n', lines));
    }

    private static string Hash(string text) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
}
```

`Repositories/IRuleDefinitionRepository.cs`:
```csharp
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.Repositories;

public interface IRuleDefinitionRepository
{
    /// <summary>Все версии всех слоёв — разрешение делает RuleResolution.</summary>
    Task<IReadOnlyList<RuleDefinition>> ListAsync(CancellationToken ct = default);
    Task AddAsync(RuleDefinition definition, CancellationToken ct = default);
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Validation.Tests`
Expected: 16 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: rule definitions, layer resolution with mandatory rules, rule-set fingerprint

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 3: RuleOutcome, агрегация с привязанными overrides, Capabilities

**Files:**
- Create: `ValueObjects/RuleOutcome.cs`, `ValueObjects/Capabilities.cs`, `DomainServices/OutcomeAggregation.cs`, `DomainServices/IValidationRule.cs`
- Test: `OutcomeAggregationTests.cs`, `CapabilitiesTests.cs`

**Interfaces:**
- Produces: `RuleOutcome` (record: `RuleId`, `RuleVersion`, `Step`, `Layer`, `DistributionLine`, `BudgetKey`, `Severity`, `Inputs`, `Computed`, `Message`, `Resolution`, `OverridableBy`, `OverriddenBy`, `IsOverridden`) + `RuleOutcome.From(RuleDefinition, Severity, int? line, inputs, computed, message?, budgetKey?)`; `OutcomeAggregation.Apply(outcomes, overrides) → (IReadOnlyList<RuleOutcome> WithOverrides, Severity Overall)`; `Capabilities(CanSave, CanSubmit, CanApprove, CanPost)` + `Capabilities.For(Severity overall, bool? postingPassed)`; `IValidationRule { string RuleId; IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject, RuleDefinition); }`.
- Override снимает outcome, только если совпадают `RuleId`, `RuleVersion` и `DistributionLine` (spec §4.1), outcome — `SoftStop`, и правило вообще разрешает override (`OverridableBy` не пуст). Версия содержания и цикл отфильтрованы раньше: в снимок попадают только активные overrides (план 3). Готовность к оплате — не capability конвейера (spec §4.3).

- [ ] **Step 1: Тесты**

`OutcomeAggregationTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class OutcomeAggregationTests
{
    private static RuleOutcome Outcome(string id, Severity s, int? line = null, int version = 1, params ApproverRole[] overridable) =>
        RuleOutcome.From(DemoRules.Rule(id, ValidationStep.TransactionPurpose, RuleLayer.Core, s, overridableBy: overridable, version: version),
            s, line, new Dictionary<string, string>(), new Dictionary<string, string>());

    private static OverrideSnapshot Ov(string ruleId, int? line = null, int version = 1) =>
        new(Guid.NewGuid(), ruleId, version, line, SubjectBuilder.Director, "ok");

    [Fact]
    public void Overall_is_strictest() =>
        OutcomeAggregation.Apply([Outcome("A", Severity.Warning), Outcome("B", Severity.SoftStop), Outcome("C", Severity.Allowed)], [])
            .Overall.Should().Be(Severity.SoftStop);

    [Fact]
    public void No_outcomes_means_allowed() =>
        OutcomeAggregation.Apply([], []).Overall.Should().Be(Severity.Allowed);

    [Fact]
    public void Override_removes_soft_stop_from_overall_but_keeps_outcome()
    {
        var (with, overall) = OutcomeAggregation.Apply(
            [Outcome("A", Severity.Warning), Outcome("B", Severity.SoftStop, overridable: ApproverRole.FinanceDirector)], [Ov("B")]);
        overall.Should().Be(Severity.Warning);
        with.Single(o => o.RuleId == "B").Severity.Should().Be(Severity.SoftStop);
        with.Single(o => o.RuleId == "B").IsOverridden.Should().BeTrue();
    }

    [Fact]
    public void Override_OtherLineOrRuleVersion_DoesNotApply()
    {
        var soft = Outcome("BUDGET_AVAILABILITY", Severity.SoftStop, line: 1, version: 2, overridable: ApproverRole.BudgetOfficer);
        OutcomeAggregation.Apply([soft], [Ov("BUDGET_AVAILABILITY", line: 2, version: 2)]).Overall.Should().Be(Severity.SoftStop);
        OutcomeAggregation.Apply([soft], [Ov("BUDGET_AVAILABILITY", line: 1, version: 1)]).Overall.Should().Be(Severity.SoftStop);
        OutcomeAggregation.Apply([soft], [Ov("BUDGET_AVAILABILITY", line: 1, version: 2)]).Overall.Should().Be(Severity.Allowed);
    }

    [Fact]
    public void Override_does_not_apply_to_hard_stop_or_non_overridable()
    {
        var (with, overall) = OutcomeAggregation.Apply(
            [Outcome("H", Severity.HardStop, overridable: ApproverRole.FinanceDirector), Outcome("S", Severity.SoftStop)],
            [Ov("H"), Ov("S")]);
        overall.Should().Be(Severity.HardStop);
        with.Should().OnlyContain(o => !o.IsOverridden);
    }
}
```

`CapabilitiesTests.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class CapabilitiesTests
{
    [Fact]
    public void HardStop_can_only_save() =>
        Capabilities.For(Severity.HardStop, null).Should().Be(new Capabilities(true, false, false, false));

    [Fact]
    public void SoftStop_can_save_and_submit_only() =>
        Capabilities.For(Severity.SoftStop, null).Should().Be(new Capabilities(true, true, false, false));

    [Fact]
    public void Warning_and_allowed_post_only_when_posting_check_passed()
    {
        Capabilities.For(Severity.Warning, null).Should().Be(new Capabilities(true, true, true, false));
        Capabilities.For(Severity.Allowed, postingPassed: false).CanPost.Should().BeFalse();
        Capabilities.For(Severity.Allowed, postingPassed: true).CanPost.Should().BeTrue();
    }
}
```

`Capabilities_Warning…` намеренно отличается от ранней версии: `CanPost` без выполненной проверки шага 8 — `false`. Возможность провести показывается только по результату оценки с `trigger = Post` или по отдельному preview-вызову проверки (план 3).

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

`ValueObjects/RuleOutcome.cs`:
```csharp
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.ValueObjects;

public sealed record RuleOutcome(
    string RuleId, int RuleVersion, ValidationStep Step, RuleLayer Layer, int? DistributionLine, string? BudgetKey,
    Severity Severity,
    IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyDictionary<string, string> Computed,
    string Message, string Resolution,
    IReadOnlyList<ApproverRole> OverridableBy,
    OverrideSnapshot? OverriddenBy)
{
    public static RuleOutcome From(RuleDefinition rule, Severity severity, int? line,
        IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string> computed,
        string? message = null, string? budgetKey = null) =>
        new(rule.RuleId, rule.Version, rule.Step, rule.Layer, line, budgetKey, severity, inputs, computed,
            message ?? rule.Message, rule.Resolution, rule.OverridableBy, null);

    public bool IsOverridden => OverriddenBy is not null;
}
```

`ValueObjects/Capabilities.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Что можно сделать с документом по результату оценки. Готовность к оплате вычисляет инвойс (spec §4.3).</summary>
public sealed record Capabilities(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost)
{
    public static Capabilities For(Severity overall, bool? postingPassed) => overall switch
    {
        Severity.HardStop => new(true, false, false, false),
        Severity.SoftStop => new(true, true, false, false),
        _ => new(true, true, true, postingPassed == true),
    };
}
```

`DomainServices/IValidationRule.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Одно типизированное правило. Без состояния. Параметры — из RuleDefinition.</summary>
public interface IValidationRule
{
    string RuleId { get; }
    IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition);

    /// <summary>Ослабляет ли candidate базовое определение (проверка при сохранении/seed, RuleSetGuard). По умолчанию — сравнение severity.</summary>
    bool IsWeakening(RuleDefinition baseline, RuleDefinition candidate) =>
        (candidate.Severity ?? Severity.HardStop) < (baseline.Severity ?? Severity.HardStop);
}
```

`DomainServices/OutcomeAggregation.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class OutcomeAggregation
{
    /// <summary>Проставляет OverriddenBy на overridable Soft Stop'ы с совпадающими RuleId, RuleVersion и строкой; Overall — без них.</summary>
    public static (IReadOnlyList<RuleOutcome> WithOverrides, Severity Overall) Apply(
        IReadOnlyList<RuleOutcome> outcomes, IReadOnlyList<OverrideSnapshot> overrides)
    {
        var with = outcomes.Select(o =>
        {
            if (o.Severity != Severity.SoftStop || o.OverridableBy.Count == 0)
            {
                return o;
            }

            var match = overrides.FirstOrDefault(x =>
                x.RuleId == o.RuleId && x.RuleVersion == o.RuleVersion && x.DistributionLine == o.DistributionLine);
            return match is null ? o : o with { OverriddenBy = match };
        }).ToList();

        var overall = with.Where(o => !o.IsOverridden).Select(o => o.Severity).DefaultIfEmpty(Severity.Allowed).Max();
        return (with, overall);
    }
}
```

- [ ] **Step 4: Прогнать**

Expected: 24 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: rule outcomes, bound overrides, capabilities

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 4: Правила шагов 1–4 (сегменты, комбинация, ограничения, purpose)

**Files:**
- Create: `DomainServices/Rules/RuleSupport.cs`, `SegRequiredRule.cs`, `SegGrantForbiddenRule.cs`, `CoaCombinationActiveRule.cs`, `FundDeptObjectAllowedRule.cs`, `GrantEligibleRule.cs`, `VendorEligibleRule.cs`, `ProcurementThresholdRule.cs`, `InvoiceDuplicateRule.cs`
- Test: `tests/.../Rules/Step1To4RulesTests.cs`

**Interfaces:**
- Produces: восемь классов `IValidationRule` с `RuleId`, совпадающим со спекой 4.2. Внутренний помощник `RuleSupport` (`Inputs(DistributionSnapshot, ...)`, `Map(...)`). `ProcurementThresholdRule.IsWeakening` — повышение `threshold` над базовым слоем.
- Даты по назначению (spec §5, правило 6): комбинация — на `InvoiceDate`; допустимость гранта уже вычислена сборщиком на `ServiceDate` и приходит в `GrantSnapshot.Eligibility`.

- [ ] **Step 1: Тесты**

`tests/.../Rules/Step1To4RulesTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests.Rules;

public class Step1To4RulesTests
{
    private static Entities.RuleDefinition Def(string id) => DemoRules.All().Single(r => r.RuleId == id);

    [Fact]
    public void SegRequired_701_without_grant_is_hard_stop()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100", 1000m, fund: SubjectBuilder.Grants701())).Build();
        var o = new SegRequiredRule().Evaluate(s, Def("SEG_REQUIRED"));
        o.Should().ContainSingle(x => x.Severity == Severity.HardStop && x.DistributionLine == 1);
        o[0].Inputs["grantPolicy"].Should().Be("Required");
    }

    [Fact]
    public void SegRequired_701_with_grant_passes() =>
        new SegRequiredRule().Evaluate(SubjectBuilder.Exercise(), Def("SEG_REQUIRED")).Should().BeEmpty();

    [Fact]
    public void SegGrantForbidden_101_with_grant_is_hard_stop()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100-G-COPS-26", 1000m, fund: SubjectBuilder.General101())).Build();
        new SegGrantForbiddenRule().Evaluate(s, Def("SEG_GRANT_FORBIDDEN")).Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Fact]
    public void CoaCombination_missing_and_inactive_are_hard_stop()
    {
        var missing = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 1m,
            combination: new CombinationSnapshot(false, false, "Missing"))).Build();
        var inactive = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-FEMA-24", 1m,
            combination: new CombinationSnapshot(true, false, "Inactive"))).Build();
        new CoaCombinationActiveRule().Evaluate(missing, Def("COA_COMBINATION_ACTIVE")).Should().ContainSingle(x => x.Message.Contains("does not exist"));
        new CoaCombinationActiveRule().Evaluate(inactive, Def("COA_COMBINATION_ACTIVE")).Should().ContainSingle(x => x.Message.Contains("Inactive"));
    }

    [Fact]
    public void FundDeptObject_restriction_is_hard_stop()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "202-3000-53100", 1m,
            fund: SubjectBuilder.Street202(FundRestriction.DepartmentNotAllowed))).Build();
        new FundDeptObjectAllowedRule().Evaluate(s, Def("FUND_DEPT_OBJECT_ALLOWED"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Computed["restriction"] == "DepartmentNotAllowed");
    }

    [Theory]
    [InlineData(GrantEligibilityResult.GrantNotActive)]
    [InlineData(GrantEligibilityResult.OutsidePeriod)]
    [InlineData(GrantEligibilityResult.DepartmentNotAllowed)]
    [InlineData(GrantEligibilityResult.ObjectNotAllowed)]
    public void GrantEligible_failures_are_hard_stop(GrantEligibilityResult e)
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-4000-53100-G-COPS-26", 1m, grant: SubjectBuilder.Cops(e))).Build();
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Computed["eligibility"] == e.ToString());
    }

    [Fact]
    public void GrantEligible_reports_service_date()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 1m,
            grant: SubjectBuilder.Cops(GrantEligibilityResult.OutsidePeriod))).Build();
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE"))[0].Inputs["serviceDate"].Should().Be("2026-06-15");
    }

    [Fact]
    public void GrantEligible_skips_distributions_without_grant() =>
        new GrantEligibleRule().Evaluate(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m)).Build(), Def("GRANT_ELIGIBLE"))
            .Should().BeEmpty();

    [Fact]
    public void VendorEligible_debarred_or_inactive_is_hard_stop_document_level()
    {
        new VendorEligibleRule().Evaluate(new SubjectBuilder().Vendor(active: false, debarred: true).Build(), Def("VENDOR_ELIGIBLE"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.DistributionLine == null && x.Message.Contains("debarred"));
        new VendorEligibleRule().Evaluate(new SubjectBuilder().Vendor(active: false).Build(), Def("VENDOR_ELIGIBLE"))
            .Should().ContainSingle(x => x.Message.Contains("not active"));
    }

    [Fact]
    public void VendorEligible_federal_grant_requires_sam()
    {
        new VendorEligibleRule().Evaluate(new SubjectBuilder().Vendor(sam: false).Build(), Def("VENDOR_ELIGIBLE"))
            .Should().ContainSingle(x => x.Message.Contains("SAM"));
        var nonFederal = new SubjectBuilder().Vendor(sam: false).With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m)).Build();
        new VendorEligibleRule().Evaluate(nonFederal, Def("VENDOR_ELIGIBLE")).Should().BeEmpty();
    }

    [Fact]
    public void ProcurementThreshold_non_po_at_or_above_threshold_is_soft_stop()
    {
        var o = new ProcurementThresholdRule().Evaluate(SubjectBuilder.Exercise(), Def("PROCUREMENT_THRESHOLD"));
        o.Should().ContainSingle(x => x.Severity == Severity.SoftStop && x.OverridableBy.Contains(ApproverRole.FinanceDirector) && x.DistributionLine == null);
        o[0].Inputs["threshold"].Should().Be("25,000.00");
    }

    [Fact]
    public void ProcurementThreshold_skips_po_backed_and_small()
    {
        new ProcurementThresholdRule().Evaluate(SubjectBuilder.PoBacked(160_000m), Def("PROCUREMENT_THRESHOLD")).Should().BeEmpty();
        new ProcurementThresholdRule().Evaluate(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 24_999m)).Build(),
            Def("PROCUREMENT_THRESHOLD")).Should().BeEmpty();
    }

    [Fact]
    public void ProcurementThreshold_raising_the_threshold_is_weakening()
    {
        var rule = new ProcurementThresholdRule();
        var baseline = Def("PROCUREMENT_THRESHOLD");
        DomainServices.IValidationRule asRule = rule;
        asRule.IsWeakening(baseline, DemoRules.Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.Tenant,
            Severity.SoftStop, new() { ["threshold"] = "50000" }, adjustable: true)).Should().BeTrue();
        asRule.IsWeakening(baseline, DemoRules.Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.Tenant,
            Severity.SoftStop, new() { ["threshold"] = "10000" }, adjustable: true)).Should().BeFalse();
    }

    [Fact]
    public void InvoiceDuplicate_is_hard_stop() =>
        new InvoiceDuplicateRule().Evaluate(new SubjectBuilder().Duplicate().Build(), Def("INVOICE_DUPLICATE"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop);
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

Во всех файлах правил: `using GovErp.Domain.Validation.Entities; using GovErp.Domain.Validation.ValueObjects; namespace GovErp.Domain.Validation.DomainServices.Rules;`.

`RuleSupport.cs`:
```csharp
internal static class RuleSupport
{
    public static Dictionary<string, string> Inputs(DistributionSnapshot d, params (string Key, string Value)[] extra)
    {
        var map = new Dictionary<string, string>
        {
            ["line"] = d.LineNo.ToString(System.Globalization.CultureInfo.InvariantCulture),
            ["account"] = d.Account.ToString(),
            ["amount"] = d.Amount.ToString(),
        };
        foreach (var (k, v) in extra)
        {
            map[k] = v;
        }

        return map;
    }

    public static Dictionary<string, string> Map(params (string Key, string Value)[] pairs) =>
        pairs.ToDictionary(p => p.Key, p => p.Value);

    public static string Iso(DateOnly date) => date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
}
```

`SegRequiredRule.cs`:
```csharp
/// <summary>Шаг 1. Fund/Dept/Object гарантированы типом AccountCode; проверяется Grant по политике фонда.</summary>
public sealed class SegRequiredRule : IValidationRule
{
    public string RuleId => "SEG_REQUIRED";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Fund is { GrantRule: GrantRule.Required } && d.Account.Grant is null)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("fund", d.Fund!.Code), ("grantPolicy", d.Fund.GrantRule.ToString())),
                RuleSupport.Map(("missingSegment", "Grant")),
                $"Fund {d.Fund.Code} ({d.Fund.Name}) requires a grant segment on line {d.LineNo}."))
            .ToList();
}
```

`SegGrantForbiddenRule.cs`:
```csharp
public sealed class SegGrantForbiddenRule : IValidationRule
{
    public string RuleId => "SEG_GRANT_FORBIDDEN";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Fund is { GrantRule: GrantRule.Forbidden } && d.Account.Grant is not null)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("fund", d.Fund!.Code), ("grantPolicy", "Forbidden"), ("grant", d.Account.Grant!.Value)),
                RuleSupport.Map(),
                $"Fund {d.Fund.Code} ({d.Fund.Name}) does not accept a grant segment; line {d.LineNo} carries {d.Account.Grant}."))
            .ToList();
}
```

`CoaCombinationActiveRule.cs`:
```csharp
/// <summary>Шаг 2. Комбинация в whitelist и действует на InvoiceDate.</summary>
public sealed class CoaCombinationActiveRule : IValidationRule
{
    public string RuleId => "COA_COMBINATION_ACTIVE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var date = RuleSupport.Iso(subject.Transaction.InvoiceDate);
        return subject.Distributions
            .Where(d => !d.Combination.Exists || !d.Combination.IsActiveOnDate)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("exists", d.Combination.Exists.ToString()), ("status", d.Combination.Status), ("invoiceDate", date)),
                RuleSupport.Map(),
                d.Combination.Exists
                    ? $"Account combination {d.Account} is {d.Combination.Status} on {date} (line {d.LineNo})."
                    : $"Account combination {d.Account} does not exist in the chart of accounts (line {d.LineNo})."))
            .ToList();
    }
}
```

`FundDeptObjectAllowedRule.cs`:
```csharp
public sealed class FundDeptObjectAllowedRule : IValidationRule
{
    public string RuleId => "FUND_DEPT_OBJECT_ALLOWED";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Fund is not null && d.Fund.Restriction != FundRestriction.Allowed)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("fund", d.Fund!.Code)),
                RuleSupport.Map(("restriction", d.Fund.Restriction.ToString())),
                d.Fund.Restriction == FundRestriction.DepartmentNotAllowed
                    ? $"Department {d.Account.Department} may not spend from fund {d.Fund.Code} ({d.Fund.Name}) (line {d.LineNo})."
                    : $"Object {d.Account.Object} is not an allowed use of fund {d.Fund.Code} ({d.Fund.Name}) (line {d.LineNo})."))
            .ToList();
}
```

`GrantEligibleRule.cs`:
```csharp
/// <summary>Шаг 3. Допустимость гранта на ServiceDate (вычислена сборщиком снимка).</summary>
public sealed class GrantEligibleRule : IValidationRule
{
    public string RuleId => "GRANT_ELIGIBLE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var serviceDate = RuleSupport.Iso(subject.Transaction.ServiceDate);
        return subject.Distributions
            .Where(d => d.Grant is not null && d.Grant.Eligibility != GrantEligibilityResult.Eligible)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("grant", d.Grant!.Code), ("grantStatus", d.Grant.Status), ("serviceDate", serviceDate)),
                RuleSupport.Map(("eligibility", d.Grant.Eligibility.ToString())),
                d.Grant.Eligibility switch
                {
                    GrantEligibilityResult.GrantNotActive => $"Grant {d.Grant.Code} is {d.Grant.Status} (line {d.LineNo}).",
                    GrantEligibilityResult.OutsidePeriod => $"Service date {serviceDate} is outside the period of grant {d.Grant.Code} (line {d.LineNo}).",
                    GrantEligibilityResult.DepartmentNotAllowed => $"Department {d.Account.Department} is not covered by grant {d.Grant.Code} (line {d.LineNo}).",
                    _ => $"Object {d.Account.Object} is not an allowable cost under grant {d.Grant.Code} (line {d.LineNo}).",
                }))
            .ToList();
    }
}
```

`VendorEligibleRule.cs`:
```csharp
public sealed class VendorEligibleRule : IValidationRule
{
    public string RuleId => "VENDOR_ELIGIBLE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var v = subject.Transaction.Vendor;
        var severity = definition.Severity ?? Severity.HardStop;
        var inputs = RuleSupport.Map(("vendor", v.Name), ("active", v.IsActive.ToString()), ("debarred", v.IsDebarred.ToString()), ("samRegistered", v.SamRegistered.ToString()));
        if (v.IsDebarred)
        {
            return [RuleOutcome.From(definition, severity, null, inputs, RuleSupport.Map(), $"Vendor {v.Name} is debarred from government contracts.")];
        }

        if (!v.IsActive)
        {
            return [RuleOutcome.From(definition, severity, null, inputs, RuleSupport.Map(), $"Vendor {v.Name} is not active.")];
        }

        if (subject.Distributions.Any(d => d.Grant is { IsFederal: true }) && !v.SamRegistered)
        {
            return [RuleOutcome.From(definition, severity, null, inputs, RuleSupport.Map(("federalGrant", "true")),
                $"Vendor {v.Name} must be registered in SAM.gov to be paid from a federal grant.")];
        }

        return [];
    }
}
```

`ProcurementThresholdRule.cs`:
```csharp
public sealed class ProcurementThresholdRule : IValidationRule
{
    public string RuleId => "PROCUREMENT_THRESHOLD";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var threshold = Money.Of(definition.DecimalParameter("threshold"));
        var t = subject.Transaction;
        if (t.IsPoBacked || t.Total < threshold)
        {
            return [];
        }

        return [RuleOutcome.From(definition, definition.Severity ?? Severity.SoftStop, null,
            RuleSupport.Map(("total", t.Total.ToString()), ("threshold", threshold.ToString()), ("poBacked", "false")),
            RuleSupport.Map(),
            $"Non-PO invoice of {t.Total} meets the {threshold} procurement threshold; a purchase order or documented procurement exception is required.")];
    }

    /// <summary>Повышение порога ослабляет контроль.</summary>
    public bool IsWeakening(RuleDefinition baseline, RuleDefinition candidate) =>
        candidate.DecimalParameter("threshold") > baseline.DecimalParameter("threshold")
        || (candidate.Severity ?? Severity.SoftStop) < (baseline.Severity ?? Severity.SoftStop);
}
```

`InvoiceDuplicateRule.cs`:
```csharp
public sealed class InvoiceDuplicateRule : IValidationRule
{
    public string RuleId => "INVOICE_DUPLICATE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Transaction.IsDuplicate
            ? [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null,
                RuleSupport.Map(("vendor", subject.Transaction.Vendor.Name), ("transactionRef", subject.Transaction.TransactionRef)),
                RuleSupport.Map(),
                $"An invoice with the same number from {subject.Transaction.Vendor.Name} already exists.")]
            : [];
}
```

- [ ] **Step 4: Прогнать**

Expected: 41 passed (24 + 17; строки `InlineData` считаются отдельно).

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: rules for steps 1-4 (segments, combination, restrictions, purpose)

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 5: Правила шагов 5–6 (бюджет по ключу, ликвидация и допуск PO)

**Files:**
- Create: `DomainServices/Rules/BudgetAvailabilityRule.cs`, `BudgetLowRemainingRule.cs`, `PoLiquidationRule.cs`
- Test: `tests/.../Rules/BudgetRulesTests.cs`, `tests/.../Rules/PoLiquidationRuleTests.cs`

**Interfaces:**
- Produces: три правила. Бюджетные outcome'ы — по **бюджетному ключу**: `BudgetKey = account`, `DistributionLine = FirstLineOf(key)` (к ней привязывается override). `BUDGET_AVAILABILITY` — severity по `Fund.Control`; `Computed["availableForInvoice"]`, `["projectedAvailable"]`, `["overage"]`. `PO_LIQUIDATION` — по PO-строке; `Computed["liquidation"]`, `["newBudgetPart"]`, `["cumulativeExcessPct"]`. `IsWeakening`: `BUDGET_LOW_REMAINING` — снижение `pct`; `PO_LIQUIDATION` — повышение `tolerance_pct`.
- Бюджет проверяется, только если `RequiredNewBudget(key) > 0`: полностью ликвидируемый PO-инвойс новый бюджет не потребляет (spec §4.1, consistency §3).

- [ ] **Step 1: Тесты бюджета**

`tests/.../Rules/BudgetRulesTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests.Rules;

public class BudgetRulesTests
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private static Entities.RuleDefinition Def(string id) => DemoRules.All().Single(r => r.RuleId == id);

    [Fact]
    public void Exercise_scenario_hard_stop_with_13000_overage()
    {
        var o = new BudgetAvailabilityRule().Evaluate(SubjectBuilder.Exercise(), Def("BUDGET_AVAILABILITY"));
        o.Should().ContainSingle();
        o[0].Severity.Should().Be(Severity.HardStop);
        o[0].OverridableBy.Should().BeEmpty();                     // Hard-фонд: override невозможен
        o[0].BudgetKey.Should().Be(Fire);
        o[0].DistributionLine.Should().Be(1);
        o[0].Inputs["amended"].Should().Be("375,000.00");
        o[0].Inputs["actuals"].Should().Be("132,000.00");
        o[0].Inputs["encumbered"].Should().Be("96,000.00");
        o[0].Inputs["requiredNewBudget"].Should().Be("160,000.00");
        o[0].Computed["availableForInvoice"].Should().Be("147,000.00");
        o[0].Computed["overage"].Should().Be("13,000.00");
        o[0].Computed["projectedAvailable"].Should().Be("-13,000.00");
        o[0].Message.Should().Contain("13,000.00");
        o[0].Resolution.Should().Contain("Budget amendment");
    }

    [Fact]
    public void After_amendment_passes()
    {
        var s = new SubjectBuilder().Budget(SubjectBuilder.Budget(Fire, 388_000m, 132_000m, 96_000m)).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY")).Should().BeEmpty();
    }

    [Fact]
    public void Soft_fund_overage_is_soft_stop_overridable()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m)).Build();
        var o = new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"));
        o.Should().ContainSingle(x => x.Severity == Severity.SoftStop);
        o[0].OverridableBy.Should().Contain(ApproverRole.BudgetOfficer);
        o[0].Computed["overage"].Should().Be("2,000.00");
    }

    [Fact]
    public void Missing_budget_line_is_hard_stop_regardless_of_mode()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-3000-54000", 1m)).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Message.Contains("No budget"));
    }

    [Fact]
    public void Scenario_SameBudgetAcrossDistributions_IsHardStop13000()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, Fire, 80_000m))
            .With(SubjectBuilder.Distribution(2, Fire, 80_000m)).Build();
        var o = new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"));
        o.Should().ContainSingle(x => x.Computed["overage"] == "13,000.00" && x.DistributionLine == 1);
    }

    [Fact]
    public void Scenario_OwnReservationIsNotChargedTwice()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, Fire, 100_000m))
            .Budget(SubjectBuilder.Budget(Fire, 375_000m, 132_000m, 96_000m, held: 100_000m, ownHeld: 100_000m)).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY")).Should().BeEmpty();
    }

    [Fact]
    public void Held_reservations_of_other_invoices_reduce_available()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, Fire, 100_000m))
            .Budget(SubjectBuilder.Budget(Fire, 375_000m, 132_000m, 96_000m, held: 100_000m)).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"))
            .Should().ContainSingle(x => x.Computed["availableForInvoice"] == "47,000.00");
    }

    [Fact]
    public void Fully_liquidated_po_invoice_is_not_checked_against_budget()
    {
        var s = SubjectBuilder.PoBackedBuilder(160_000m)
            .Budget(SubjectBuilder.Budget("701-3000-53100-G-COPS-26", 100_000m, 100_000m, 160_000m)).Build();   // строка уже в минусе
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY")).Should().BeEmpty();
    }

    [Fact]
    public void LowRemaining_warns_below_10_percent()
    {
        // 25,000 − 5,000 = 20,000; после 18,500 остаётся 1,500 = 6% от 25,000
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "202-4000-53100", 18_500m)).Build();
        var o = new BudgetLowRemainingRule().Evaluate(s, Def("BUDGET_LOW_REMAINING"));
        o.Should().ContainSingle(x => x.Severity == Severity.Warning);
        o[0].Computed["remainingPct"].Should().Be("6.00");
    }

    [Fact]
    public void LowRemaining_warns_at_exactly_zero_after_amendment()
    {
        var s = new SubjectBuilder().Budget(SubjectBuilder.Budget(Fire, 388_000m, 132_000m, 96_000m)).Build();
        new BudgetLowRemainingRule().Evaluate(s, Def("BUDGET_LOW_REMAINING"))
            .Should().ContainSingle(x => x.Computed["projectedAvailable"] == "0.00");
    }

    [Fact]
    public void LowRemaining_silent_when_over_budget_or_healthy()
    {
        new BudgetLowRemainingRule().Evaluate(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "202-4000-53100", 8_000m)).Build(),
            Def("BUDGET_LOW_REMAINING")).Should().BeEmpty();
        new BudgetLowRemainingRule().Evaluate(SubjectBuilder.Exercise(), Def("BUDGET_LOW_REMAINING")).Should().BeEmpty();   // дефицит — дело BUDGET_AVAILABILITY
    }

    [Fact]
    public void LowRemaining_lowering_pct_is_weakening()
    {
        IValidationRule rule = new BudgetLowRemainingRule();
        rule.IsWeakening(Def("BUDGET_LOW_REMAINING"), DemoRules.Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability,
            RuleLayer.Tenant, Severity.Warning, new() { ["pct"] = "0.05" }, adjustable: true)).Should().BeTrue();
    }
}
```

- [ ] **Step 2: Тесты PO**

`tests/.../Rules/PoLiquidationRuleTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests.Rules;

public class PoLiquidationRuleTests
{
    private static Entities.RuleDefinition Def => DemoRules.All().Single(r => r.RuleId == "PO_LIQUIDATION");
    private static IReadOnlyList<RuleOutcome> Run(ValidationSubject s) => new PoLiquidationRule().Evaluate(s, Def);

    [Fact]
    public void Within_claimable_is_informational_allowed()
    {
        var o = Run(SubjectBuilder.PoBacked(160_000m));
        o.Should().ContainSingle(x => x.Severity == Severity.Allowed);
        o[0].Computed["liquidation"].Should().Be("160,000.00");
        o[0].Computed["newBudgetPart"].Should().Be("0.00");
    }

    [Fact]
    public void Cumulative_excess_3_percent_is_warning()
    {
        var o = Run(SubjectBuilder.PoBacked(164_800m));
        o.Should().ContainSingle(x => x.Severity == Severity.Warning);
        o[0].Computed["cumulativeExcessPct"].Should().Be("3.00");
        o[0].Computed["newBudgetPart"].Should().Be("4,800.00");
    }

    [Fact]
    public void Cumulative_excess_8_percent_is_hard_stop() =>
        Run(SubjectBuilder.PoBacked(172_800m)).Should().ContainSingle(x => x.Severity == Severity.HardStop);

    [Fact]
    public void Tolerance_counts_already_posted_and_other_claims()
    {
        // Утверждено 160,000; проведено 100,000; чужие claims 50,000; этот инвойс 20,000 → 170,000 → 6.25% → Hard
        Run(SubjectBuilder.PoBacked(20_000m, remaining: 60_000m, otherHeldClaims: 50_000m, alreadyPosted: 100_000m, otherBillingClaims: 50_000m))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Computed["cumulativeExcessPct"] == "6.25");
    }

    [Fact]
    public void Closed_po_line_is_hard_stop() =>
        Run(SubjectBuilder.PoBacked(1m, poOpen: false)).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Message.Contains("closed"));

    [Fact]
    public void Missing_po_line_is_hard_stop()
    {
        var s = new SubjectBuilder().Po("PO-2026-0451", new PoLineSnapshot("PO-2026-0451/1", AccountCode.Parse("701-3000-53100-G-COPS-26"), true,
                Money.Of(160_000m), Money.Zero, Money.Zero, null))
            .With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 1m, poLineRef: "PO-2026-0451/9")).Build();
        Run(s).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Message.Contains("not found"));
    }

    [Fact]
    public void Distribution_account_must_match_po_line_account()
    {
        var s = SubjectBuilder.PoBackedBuilder(160_000m)
            .With(SubjectBuilder.Distribution(2, "701-6000-53100-G-COPS-26", 1m, poLineRef: "PO-2026-0451/1")).Build();
        Run(s).Should().Contain(x => x.Severity == Severity.HardStop && x.Message.Contains("does not match"));
    }

    [Fact]
    public void Skips_non_po() => Run(SubjectBuilder.Exercise()).Should().BeEmpty();

    [Fact]
    public void Raising_tolerance_is_weakening()
    {
        IValidationRule rule = new PoLiquidationRule();
        rule.IsWeakening(Def, DemoRules.Rule("PO_LIQUIDATION", ValidationStep.EncumbranceImpact, RuleLayer.Tenant, null,
            new() { ["tolerance_pct"] = "0.10" }, adjustable: true)).Should().BeTrue();
    }
}
```

- [ ] **Step 3: Убедиться, что не компилируется**

- [ ] **Step 4: Реализация**

Во всех файлах: `using System.Globalization; using GovErp.Domain.Validation.Entities; using GovErp.Domain.Validation.ValueObjects; namespace GovErp.Domain.Validation.DomainServices.Rules;`.

`BudgetAvailabilityRule.cs`:
```csharp
/// <summary>Шаг 5. По бюджетному ключу. Hard-фонд → HardStop без override; Soft → SoftStop. Нет строки бюджета → HardStop.</summary>
public sealed class BudgetAvailabilityRule : IValidationRule
{
    public string RuleId => "BUDGET_AVAILABILITY";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var outcomes = new List<RuleOutcome>();
        foreach (var key in subject.BudgetKeys)
        {
            var required = subject.RequiredNewBudget(key);
            if (required.IsZero)
            {
                continue;
            }

            var b = subject.BudgetFor(key);
            var fund = subject.Distributions.First(d => d.Account == key).Fund;
            var line = subject.FirstLineOf(key);
            var inputs = RuleSupport.Map(
                ("account", key.ToString()), ("amended", b.Amended.ToString()), ("actuals", b.Actuals.ToString()),
                ("encumbered", b.Encumbered.ToString()), ("held", b.Held.ToString()), ("ownHeld", b.OwnHeld.ToString()),
                ("requested", subject.RequestedFor(key).ToString()), ("liquidation", subject.LiquidationFor(key).ToString()),
                ("requiredNewBudget", required.ToString()), ("controlMode", fund?.Control.ToString() ?? "Unknown"));

            if (!b.Exists)
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, line, inputs, RuleSupport.Map(),
                    $"No budget line exists for {key} in the fiscal year of the posting date.", key.ToString())
                    with { OverridableBy = [], Resolution = "Adopt or amend a budget for this account before submitting." });
                continue;
            }

            var projected = subject.ProjectedAvailable(key);
            if (!projected.IsNegative)
            {
                continue;
            }

            var overage = -projected;
            var hard = fund is null || fund.Control == BudgetControl.Hard;
            var outcome = RuleOutcome.From(definition, hard ? Severity.HardStop : Severity.SoftStop, line, inputs,
                RuleSupport.Map(("availableForInvoice", b.AvailableForInvoice.ToString()), ("projectedAvailable", projected.ToString()),
                    ("overage", overage.ToString())),
                $"{key} exceeds available budget by {overage} (available {b.AvailableForInvoice}, required {required}).", key.ToString());
            outcomes.Add(hard
                ? outcome with { OverridableBy = [], Resolution = "Budget amendment, grant-budget revision, or authorized coding change." }
                : outcome);
        }

        return outcomes;
    }
}
```

`BudgetLowRemainingRule.cs`:
```csharp
public sealed class BudgetLowRemainingRule : IValidationRule
{
    public string RuleId => "BUDGET_LOW_REMAINING";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var pct = definition.DecimalParameter("pct");
        var outcomes = new List<RuleOutcome>();
        foreach (var key in subject.BudgetKeys)
        {
            var b = subject.BudgetFor(key);
            if (!b.Exists || b.Amended.IsZero || subject.RequiredNewBudget(key).IsZero)
            {
                continue;
            }

            var projected = subject.ProjectedAvailable(key);
            if (projected.IsNegative)
            {
                continue;   // дефицит — предмет BUDGET_AVAILABILITY
            }

            var remainingPct = projected.Amount / b.Amended.Amount;
            if (remainingPct >= pct)
            {
                continue;
            }

            outcomes.Add(RuleOutcome.From(definition, definition.Severity ?? Severity.Warning, subject.FirstLineOf(key),
                RuleSupport.Map(("account", key.ToString()), ("amended", b.Amended.ToString()), ("availableForInvoice", b.AvailableForInvoice.ToString()),
                    ("thresholdPct", (pct * 100).ToString("0.00", CultureInfo.InvariantCulture))),
                RuleSupport.Map(("projectedAvailable", projected.ToString()), ("remainingPct", (remainingPct * 100).ToString("0.00", CultureInfo.InvariantCulture))),
                $"After this invoice only {projected} ({remainingPct.ToString("P2", CultureInfo.InvariantCulture)}) of the {key} budget remains.", key.ToString()));
        }

        return outcomes;
    }

    public bool IsWeakening(RuleDefinition baseline, RuleDefinition candidate) =>
        candidate.DecimalParameter("pct") < baseline.DecimalParameter("pct");
}
```

`PoLiquidationRule.cs`:
```csharp
/// <summary>
/// Шаг 6. По каждой PO-строке инвойса: строка существует и открыта, счёт совпадает, утверждённая сумма > 0;
/// накопленное превышение от утверждённой суммы: 0 → Allowed (информационный), ≤ tolerance → Warning, иначе HardStop.
/// </summary>
public sealed class PoLiquidationRule : IValidationRule
{
    public string RuleId => "PO_LIQUIDATION";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        if (!subject.Transaction.IsPoBacked)
        {
            return [];
        }

        var tolerance = definition.DecimalParameter("tolerance_pct");
        var outcomes = new List<RuleOutcome>();
        foreach (var poRef in subject.Distributions.Where(d => d.PoLineRef is not null).Select(d => d.PoLineRef!).Distinct())
        {
            var lineNo = subject.Distributions.Where(d => d.PoLineRef == poRef).Min(d => d.LineNo);
            var poLine = subject.PoLine(poRef);
            if (poLine is null)
            {
                outcomes.Add(Hard(definition, lineNo, poRef, $"PO line {poRef} was not found."));
                continue;
            }

            if (!poLine.IsOpen)
            {
                outcomes.Add(Hard(definition, lineNo, poRef, $"PO line {poRef} is closed."));
                continue;
            }

            if (poLine.AuthorizedAmount <= Money.Zero)
            {
                outcomes.Add(Hard(definition, lineNo, poRef, $"PO line {poRef} has no authorized amount."));
                continue;
            }

            var mismatch = subject.Distributions.FirstOrDefault(d => d.PoLineRef == poRef && d.Account != poLine.Account);
            if (mismatch is not null)
            {
                outcomes.Add(Hard(definition, mismatch.LineNo, poRef,
                    $"Line {mismatch.LineNo} account {mismatch.Account} does not match PO line {poRef} account {poLine.Account}."));
                continue;
            }

            var amount = subject.PoAmount(poRef);
            var liquidation = subject.EligibleLiquidation(poRef);
            var pct = subject.CumulativeExcessPct(poRef);
            var inputs = RuleSupport.Map(("poLine", poRef), ("invoicePoAmount", amount.ToString()),
                ("authorized", poLine.AuthorizedAmount.ToString()), ("alreadyPosted", poLine.AlreadyPosted.ToString()),
                ("otherActiveClaims", poLine.OtherActiveClaims.ToString()),
                ("claimable", (poLine.Encumbrance?.ClaimableForInvoice ?? Money.Zero).ToString()),
                ("tolerancePct", (tolerance * 100).ToString("0.00", CultureInfo.InvariantCulture)));
            var computed = RuleSupport.Map(("liquidation", liquidation.ToString()), ("newBudgetPart", (amount - liquidation).ToString()),
                ("cumulativeExcessPct", (pct * 100).ToString("0.00", CultureInfo.InvariantCulture)));

            var (severity, message) = pct == 0m
                ? (Severity.Allowed, $"PO line {poRef}: {liquidation} liquidates the encumbrance; {amount - liquidation} is charged to available budget.")
                : pct <= tolerance
                    ? (Severity.Warning, $"PO line {poRef} is over its authorized amount by {(pct * 100).ToString("0.00", CultureInfo.InvariantCulture)}%, within the {(tolerance * 100).ToString("0.##", CultureInfo.InvariantCulture)}% tolerance.")
                    : (Severity.HardStop, $"PO line {poRef} is over its authorized amount by {(pct * 100).ToString("0.00", CultureInfo.InvariantCulture)}%, above the {(tolerance * 100).ToString("0.##", CultureInfo.InvariantCulture)}% tolerance; a change order is required.");
            outcomes.Add(RuleOutcome.From(definition, severity, lineNo, inputs, computed, message));
        }

        return outcomes;
    }

    public bool IsWeakening(RuleDefinition baseline, RuleDefinition candidate) =>
        candidate.DecimalParameter("tolerance_pct") > baseline.DecimalParameter("tolerance_pct");

    private static RuleOutcome Hard(RuleDefinition definition, int line, string poRef, string message) =>
        RuleOutcome.From(definition, Severity.HardStop, line, RuleSupport.Map(("poLine", poRef)), RuleSupport.Map(), message);
}
```

- [ ] **Step 5: Прогнать**

Expected: 62 passed (41 + 12 + 9).

- [ ] **Step 6: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: budget availability per budget key, low remaining, cumulative PO tolerance

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: ApprovalRouting, PostingPreviewComposer, PostingEligibility

**Files:**
- Create: `ValueObjects/ApprovalRequirement.cs`, `PostingPreviewLine.cs`, `PostingCheck.cs`; `DomainServices/ApprovalRouting.cs`, `PostingPreviewComposer.cs`, `PostingEligibility.cs`
- Test: `ApprovalRoutingTests.cs`, `PostingPreviewTests.cs`, `PostingEligibilityTests.cs`

**Interfaces:**
- Produces: `ApprovalRequirement(ApproverRole Role, string? Department, string Reason, bool IsSatisfied)`; `ApprovalRouting.Build(ValidationSubject, IReadOnlyList<RuleOutcome> outcomesWithOverrides, RuleDefinition? routeRule) → IReadOnlyList<ApprovalRequirement>`; `PostingPreviewLine(AccountCode Account, string Family, Money Debit, Money Credit, string Description)` где `Family` ∈ `"Financial" | "Budgetary"`; `PostingPreviewComposer.Compose(ValidationSubject)`, `PostingPreviewComposer.IsBalancedPerFundAndFamily(lines)`; `PostingCheck(bool Passed, IReadOnlyList<string> Failures)`; `PostingEligibility.Check(ValidationSubject, Severity overall, route, preview, string currentFingerprint) → PostingCheck`; константа `PostingEligibility.RevalidationRequired = "REVALIDATION_REQUIRED"`.
- Шаг маршрута — пара (роль, департамент). `DepartmentHead` удовлетворяется только согласованием **своего** департамента; остальные роли — согласованием роли (spec §4.3). Удовлетворённость считается по `ActiveApprovals` — прошлые циклы в снимок не попадают.
- Posting preview (spec §2.5): по каждой PO-строке с ненулевой ликвидацией — бюджетная пара (Дт Reserve for Encumbrances / Кт Encumbrances); по каждой distribution — Дт Expenditure (governmental) / Expense (enterprise); по каждому фонду — Кт AP.
- `PostingEligibility` (spec §4.3): `overall ≤ Warning` ∧ все шаги маршрута удовлетворены ∧ период открыт ∧ баланс по (фонд, семейство) ∧ `ApprovalBaseline` совпадает с текущими `ContentVersion` и fingerprint. Несовпадение базовой линии даёт `REVALIDATION_REQUIRED`; оно снимается новым согласованием по актуальному fingerprint (план 3 открывает новый цикл), а не держится вечно.

- [ ] **Step 1: Тесты**

`ApprovalRoutingTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class ApprovalRoutingTests
{
    private static Entities.RuleDefinition Route => DemoRules.All().Single(r => r.RuleId == "APPROVAL_ROUTE");

    [Fact]
    public void Exercise_invoice_routes_to_fire_head_grants_manager_finance_director()
    {
        var route = ApprovalRouting.Build(SubjectBuilder.Exercise(), [], Route);
        route.Select(r => (r.Role, r.Department)).Should().BeEquivalentTo(new[]
        {
            (ApproverRole.DepartmentHead, (string?)"6000"), (ApproverRole.GrantsManager, null), (ApproverRole.FinanceDirector, null),
        });
    }

    [Fact]
    public void Small_general_fund_invoice_needs_only_department_head() =>
        ApprovalRouting.Build(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 4_000m)).Build(), [], Route)
            .Should().ContainSingle(r => r.Role == ApproverRole.DepartmentHead);

    [Fact]
    public void Multi_fund_invoice_needs_each_department_head_once()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 8_000m))
            .With(SubjectBuilder.Distribution(3, "501-5000-53100", 10_000m)).Build();
        ApprovalRouting.Build(s, [], Route).Where(r => r.Role == ApproverRole.DepartmentHead)
            .Select(r => r.Department).Should().BeEquivalentTo("6000", "4000", "5000");
    }

    [Fact]
    public void PostingEligibility_DepartmentHeadOfOtherDepartment_DoesNotSatisfyStep()
    {
        var s = new SubjectBuilder().Approved(ApproverRole.DepartmentHead, "3000").Build();
        ApprovalRouting.Build(s, [], Route).Single(r => r.Role == ApproverRole.DepartmentHead).IsSatisfied.Should().BeFalse();
        var own = new SubjectBuilder().Approved(ApproverRole.DepartmentHead, "6000").Build();
        ApprovalRouting.Build(own, [], Route).Single(r => r.Role == ApproverRole.DepartmentHead).IsSatisfied.Should().BeTrue();
    }

    [Fact]
    public void Unoverridden_soft_stop_adds_its_overrider_once()
    {
        var soft = RuleOutcome.From(DemoRules.All().Single(r => r.RuleId == "PROCUREMENT_THRESHOLD"), Severity.SoftStop, null,
            new Dictionary<string, string>(), new Dictionary<string, string>());
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 30_000m)).Build();
        ApprovalRouting.Build(s, [soft], Route)
            .Should().ContainSingle(r => r.Role == ApproverRole.FinanceDirector && r.Reason.Contains("PROCUREMENT_THRESHOLD"));
    }

    [Fact]
    public void Finance_director_threshold_comes_from_rule_parameters()
    {
        var lower = DemoRules.Rule("APPROVAL_ROUTE", ValidationStep.ApprovalRequirements, RuleLayer.Tenant, null,
            new() { ["finance_director_threshold"] = "5000" }, adjustable: true);
        ApprovalRouting.Build(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 6_000m)).Build(), [], lower)
            .Should().Contain(r => r.Role == ApproverRole.FinanceDirector);
    }
}
```

`PostingPreviewTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class PostingPreviewTests
{
    [Fact]
    public void Non_po_governmental_gives_expenditure_and_ap_pair()
    {
        var lines = PostingPreviewComposer.Compose(SubjectBuilder.Exercise());
        lines.Should().HaveCount(2);
        lines[0].Should().BeEquivalentTo(new { Account = AccountCode.Parse("701-6000-53100-G-COPS-26"), Family = "Financial", Debit = Money.Of(160_000m), Credit = Money.Zero });
        lines[0].Description.Should().StartWith("Expenditure");
        lines[1].Should().BeEquivalentTo(new { Account = AccountCode.Parse("701-0000-2100"), Family = "Financial", Debit = Money.Zero, Credit = Money.Of(160_000m) });
    }

    [Fact]
    public void Enterprise_fund_uses_expense_wording() =>
        PostingPreviewComposer.Compose(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "501-5000-53100", 10_000m)).Build())[0]
            .Description.Should().StartWith("Expense").And.NotContain("Expenditure");

    [Fact]
    public void PostingPreview_PoBacked_HasBudgetaryReversalLines()
    {
        var lines = PostingPreviewComposer.Compose(SubjectBuilder.PoBacked(160_000m));
        lines.Should().HaveCount(4);
        lines[0].Should().BeEquivalentTo(new { Account = AccountCode.Parse("701-0000-2900-G-COPS-26"), Family = "Budgetary", Debit = Money.Of(160_000m) });
        lines[1].Should().BeEquivalentTo(new { Account = AccountCode.Parse("701-3000-5900-G-COPS-26"), Family = "Budgetary", Credit = Money.Of(160_000m) });
        lines[2].Family.Should().Be("Financial");
    }

    [Fact]
    public void Partial_liquidation_reverses_only_the_liquidated_part()
    {
        var lines = PostingPreviewComposer.Compose(SubjectBuilder.PoBacked(164_800m));
        lines.Where(l => l.Family == "Budgetary").Sum(l => l.Debit.Amount).Should().Be(160_000m);
        lines.Where(l => l.Family == "Financial").Sum(l => l.Debit.Amount).Should().Be(164_800m);
    }

    [Fact]
    public void PostingPreview_BalancedPerFundAndFamily()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 8_000m))
            .With(SubjectBuilder.Distribution(3, "501-5000-53100", 10_000m)).Build();
        var lines = PostingPreviewComposer.Compose(s);
        lines.Where(l => !l.Credit.IsZero).Select(l => l.Account.Fund.Value).Should().BeEquivalentTo("101", "202", "501");
        PostingPreviewComposer.IsBalancedPerFundAndFamily(lines).Should().BeTrue();
    }

    [Fact]
    public void Balance_check_detects_unbalanced_fund() =>
        PostingPreviewComposer.IsBalancedPerFundAndFamily(
        [
            new PostingPreviewLine(AccountCode.Parse("101-6000-53100"), "Financial", Money.Of(10m), Money.Zero, "x"),
            new PostingPreviewLine(AccountCode.Parse("202-0000-2100"), "Financial", Money.Zero, Money.Of(10m), "x"),
        ]).Should().BeFalse();
}
```

`PostingEligibilityTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class PostingEligibilityTests
{
    private static readonly string Fingerprint = DemoRules.Resolved().Fingerprint;
    private static Entities.RuleDefinition Route => DemoRules.All().Single(r => r.RuleId == "APPROVAL_ROUTE");

    private static PostingCheck Check(SubjectBuilder b, Severity overall = Severity.Allowed, string? fingerprint = null)
    {
        var s = b.Build();
        return PostingEligibility.Check(s, overall, ApprovalRouting.Build(s, [], Route), PostingPreviewComposer.Compose(s), fingerprint ?? Fingerprint);
    }

    private static SubjectBuilder FullyApproved() => new SubjectBuilder()
        .Approved(ApproverRole.DepartmentHead, "6000").Approved(ApproverRole.GrantsManager).Approved(ApproverRole.FinanceDirector)
        .Baseline(Fingerprint);

    [Fact]
    public void Passes_when_everything_is_in_place() => Check(FullyApproved()).Passed.Should().BeTrue();

    [Fact]
    public void Fails_on_missing_approval()
    {
        var check = Check(new SubjectBuilder().Approved(ApproverRole.DepartmentHead, "6000").Baseline(Fingerprint));
        check.Passed.Should().BeFalse();
        check.Failures.Should().Contain(f => f.Contains("GrantsManager"));
    }

    [Fact]
    public void Fails_on_closed_period() =>
        Check(FullyApproved().PeriodClosed()).Failures.Should().Contain(f => f.Contains("period"));

    [Fact]
    public void PostingEligibility_FingerprintChanged_RequiresReapproval() =>
        Check(FullyApproved(), fingerprint: new string('0', 64)).Failures.Should().Contain(f => f.StartsWith(PostingEligibility.RevalidationRequired));

    [Fact]
    public void Content_version_changed_since_approval_requires_reapproval() =>
        Check(FullyApproved().ContentVersion(2)).Failures.Should().Contain(f => f.StartsWith(PostingEligibility.RevalidationRequired));

    [Fact]
    public void No_baseline_means_no_approval_basis() =>
        Check(new SubjectBuilder().Approved(ApproverRole.DepartmentHead, "6000").Approved(ApproverRole.GrantsManager).Approved(ApproverRole.FinanceDirector))
            .Failures.Should().Contain(f => f.StartsWith(PostingEligibility.RevalidationRequired));

    [Fact]
    public void Fails_on_soft_or_hard_overall_passes_on_warning()
    {
        Check(FullyApproved(), Severity.SoftStop).Passed.Should().BeFalse();
        Check(FullyApproved(), Severity.Warning).Passed.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

`ValueObjects/ApprovalRequirement.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

public sealed record ApprovalRequirement(ApproverRole Role, string? Department, string Reason, bool IsSatisfied);
```

`ValueObjects/PostingPreviewLine.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Family — строка ("Financial" / "Budgetary"), чтобы не дублировать enum Ledger; маппинг в плане 3.</summary>
public sealed record PostingPreviewLine(AccountCode Account, string Family, Money Debit, Money Credit, string Description);
```

`ValueObjects/PostingCheck.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

public sealed record PostingCheck(bool Passed, IReadOnlyList<string> Failures)
{
    public static readonly PostingCheck Ok = new(true, []);
}
```

`DomainServices/ApprovalRouting.cs`:
```csharp
using System.Globalization;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Шаг 7. DepartmentHead каждого департамента; GrantsManager при гранте; FinanceDirector при total ≥ порога; overrider каждого не снятого Soft Stop.</summary>
public static class ApprovalRouting
{
    public const decimal DefaultFinanceDirectorThreshold = 50_000m;

    public static IReadOnlyList<ApprovalRequirement> Build(ValidationSubject subject, IReadOnlyList<RuleOutcome> outcomes, RuleDefinition? routeRule)
    {
        var threshold = Money.Of(routeRule?.DecimalParameter("finance_director_threshold") ?? DefaultFinanceDirectorThreshold);
        var route = new List<ApprovalRequirement>();

        bool Satisfied(ApproverRole role, string? department) =>
            subject.ActiveApprovals.Any(a => a.Role == role && (role != ApproverRole.DepartmentHead || a.Department == department));

        void Add(ApproverRole role, string? department, string reason)
        {
            if (route.All(r => r.Role != role || r.Department != department))
            {
                route.Add(new ApprovalRequirement(role, department, reason, Satisfied(role, department)));
            }
        }

        foreach (var dept in subject.Distributions.Select(d => d.Account.Department.Value).Distinct())
        {
            Add(ApproverRole.DepartmentHead, dept, $"Department {dept} is charged.");
        }

        if (subject.Distributions.Any(d => d.Account.Grant is not null))
        {
            Add(ApproverRole.GrantsManager, null, "Grant-funded distribution.");
        }

        if (subject.Transaction.Total >= threshold)
        {
            Add(ApproverRole.FinanceDirector, null, $"Total {subject.Transaction.Total} ≥ {threshold}.");
        }

        foreach (var soft in outcomes.Where(o => o.Severity == Severity.SoftStop && !o.IsOverridden && o.OverridableBy.Count > 0))
        {
            var line = soft.DistributionLine?.ToString(CultureInfo.InvariantCulture) ?? "document";
            Add(soft.OverridableBy[0], null, $"Override required for {soft.RuleId} ({line}).");
        }

        return route;
    }
}
```

`DomainServices/PostingPreviewComposer.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Проводки, которые создаст Post. Бюджетная пара сторно encumbrance — по PO-строке, перед финансовыми.</summary>
public static class PostingPreviewComposer
{
    public const string Financial = "Financial";
    public const string Budgetary = "Budgetary";

    public static IReadOnlyList<PostingPreviewLine> Compose(ValidationSubject subject)
    {
        var acc = subject.PostingAccounts;
        var lines = new List<PostingPreviewLine>();

        foreach (var po in subject.PoLines)
        {
            var liquidation = subject.EligibleLiquidation(po.PoLineRef);
            if (liquidation.IsZero)
            {
                continue;
            }

            var reserve = new AccountCode(po.Account.Fund, acc.BalanceSheetDepartment, acc.ReserveForEncumbrances, po.Account.Grant);
            var encumbrances = po.Account.WithObject(acc.Encumbrances);
            lines.Add(new PostingPreviewLine(reserve, Budgetary, liquidation, Money.Zero, $"Reserve for encumbrances — liquidate {po.PoLineRef}"));
            lines.Add(new PostingPreviewLine(encumbrances, Budgetary, Money.Zero, liquidation, $"Encumbrances — liquidate {po.PoLineRef}"));
        }

        foreach (var d in subject.Distributions)
        {
            var wording = d.Fund?.Kind == FundKind.Enterprise ? "Expense" : "Expenditure";
            lines.Add(new PostingPreviewLine(d.Account, Financial, d.Amount, Money.Zero, $"{wording} — line {d.LineNo}"));
        }

        foreach (var g in subject.Distributions.GroupBy(d => d.Account.Fund))
        {
            var total = g.Aggregate(Money.Zero, (s, d) => s + d.Amount);
            var ap = new AccountCode(g.Key, acc.BalanceSheetDepartment, acc.AccountsPayable, null);
            lines.Add(new PostingPreviewLine(ap, Financial, Money.Zero, total, $"Accounts payable — fund {g.Key}"));
        }

        return lines;
    }

    public static bool IsBalancedPerFundAndFamily(IReadOnlyList<PostingPreviewLine> lines) =>
        lines.GroupBy(l => (l.Account.Fund, l.Family))
            .All(g => g.Aggregate(Money.Zero, (s, l) => s + l.Debit) == g.Aggregate(Money.Zero, (s, l) => s + l.Credit));
}
```

`DomainServices/PostingEligibility.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Шаг 8.</summary>
public static class PostingEligibility
{
    public const string RevalidationRequired = "REVALIDATION_REQUIRED";

    public static PostingCheck Check(ValidationSubject subject, Severity overall, IReadOnlyList<ApprovalRequirement> route,
        IReadOnlyList<PostingPreviewLine> preview, string currentFingerprint)
    {
        var failures = new List<string>();
        if (overall > Severity.Warning)
        {
            failures.Add($"Overall result is {overall}.");
        }

        foreach (var r in route.Where(r => !r.IsSatisfied))
        {
            failures.Add($"Approval by {r.Role}{(r.Department is null ? "" : $" ({r.Department})")} is missing in the active approval cycle.");
        }

        if (!subject.PeriodIsOpen)
        {
            failures.Add("The fiscal period of the posting date is closed.");
        }

        if (!PostingPreviewComposer.IsBalancedPerFundAndFamily(preview))
        {
            failures.Add("Posting preview is not balanced per fund and ledger family.");
        }

        var baseline = subject.ApprovalBaseline;
        if (baseline is null)
        {
            failures.Add($"{RevalidationRequired}: no approval in the active cycle records the rule set it was based on.");
        }
        else if (baseline.ContentVersion != subject.Transaction.ContentVersion || baseline.RuleSetFingerprint != currentFingerprint)
        {
            failures.Add($"{RevalidationRequired}: approvals were given for content v{baseline.ContentVersion} and rules " +
                $"{baseline.RuleSetFingerprint[..12]}…, current are v{subject.Transaction.ContentVersion} and {currentFingerprint[..12]}…; approve again.");
        }

        return failures.Count == 0 ? PostingCheck.Ok : new PostingCheck(false, failures);
    }
}
```

- [ ] **Step 4: Прогнать**

Expected: 81 passed (62 + 6 + 6 + 7).

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: department-scoped approval routing, posting preview, posting eligibility baseline

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: EvaluationRecord, RuleCatalog, RuleSetGuard, ValidationPipeline — сквозные сценарии

**Files:**
- Create: `Entities/EvaluationRecord.cs`, `DomainServices/RuleCatalog.cs`, `DomainServices/RuleSetGuard.cs`, `DomainServices/ValidationPipeline.cs`, `Repositories/IEvaluationRecordRepository.cs`
- Test: `RuleSetGuardTests.cs`, `PipelineScenarioTests.cs`
- Modify: `tests/GovErp.Architecture.Tests/ArchitectureFixture.cs` — добавить сборку Validation

**Interfaces:**
- Produces: `ValidationPipeline(RuleCatalog)` с `Evaluate(ValidationSubject, EffectiveRuleSet, EvaluationTrigger, UserId evaluatedBy, DateTimeOffset at) → EvaluationRecord`; `EvaluationRecord` (`Id`, `TransactionRef`, `InvoiceId`, `ContentVersion`, `ApprovalCycleId`, `Trigger`, `EvaluatedAt`, `EvaluatedBy`, `EngineVersion`, `AppliedRules`, `RuleSetFingerprint`, `Outcomes` — уже с overrides, `Overall`, `Capabilities`, `ApprovalRoute`, `PostingPreview`, `PostingCheck` — только при `Trigger == Post`, `InputSnapshot`); `RuleCatalog.Default` — все 11 правил; `RuleCatalog.Find(string)`; `RuleSetGuard.FindViolations(IReadOnlyList<RuleDefinition>, RuleCatalog) → IReadOnlyList<string>`; `IEvaluationRecordRepository { AddAsync; FindAsync(Guid); ListByTransactionAsync(string transactionRef) }`.
- `RuleSetGuard` (spec §4.1 п. 3): для каждого адаптируемого правила сравнивает каждое более специфичное определение с ближайшим менее специфичным через `IValidationRule.IsWeakening`; для необязательных к адаптации правил слои не сравниваются — они выполняются все. Слой сценариев вызывает guard при seed и при сохранении правила (план 3).

- [ ] **Step 1: Тесты guard**

`RuleSetGuardTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class RuleSetGuardTests
{
    [Fact]
    public void Demo_rules_have_no_violations() =>
        RuleSetGuard.FindViolations(DemoRules.All(), RuleCatalog.Default).Should().BeEmpty();

    [Fact]
    public void Resolution_WeakeningThreshold_IsRejected()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.SoftStop,
                new() { ["threshold"] = "100000" }, [ApproverRole.FinanceDirector], adjustable: true),
        };
        RuleSetGuard.FindViolations(rules, RuleCatalog.Default).Should().ContainSingle(v => v.Contains("PROCUREMENT_THRESHOLD") && v.Contains("Tenant"));
    }

    [Fact]
    public void Tightening_is_allowed()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.SoftStop,
                new() { ["threshold"] = "10000" }, [ApproverRole.FinanceDirector], adjustable: true),
        };
        RuleSetGuard.FindViolations(rules, RuleCatalog.Default).Should().BeEmpty();
    }

    [Fact]
    public void Configured_rule_without_implementation_is_a_violation()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("UNKNOWN_RULE", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, adjustable: true),
        };
        RuleSetGuard.FindViolations(rules, RuleCatalog.Default).Should().ContainSingle(v => v.Contains("UNKNOWN_RULE"));
    }
}
```

- [ ] **Step 2: Сквозные сценарии**

`PipelineScenarioTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class PipelineScenarioTests
{
    private const string Fire = "701-6000-53100-G-COPS-26";
    private static readonly DateTimeOffset At = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly EffectiveRuleSet Rules = DemoRules.Resolved();
    private static readonly ValidationPipeline Pipeline = new(RuleCatalog.Default);

    private static Entities.EvaluationRecord Run(ValidationSubject s, EvaluationTrigger t = EvaluationTrigger.Manual) =>
        Pipeline.Evaluate(s, Rules, t, SubjectBuilder.Clerk, At);

    private static SubjectBuilder Amended() => new SubjectBuilder().Budget(SubjectBuilder.Budget(Fire, 388_000m, 132_000m, 96_000m));

    [Fact]
    public void Scenario_NonPo_701_ExceedsAvailable_By13000_IsHardStop()
    {
        var r = Run(SubjectBuilder.Exercise());
        r.Overall.Should().Be(Severity.HardStop);
        r.Capabilities.Should().Be(new Capabilities(true, false, false, false));
        r.Outcomes.Single(o => o.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
        r.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD");   // шаг 4 выполнился до шага 5
        r.ApprovalRoute.Should().NotBeEmpty();                                     // маршрут строится даже при стопе
        r.PostingPreview.Should().BeEmpty();                                       // превью — только если ≤ SoftStop
        r.RuleSetFingerprint.Should().Be(Rules.Fingerprint);
        r.AppliedRules.Should().HaveCount(12);
        r.EngineVersion.Should().Be(RuleResolution.EngineVersion);
        r.ContentVersion.Should().Be(1);
        r.ApprovalCycleId.Should().Be(SubjectBuilder.CycleId);
    }

    [Fact]
    public void Scenario_NonPo_701_AfterAmendment13000_IsSoftStop_ProcurementThreshold()
    {
        var r = Run(Amended().Build());
        r.Overall.Should().Be(Severity.SoftStop);
        r.Outcomes.Should().ContainSingle(o => o.RuleId == "PROCUREMENT_THRESHOLD");
        r.Outcomes.Should().Contain(o => o.RuleId == "BUDGET_LOW_REMAINING");     // остаток 0 → Warning
        r.Capabilities.CanSubmit.Should().BeTrue();
        r.Capabilities.CanApprove.Should().BeFalse();
        r.PostingPreview.Should().HaveCount(2);
        r.ApprovalRoute.Select(a => a.Role).Should().Contain(ApproverRole.FinanceDirector);
    }

    [Fact]
    public void Scenario_NonPo_701_AfterAmendment_WithOverride_IsWarning()
    {
        var r = Run(Amended().Overridden("PROCUREMENT_THRESHOLD").Build());
        r.Overall.Should().Be(Severity.Warning);
        r.Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD").IsOverridden.Should().BeTrue();
        r.Capabilities.CanApprove.Should().BeTrue();
    }

    [Fact]
    public void Scenario_PoBacked_WithinRemaining_IsAllowed_AvailableUnchanged()
    {
        var r = Run(SubjectBuilder.PoBacked(160_000m));
        r.Overall.Should().Be(Severity.Allowed);
        r.Outcomes.Should().ContainSingle(o => o.RuleId == "PO_LIQUIDATION" && o.Severity == Severity.Allowed);
        r.Outcomes.Should().NotContain(o => o.RuleId == "BUDGET_AVAILABILITY" || o.RuleId == "PROCUREMENT_THRESHOLD");
        r.PostingPreview.Should().HaveCount(4);
        r.PostingPreview.Count(l => l.Family == "Budgetary").Should().Be(2);
    }

    [Fact]
    public void Scenario_PoBacked_CumulativeExcess3Pct_IsWarning()
    {
        var r = Run(SubjectBuilder.PoBacked(164_800m));
        r.Overall.Should().Be(Severity.Warning);
        r.Outcomes.Single(o => o.RuleId == "PO_LIQUIDATION").Computed["newBudgetPart"].Should().Be("4,800.00");
    }

    [Fact]
    public void Scenario_PoBacked_CumulativeExcess8Pct_IsHardStop() =>
        Run(SubjectBuilder.PoBacked(172_800m)).Overall.Should().Be(Severity.HardStop);

    [Fact]
    public void Scenario_MultiFund_101_Overage_IsSoftStop_202_501_Allowed()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 8_000m))
            .With(SubjectBuilder.Distribution(3, "501-5000-53100", 10_000m)).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.SoftStop);
        r.Outcomes.Where(o => o.RuleId == "BUDGET_AVAILABILITY").Should().ContainSingle(o => o.BudgetKey == "101-6000-53100" && o.Severity == Severity.SoftStop);
        r.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD");  // 30,000 ≥ 25,000
        r.PostingPreview.Should().HaveCount(6);
        PostingPreviewComposer.IsBalancedPerFundAndFamily(r.PostingPreview).Should().BeTrue();
    }

    [Fact]
    public void Scenario_MultiFund_501_UsesExpenseNotExpenditure() =>
        Run(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "501-5000-53100", 10_000m)).Build())
            .PostingPreview[0].Description.Should().StartWith("Expense");

    [Fact]
    public void Scenario_SameBudgetAcrossDistributions_IsHardStop13000()
    {
        var r = Run(new SubjectBuilder().With(SubjectBuilder.Distribution(1, Fire, 80_000m)).With(SubjectBuilder.Distribution(2, Fire, 80_000m)).Build());
        r.Overall.Should().Be(Severity.HardStop);
        r.Outcomes.Single(o => o.RuleId == "BUDGET_AVAILABILITY").Computed["overage"].Should().Be("13,000.00");
    }

    [Fact]
    public void Scenario_OwnReservationIsNotChargedTwice()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, Fire, 100_000m))
            .Budget(SubjectBuilder.Budget(Fire, 375_000m, 132_000m, 96_000m, held: 100_000m, ownHeld: 100_000m)).Build();
        Run(s, EvaluationTrigger.Approve).Outcomes.Should().NotContain(o => o.RuleId == "BUDGET_AVAILABILITY");
    }

    [Fact]
    public void Pipeline_HardStopAtStep2_SkipsSteps3To6_StillBuildsRoute()
    {
        var r = Run(new SubjectBuilder().With(SubjectBuilder.Distribution(1, Fire, 160_000m,
            combination: new CombinationSnapshot(false, false, "Missing"))).Build());
        r.Outcomes.Select(o => o.Step).Max().Should().Be(ValidationStep.ValidCombination);
        r.Outcomes.Should().NotContain(o => o.RuleId == "BUDGET_AVAILABILITY");
        r.ApprovalRoute.Should().NotBeEmpty();
    }

    [Fact]
    public void Pipeline_NonAdjustableRuleInTwoLayers_BothEvaluated_StrictestWins()
    {
        var rules = DemoRules.Resolved(new List<Entities.RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("VENDOR_ELIGIBLE", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning),
        });
        var r = Pipeline.Evaluate(new SubjectBuilder().Vendor(debarred: true).Build(), rules, EvaluationTrigger.Manual, SubjectBuilder.Clerk, At);
        r.Outcomes.Where(o => o.RuleId == "VENDOR_ELIGIBLE").Select(o => o.Severity).Should().BeEquivalentTo([Severity.HardStop, Severity.Warning]);
        r.Overall.Should().Be(Severity.HardStop);
    }

    [Fact]
    public void Pipeline_Override_ChangesOverall_NotOutcome()
    {
        var r = Run(new SubjectBuilder().Overridden("PROCUREMENT_THRESHOLD")
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 30_000m))
            .Budget(SubjectBuilder.Budget("101-6000-53100", 100_000m, 0m, 0m)).Build());
        r.Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD").Severity.Should().Be(Severity.SoftStop);
        r.Overall.Should().Be(Severity.Allowed);
    }

    [Fact]
    public void Post_trigger_fills_posting_check_and_enables_post()
    {
        var s = new SubjectBuilder().Approved(ApproverRole.DepartmentHead, "6000").Approved(ApproverRole.GrantsManager)
            .Approved(ApproverRole.FinanceDirector).Baseline(Rules.Fingerprint)
            .Overridden("PROCUREMENT_THRESHOLD")                   // non-PO 100,000 ≥ 25,000 → Soft Stop, снят
            .With(SubjectBuilder.Distribution(1, Fire, 100_000m)).Build();
        var post = Run(s, EvaluationTrigger.Post);
        post.PostingCheck!.Passed.Should().BeTrue();
        post.Capabilities.CanPost.Should().BeTrue();
        Run(s, EvaluationTrigger.Manual).PostingCheck.Should().BeNull();
    }

    [Fact]
    public void Record_carries_input_snapshot_and_is_immutable()
    {
        var r = Run(SubjectBuilder.Exercise());
        r.InputSnapshot.Distributions.Should().HaveCount(1);
        typeof(Entities.EvaluationRecord).GetProperties().Should().OnlyContain(p => p.SetMethod == null || !p.SetMethod.IsPublic);
    }
}
```

- [ ] **Step 3: Убедиться, что не компилируется**

- [ ] **Step 4: Реализация**

`Entities/EvaluationRecord.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

/// <summary>Неизменяемая запись одной оценки (GE-12). Создаётся только конвейером.</summary>
public sealed class EvaluationRecord
{
    public Guid Id { get; private set; }
    public string TransactionRef { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public Guid ApprovalCycleId { get; private set; }
    public EvaluationTrigger Trigger { get; private set; }
    public DateTimeOffset EvaluatedAt { get; private set; }
    public UserId EvaluatedBy { get; private set; }
    public string EngineVersion { get; private set; }
    public IReadOnlyList<AppliedRule> AppliedRules { get; private set; }
    public string RuleSetFingerprint { get; private set; }
    public IReadOnlyList<RuleOutcome> Outcomes { get; private set; }
    public Severity Overall { get; private set; }
    public Capabilities Capabilities { get; private set; }
    public IReadOnlyList<ApprovalRequirement> ApprovalRoute { get; private set; }
    public IReadOnlyList<PostingPreviewLine> PostingPreview { get; private set; }
    public PostingCheck? PostingCheck { get; private set; }
    public ValidationSubject InputSnapshot { get; private set; }

    internal EvaluationRecord(ValidationSubject subject, EvaluationTrigger trigger, DateTimeOffset evaluatedAt, UserId evaluatedBy,
        EffectiveRuleSet ruleSet, IReadOnlyList<RuleOutcome> outcomes, Severity overall, Capabilities capabilities,
        IReadOnlyList<ApprovalRequirement> approvalRoute, IReadOnlyList<PostingPreviewLine> postingPreview, PostingCheck? postingCheck)
    {
        Id = Guid.NewGuid();
        TransactionRef = subject.Transaction.TransactionRef;
        InvoiceId = subject.Transaction.InvoiceId;
        ContentVersion = subject.Transaction.ContentVersion;
        ApprovalCycleId = subject.Transaction.ApprovalCycleId;
        Trigger = trigger;
        EvaluatedAt = evaluatedAt;
        EvaluatedBy = evaluatedBy;
        EngineVersion = ruleSet.EngineVersion;
        AppliedRules = ruleSet.AppliedRules;
        RuleSetFingerprint = ruleSet.Fingerprint;
        Outcomes = outcomes;
        Overall = overall;
        Capabilities = capabilities;
        ApprovalRoute = approvalRoute;
        PostingPreview = postingPreview;
        PostingCheck = postingCheck;
        InputSnapshot = subject;
    }

    private EvaluationRecord()
    {
        TransactionRef = null!;
        EngineVersion = null!;
        AppliedRules = [];
        RuleSetFingerprint = null!;
        Outcomes = [];
        Capabilities = null!;
        ApprovalRoute = [];
        PostingPreview = [];
        InputSnapshot = null!;
    }
}
```

`DomainServices/RuleCatalog.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices.Rules;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Все известные коду правила. RuleDefinition шагов 1–6 без реализации здесь — ошибка конфигурации.</summary>
public sealed class RuleCatalog
{
    private readonly IReadOnlyDictionary<string, IValidationRule> _byId;

    public RuleCatalog(IEnumerable<IValidationRule> rules)
    {
        _byId = rules.ToDictionary(r => r.RuleId);
    }

    public static readonly RuleCatalog Default = new(
    [
        new SegRequiredRule(), new SegGrantForbiddenRule(), new CoaCombinationActiveRule(),
        new FundDeptObjectAllowedRule(), new GrantEligibleRule(), new VendorEligibleRule(),
        new ProcurementThresholdRule(), new InvoiceDuplicateRule(), new BudgetAvailabilityRule(),
        new BudgetLowRemainingRule(), new PoLiquidationRule(),
    ]);

    public IValidationRule? Find(string ruleId) => _byId.GetValueOrDefault(ruleId);
}
```

`DomainServices/RuleSetGuard.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Проверка набора определений при seed / сохранении: реализация существует; адаптируемое правило не ослаблено локальным слоем.</summary>
public static class RuleSetGuard
{
    public static IReadOnlyList<string> FindViolations(IReadOnlyList<RuleDefinition> definitions, RuleCatalog catalog)
    {
        var violations = new List<string>();
        foreach (var group in definitions.GroupBy(d => d.RuleId))
        {
            var sample = group.First();
            if (sample.Step <= ValidationStep.EncumbranceImpact && catalog.Find(group.Key) is null)
            {
                violations.Add($"{group.Key}: configured for step {(int)sample.Step} but has no implementation.");
                continue;
            }

            var latestPerLayer = group.GroupBy(d => d.Layer)
                .Select(l => l.OrderByDescending(d => d.Version).First())
                .OrderBy(d => d.Layer).ToList();
            if (!latestPerLayer[0].IsLocallyAdjustable || catalog.Find(group.Key) is not { } rule)
            {
                continue;
            }

            for (var i = 1; i < latestPerLayer.Count; i++)
            {
                if (rule.IsWeakening(latestPerLayer[i - 1], latestPerLayer[i]))
                {
                    violations.Add($"{group.Key}: {latestPerLayer[i].Layer} v{latestPerLayer[i].Version} weakens {latestPerLayer[i - 1].Layer} v{latestPerLayer[i - 1].Version}.");
                }
            }
        }

        return violations;
    }
}
```

`APPROVAL_ROUTE` (шаг 7) реализации в каталоге не требует — его параметры читает `ApprovalRouting`; ослабление порога FinanceDirector для демо не проверяется (записать в Self-review как известное упрощение).

`DomainServices/ValidationPipeline.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>
/// Восемь шагов над снимками. Шаги 1–6 — правила из каталога; Hard Stop прерывает.
/// Шаг 7 — маршрут (всегда). Шаг 8 — posting check (только при trigger = Post).
/// Чистая функция: ни портов, ни часов, ни случайности, кроме Guid записи.
/// </summary>
public sealed class ValidationPipeline(RuleCatalog catalog)
{
    private static readonly ValidationStep[] RuleSteps =
    [
        ValidationStep.RequiredSegments, ValidationStep.ValidCombination, ValidationStep.FundAndGrantRestrictions,
        ValidationStep.TransactionPurpose, ValidationStep.BudgetAvailability, ValidationStep.EncumbranceImpact,
    ];

    private readonly RuleCatalog _catalog = catalog;

    public EvaluationRecord Evaluate(ValidationSubject subject, EffectiveRuleSet ruleSet, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt)
    {
        var raw = new List<RuleOutcome>();
        foreach (var step in RuleSteps)
        {
            foreach (var definition in ruleSet.ForStep(step))
            {
                var rule = _catalog.Find(definition.RuleId)
                    ?? throw new ValidationException($"Rule {definition.RuleId} is configured but has no implementation.");
                raw.AddRange(rule.Evaluate(subject, definition));
            }

            if (raw.Any(o => o.Severity == Severity.HardStop))
            {
                break;
            }
        }

        var (outcomes, overall) = OutcomeAggregation.Apply(raw, subject.ActiveOverrides);
        var route = ApprovalRouting.Build(subject, outcomes, ruleSet.Find("APPROVAL_ROUTE"));
        var preview = overall <= Severity.SoftStop ? PostingPreviewComposer.Compose(subject) : [];
        var postingCheck = trigger == EvaluationTrigger.Post
            ? PostingEligibility.Check(subject, overall, route, preview, ruleSet.Fingerprint)
            : null;

        return new EvaluationRecord(subject, trigger, evaluatedAt, evaluatedBy, ruleSet, outcomes, overall,
            Capabilities.For(overall, postingCheck?.Passed), route, preview, postingCheck);
    }
}
```

`Repositories/IEvaluationRecordRepository.cs`:
```csharp
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.Repositories;

public interface IEvaluationRecordRepository
{
    Task AddAsync(EvaluationRecord record, CancellationToken ct = default);
    Task<EvaluationRecord?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<EvaluationRecord>> ListByTransactionAsync(string transactionRef, CancellationToken ct = default);
}
```

В `tests/GovErp.Architecture.Tests/ArchitectureFixture.cs` добавить:
```csharp
public static readonly Assembly Validation = typeof(Domain.Validation.DomainServices.ValidationPipeline).Assembly;
public static IEnumerable<Assembly> DomainContexts => [ChartOfAccounts, Ledger, Payables, Validation];
public static IEnumerable<Assembly> AllDomain => [Shared, ChartOfAccounts, Ledger, Payables, Validation];
```

- [ ] **Step 5: Прогнать всё**

Run: `dotnet test`
Expected: Validation.Tests — 100 passed (81 + 4 + 15); Architecture.Tests — зелёные (`ValidationPipeline._catalog` и `RuleCatalog._byId` — `readonly`, поэтому `Domain_services_have_no_mutable_instance_fields` проходит).

- [ ] **Step 6: Commit и push**

```bash
git add -A
git commit -m "Validation: evaluation record with fingerprint, rule catalog and guard, pipeline scenarios

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

## Self-review

**Покрытие спеки 3.5 / 4.1–4.3:** снимки, бюджетный ключ, собственный резерв, PO-строки — задача 1; `RuleDefinition` с адаптируемостью, разрешение слоёв, fingerprint — задача 2; `RuleOutcome`, привязанные overrides, `Capabilities` — задача 3; 11 правил — задачи 4–5; маршрут по департаментам, posting preview, eligibility с базовой линией — задача 6; `EvaluationRecord`, каталог, guard ослабления, конвейер и сценарии spec §7 — задача 7.

**Решения, фиксируемые планом:** (1) расчёт `RequiredNewBudget` / `ProjectedAvailable` — чистые методы `ValidationSubject`, а не поля, заполняемые сборщиком: одна формула, проверяемая юнит-тестами; значения всё равно сохраняются в `Inputs`/`Computed` outcome'ов; (2) бюджетные outcome'ы привязаны к первой строке ключа — к ней же привязывается override; (3) `PO_LIQUIDATION` выдаёт информационный outcome `Allowed`, чтобы ликвидация была видна в Results и аудите; (4) ослабление `APPROVAL_ROUTE.finance_director_threshold` не проверяется — известное упрощение демо.

**Согласованность имён:** `Capabilities` — 4 флага (без оплаты); `PostingEligibility.Check(..., string currentFingerprint)`; `ApprovalBaseline(ContentVersion, RuleSetFingerprint)` формирует план 3 из оценки, на которую ссылается последнее согласование активного цикла; `PostingPreviewLine.Family` — строки `PostingPreviewComposer.Financial` / `.Budgetary`, план 3 маппит их в `LedgerFamily`.
