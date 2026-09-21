# План 2: Validation — конвейер, правила, result model

> Перед реализацией прочитать [обязательные уточнения согласованности](2026-09-22-plan-consistency.md). Они исправляют даты, резервирование, транзакции и безопасность в ранних фрагментах ниже.

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Реализовать контекст `GovErp.Domain.Validation`: чистый конвейер из восьми шагов над снимками, одиннадцать типизированных правил с параметрами из `RuleDefinition`, слоистое разрешение конфликтов, result model (`EvaluationRecord`, `Capabilities`), маршрут согласования, posting preview и posting eligibility — всё проверяемо на голых числах без базы.

**Architecture:** Вход конвейера — `ValidationSubject` (value object из снимков, собирается слоем сценариев в плане 3). Правила — классы `IValidationRule`, читающие параметры из `RuleDefinition`; в БД лежат только включённость, версия, слой, severity и параметры. Конфликты: внутри шага — строжайший; Hard Stop на шагах 1–6 прерывает; одинаковый `RuleId` в разных слоях — побеждает более специфичный. Override меняет агрегацию, не outcome. Выход — неизменяемый `EvaluationRecord`.

**Tech Stack:** как в плане 1. Ни одного NuGet-пакета в `GovErp.Domain.Validation`.

**Spec:** `docs/superpowers/specs/2026-09-21-validation-engine-design.md` (разделы 3.5, 4.1–4.3, 7). Манифест: `GE-9`, `GE-10`, `GE-11`, `GE-12`.

**Зависит от:** план 1 (Shared kernel: `Money`, `AccountCode`, `FiscalYear`, `UserId`; проект `GovErp.Domain.Validation` уже существует и ссылается на `GovErp.Domain.Shared`).

## Global Constraints

- `TransactionVersion` означает ContentVersion, а не SQL RowVersion. Оценка сохраняет ApprovalCycleId и полный fingerprint применимых правил; старые согласования другого цикла не удовлетворяют маршрут.
- PO snapshot содержит AuthorizedPoAmount, AlreadyPostedAgainstPo, OtherActiveInvoiceClaims, CurrentInvoicePoAmount и вычисляемое decimal CumulativePoExcessPct = max(0, AlreadyPostedAgainstPo + OtherActiveInvoiceClaims + CurrentInvoicePoAmount - AuthorizedPoAmount) / AuthorizedPoAmount. Нулевая AuthorizedPoAmount делает PO недопустимым до расчёта. Допуск проверяется накопительно относительно AuthorizedPoAmount, а liquidation отдельно ограничена свободным encumbrance. Claims полной суммы и ликвидируемой части не смешивать.
- Payment eligibility здесь означает только ReadyForPaymentHandoff: Posted, VendorActive, нет PaymentHold, DueDate <= явно переданной BusinessDate. Не создавать хранимый статус Payable.

- Всё из плана 1.
- `GovErp.Domain.Validation` не ссылается на другие контексты. Всё, что нужно от ChartOfAccounts/Ledger/Payables, приходит в снимках со **своими** перечислениями Validation (`FundKind`, `BudgetControl`, `GrantRule`, `FundRestriction`, `GrantEligibilityResult`, `ApproverRole`). Маппинг — в плане 3.
- Порядок шагов фиксирован: `ValidationStep` 1..8. Конфигурируются включённость, severity и параметры.
- Денежные значения в `Inputs`/`Computed` outcome'ов — строки в инвариантной культуре (`Money.ToString()`), чтобы JSON-колонки читались человеком.
- Идентификаторы правил — `UPPER_SNAKE_CASE`, ровно как в спеке 4.2.

---

## Структура файлов

```
src/GovErp.Domain.Validation/
  ValueObjects/
    Severity.cs, RuleLayer.cs, ValidationStep.cs, EvaluationTrigger.cs, ApproverRole.cs
    FundKind.cs, BudgetControl.cs, GrantRule.cs, FundRestriction.cs, GrantEligibilityResult.cs
    VendorSnapshot.cs, TransactionSnapshot.cs, CombinationSnapshot.cs, FundSnapshot.cs,
    GrantSnapshot.cs, BudgetSnapshot.cs, EncumbranceSnapshot.cs, DistributionSnapshot.cs,
    OverrideSnapshot.cs, PostingAccounts.cs, ValidationSubject.cs
    RuleSetVersions.cs, RuleOutcome.cs, Capabilities.cs, ApprovalRequirement.cs,
    PostingPreviewLine.cs, PostingCheck.cs, EffectiveRuleSet.cs
  Entities/
    RuleDefinition.cs, EvaluationRecord.cs
  DomainServices/
    IValidationRule.cs, RuleCatalog.cs, RuleResolution.cs, OutcomeAggregation.cs,
    ApprovalRouting.cs, PostingPreviewComposer.cs, PostingEligibility.cs, ValidationPipeline.cs
    Rules/
      SegRequiredRule.cs, SegGrantForbiddenRule.cs, CoaCombinationActiveRule.cs,
      FundDeptObjectAllowedRule.cs, GrantEligibleRule.cs, VendorEligibleRule.cs,
      ProcurementThresholdRule.cs, InvoiceDuplicateRule.cs, BudgetAvailabilityRule.cs,
      BudgetLowRemainingRule.cs, PoLiquidationRule.cs
  Repositories/
    IRuleDefinitionRepository.cs, IEvaluationRecordRepository.cs
  Exceptions/
    ValidationException.cs
tests/GovErp.Domain.Validation.Tests/
  Support/SubjectBuilder.cs, Support/DemoRules.cs
  RuleResolutionTests.cs, OutcomeAggregationTests.cs, CapabilitiesTests.cs
  Rules/*.cs (по одному файлу на правило)
  ApprovalRoutingTests.cs, PostingPreviewTests.cs, PostingEligibilityTests.cs
  PipelineScenarioTests.cs
```

---

### Task 1: Перечисления и снимки

**Files:**
- Create: `src/GovErp.Domain.Validation/ValueObjects/*.cs` (перечисления и снимки, см. ниже)
- Create: `src/GovErp.Domain.Validation/Exceptions/ValidationException.cs`
- Create: `tests/GovErp.Domain.Validation.Tests/` (проект), `Support/SubjectBuilder.cs`
- Test: `tests/GovErp.Domain.Validation.Tests/DistributionSnapshotTests.cs`

**Interfaces:**
- Produces: все снимки и `ValidationSubject`; `DistributionSnapshot.LiquidationAmount`, `.Excess`, `.AmountToCheck`. Тестовый `SubjectBuilder` — им пользуются все дальнейшие тесты.

- [ ] **Step 1: Создать тестовый проект**

```powershell
dotnet new xunit -n GovErp.Domain.Validation.Tests -o tests/GovErp.Domain.Validation.Tests
Remove-Item tests/GovErp.Domain.Validation.Tests/UnitTest1.cs
dotnet sln add tests/GovErp.Domain.Validation.Tests
dotnet add tests/GovErp.Domain.Validation.Tests reference src/GovErp.Domain.Validation
dotnet add tests/GovErp.Domain.Validation.Tests reference src/GovErp.Domain.Shared
```
Заменить csproj на CPM-вариант (как в плане 1, задача 1, шаг 2).

- [ ] **Step 2: Тест на вычисляемые свойства снимка**

`tests/GovErp.Domain.Validation.Tests/DistributionSnapshotTests.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class DistributionSnapshotTests
{
    [Fact]
    public void Non_po_distribution_checks_full_amount()
    {
        var d = SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 160_000m);
        d.LiquidationAmount.Should().Be(Money.Zero);
        d.Excess.Should().Be(Money.Of(160_000m));
        d.AmountToCheck.Should().Be(Money.Of(160_000m));
    }

    [Fact]
    public void Po_backed_within_remaining_checks_nothing()
    {
        var d = SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 160_000m,
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), IsOpen: true));
        d.LiquidationAmount.Should().Be(Money.Of(160_000m));
        d.Excess.Should().Be(Money.Zero);
        d.AmountToCheck.Should().Be(Money.Zero);
    }

    [Fact]
    public void Po_backed_over_remaining_checks_excess()
    {
        var d = SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 100_000m,
            encumbrance: new EncumbranceSnapshot("PO-1/1", Money.Of(96_000m), IsOpen: true));
        d.LiquidationAmount.Should().Be(Money.Of(96_000m));
        d.Excess.Should().Be(Money.Of(4_000m));
        d.AmountToCheck.Should().Be(Money.Of(4_000m));
    }

    [Fact]
    public void Closed_encumbrance_liquidates_nothing()
    {
        var d = SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 10m,
            encumbrance: new EncumbranceSnapshot("PO-1/1", Money.Zero, IsOpen: false));
        d.AmountToCheck.Should().Be(Money.Of(10m));
    }
}
```

- [ ] **Step 3: Реализация перечислений**

`namespace GovErp.Domain.Validation.ValueObjects;`, по файлу на тип:
```csharp
public enum Severity { Allowed = 0, Warning = 1, SoftStop = 2, HardStop = 3 }
public enum RuleLayer { Core = 0, Federal = 1, State = 2, Tenant = 3 }   // больше = специфичнее
public enum ValidationStep
{
    RequiredSegments = 1, ValidCombination = 2, FundAndGrantRestrictions = 3, TransactionPurpose = 4,
    BudgetAvailability = 5, EncumbranceImpact = 6, ApprovalRequirements = 7, PostingEligibility = 8
}
public enum EvaluationTrigger { Manual, Submit, Approve, Post }
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

- [ ] **Step 4: Реализация снимков**

```csharp
// VendorSnapshot.cs
public sealed record VendorSnapshot(Guid VendorId, string Name, bool IsDebarred, bool SamRegistered);

// TransactionSnapshot.cs
/// <summary>Снимок документа. IsDuplicate вычисляет слой сценариев (запрос к Payables).</summary>
public sealed record TransactionSnapshot(
    string TransactionRef, int TransactionVersion, string TransactionType, DateOnly Date, Money Total,
    VendorSnapshot Vendor, bool IsPoBacked, bool IsDuplicate, UserId CreatedBy);

// CombinationSnapshot.cs
public sealed record CombinationSnapshot(bool Exists, bool IsActiveOnDate, string Status);

// FundSnapshot.cs
public sealed record FundSnapshot(
    string Code, string Name, FundKind Kind, BudgetControl Control, GrantRule GrantRule,
    FundRestriction Restriction, bool IsActive);

// GrantSnapshot.cs
public sealed record GrantSnapshot(string Code, bool IsFederal, GrantEligibilityResult Eligibility, string Status);

// BudgetSnapshot.cs
public sealed record BudgetSnapshot(bool Exists, Money Amended, Money Actuals, Money Encumbered, Money Held, Money Available)
{
    public static readonly BudgetSnapshot Missing = new(false, Money.Zero, Money.Zero, Money.Zero, Money.Zero, Money.Zero);
}

// EncumbranceSnapshot.cs
public sealed record EncumbranceSnapshot(string PoLineRef, Money Remaining, bool IsOpen);

// OverrideSnapshot.cs
public sealed record OverrideSnapshot(string RuleId, UserId UserId, string Reason);

// PostingAccounts.cs
/// <summary>Object-коды и «балансовый» департамент для строк AP / резерва / encumbrance в posting preview.</summary>
public sealed record PostingAccounts(ObjectCode AccountsPayable, ObjectCode ReserveForEncumbrances,
    ObjectCode Encumbrances, DepartmentCode BalanceSheetDepartment);

// DistributionSnapshot.cs
public sealed record DistributionSnapshot(
    int LineNo, AccountCode Account, Money Amount,
    CombinationSnapshot Combination, FundSnapshot? Fund, GrantSnapshot? Grant,
    BudgetSnapshot Budget, EncumbranceSnapshot? Encumbrance)
{
    /// <summary>Сколько уйдёт из encumbrance при Post.</summary>
    public Money LiquidationAmount =>
        Encumbrance is { IsOpen: true } e ? Money.Min(Amount, e.Remaining) : Money.Zero;

    /// <summary>Часть суммы сверх остатка PO (или вся сумма для non-PO).</summary>
    public Money Excess => Amount - LiquidationAmount;

    /// <summary>Что проверяется против available budget.</summary>
    public Money AmountToCheck => Excess;
}

// ValidationSubject.cs
public sealed record ValidationSubject(
    TransactionSnapshot Transaction,
    IReadOnlyList<DistributionSnapshot> Distributions,
    IReadOnlyList<ApproverRole> ApprovalsSoFar,
    IReadOnlyList<OverrideSnapshot> OverridesSoFar,
    bool PeriodIsOpen,
    RuleSetVersions? VersionsAtLastApproval,
    PostingAccounts PostingAccounts);
```

`RuleSetVersions` определяется в задаче 2; для компиляции этой задачи добавить его сразу:
```csharp
// RuleSetVersions.cs
/// <summary>Версии слоёв правил, применённые в оценке. Engine — версия кода конвейера.</summary>
public sealed record RuleSetVersions(string Engine, int Core, int Federal, int State, int Tenant);
```

- [ ] **Step 5: Тестовый SubjectBuilder**

`tests/GovErp.Domain.Validation.Tests/Support/SubjectBuilder.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

/// <summary>Собирает ValidationSubject для тестов. По умолчанию — сценарий задания: non-PO $160,000 на 701-6000-53100-G-COPS-26.</summary>
public sealed class SubjectBuilder
{
    public static readonly UserId Clerk = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    public static readonly DateOnly Sep15 = new(2026, 9, 15);

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

    public static BudgetSnapshot Budget(decimal amended, decimal actuals, decimal encumbered, decimal held = 0) =>
        new(true, Money.Of(amended), Money.Of(actuals), Money.Of(encumbered), Money.Of(held),
            Money.Of(amended - actuals - encumbered - held));

    /// <summary>375,000 − 132,000 − 96,000 = 147,000.</summary>
    public static BudgetSnapshot ExerciseBudget() => Budget(375_000m, 132_000m, 96_000m);

    public static DistributionSnapshot Distribution(int lineNo, string account, decimal amount,
        FundSnapshot? fund = null, GrantSnapshot? grant = null, BudgetSnapshot? budget = null,
        CombinationSnapshot? combination = null, EncumbranceSnapshot? encumbrance = null)
    {
        var code = AccountCode.Parse(account);
        fund ??= code.Fund.Value switch { "701" => Grants701(), "101" => General101(), "202" => Street202(), "501" => Water501(), _ => null };
        grant ??= code.Grant is null ? null : Cops();
        return new DistributionSnapshot(lineNo, code, Money.Of(amount),
            combination ?? new CombinationSnapshot(true, true, "Active"), fund, grant,
            budget ?? ExerciseBudget(), encumbrance);
    }

    private readonly List<DistributionSnapshot> _distributions = [];
    private readonly List<ApproverRole> _approvals = [];
    private readonly List<OverrideSnapshot> _overrides = [];
    private Money? _total;
    private bool _isPoBacked;
    private bool _isDuplicate;
    private bool _periodOpen = true;
    private VendorSnapshot _vendor = new(Guid.NewGuid(), "Acme Consulting", IsDebarred: false, SamRegistered: true);
    private RuleSetVersions? _versionsAtApproval;
    private DateOnly _date = Sep15;

    public SubjectBuilder With(DistributionSnapshot d) { _distributions.Add(d); return this; }
    public SubjectBuilder Total(decimal total) { _total = Money.Of(total); return this; }
    public SubjectBuilder PoBacked() { _isPoBacked = true; return this; }
    public SubjectBuilder Duplicate() { _isDuplicate = true; return this; }
    public SubjectBuilder PeriodClosed() { _periodOpen = false; return this; }
    public SubjectBuilder Vendor(bool debarred = false, bool sam = true) { _vendor = _vendor with { IsDebarred = debarred, SamRegistered = sam }; return this; }
    public SubjectBuilder Approved(params ApproverRole[] roles) { _approvals.AddRange(roles); return this; }
    public SubjectBuilder Overridden(string ruleId, string reason = "justified") { _overrides.Add(new OverrideSnapshot(ruleId, Clerk, reason)); return this; }
    public SubjectBuilder VersionsAtApproval(RuleSetVersions v) { _versionsAtApproval = v; return this; }
    public SubjectBuilder Dated(DateOnly d) { _date = d; return this; }

    public ValidationSubject Build()
    {
        if (_distributions.Count == 0)
        {
            _distributions.Add(Distribution(1, "701-6000-53100-G-COPS-26", 160_000m));
        }

        var total = _total ?? _distributions.Aggregate(Money.Zero, (s, d) => s + d.Amount);
        var tx = new TransactionSnapshot("INV-V-7781", 1, "AP_INVOICE", _date, total, _vendor, _isPoBacked, _isDuplicate, Clerk);
        return new ValidationSubject(tx, _distributions, _approvals, _overrides, _periodOpen, _versionsAtApproval, Accounts);
    }

    /// <summary>Сценарий задания целиком.</summary>
    public static ValidationSubject Exercise() => new SubjectBuilder().Build();
}
```

- [ ] **Step 6: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Validation.Tests`
Expected: 4 passed.

- [ ] **Step 7: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests GovErp.sln
git commit -m "Validation: enumerations, snapshots, ValidationSubject and test builder

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: RuleDefinition, EffectiveRuleSet и разрешение слоёв

**Files:**
- Create: `Entities/RuleDefinition.cs`, `ValueObjects/EffectiveRuleSet.cs`, `DomainServices/RuleResolution.cs`, `Repositories/IRuleDefinitionRepository.cs`
- Test: `tests/.../RuleResolutionTests.cs`, `tests/.../Support/DemoRules.cs`

**Interfaces:**
- Produces: `RuleDefinition` (`RuleId`, `Version`, `Step`, `Layer`, `Severity?` — `null` значит «правило решает само», `Parameters`, `OverridableBy`, `EffectiveFrom/To`, `Message`, `Resolution`, `IsEnabled`, `IsEffectiveOn(DateOnly)`, `Parameter(string) → string`, `DecimalParameter(string)`); `EffectiveRuleSet` (`Definitions`, `Versions`, `ForStep(ValidationStep)`, `Find(string ruleId)`); `RuleResolution.Resolve(IReadOnlyList<RuleDefinition>, DateOnly) → EffectiveRuleSet`.
- `ValidationPipeline.EngineVersion = "engine-1.0.0"` — константа в `RuleResolution`.

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
        DateOnly? from = null, DateOnly? to = null, bool enabled = true) =>
        new(id, version, step, layer, severity, parameters ?? [], overridableBy ?? [], from ?? From, to,
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
            new() { ["threshold"] = "25000" }, [ApproverRole.FinanceDirector]),
        Rule("INVOICE_DUPLICATE", ValidationStep.TransactionPurpose, RuleLayer.Core, Severity.HardStop),
        Rule("BUDGET_AVAILABILITY", ValidationStep.BudgetAvailability, RuleLayer.Core, null,
            overridableBy: [ApproverRole.BudgetOfficer, ApproverRole.FinanceDirector]),
        Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
            new() { ["pct"] = "0.10" }),
        Rule("PO_LIQUIDATION", ValidationStep.EncumbranceImpact, RuleLayer.Core, null,
            new() { ["tolerance_pct"] = "0.05" }),
        Rule("APPROVAL_ROUTE", ValidationStep.ApprovalRequirements, RuleLayer.Tenant, null,
            new() { ["finance_director_threshold"] = "50000" }),
    ];
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
    private static readonly DateOnly Sep15 = new(2026, 9, 15);

    [Fact]
    public void Resolves_all_effective_enabled_rules()
    {
        var set = RuleResolution.Resolve(DemoRules.All(), Sep15);
        set.Definitions.Should().HaveCount(12);
        set.ForStep(ValidationStep.TransactionPurpose).Select(r => r.RuleId)
            .Should().BeEquivalentTo("VENDOR_ELIGIBLE", "PROCUREMENT_THRESHOLD", "INVOICE_DUPLICATE");
    }

    [Fact]
    public void Excludes_disabled_and_out_of_effective_window()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("FUTURE", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, from: new DateOnly(2027, 1, 1)),
            DemoRules.Rule("EXPIRED", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, to: new DateOnly(2026, 6, 30)),
            DemoRules.Rule("OFF", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.Warning, enabled: false),
        };
        var set = RuleResolution.Resolve(rules, Sep15);
        set.Find("FUTURE").Should().BeNull();
        set.Find("EXPIRED").Should().BeNull();
        set.Find("OFF").Should().BeNull();
    }

    [Fact]
    public void Same_rule_id_more_specific_layer_wins()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("PROCUREMENT_THRESHOLD", ValidationStep.TransactionPurpose, RuleLayer.Tenant, Severity.SoftStop,
                new() { ["threshold"] = "10000" }, [ApproverRole.FinanceDirector], version: 3),
        };
        var set = RuleResolution.Resolve(rules, Sep15);
        var rule = set.Find("PROCUREMENT_THRESHOLD")!;
        rule.Layer.Should().Be(RuleLayer.Tenant);
        rule.DecimalParameter("threshold").Should().Be(10_000m);
    }

    [Fact]
    public void Same_rule_id_same_layer_highest_version_wins()
    {
        var rules = new List<RuleDefinition>(DemoRules.All())
        {
            DemoRules.Rule("BUDGET_LOW_REMAINING", ValidationStep.BudgetAvailability, RuleLayer.Tenant, Severity.Warning,
                new() { ["pct"] = "0.20" }, version: 2),
        };
        RuleResolution.Resolve(rules, Sep15).Find("BUDGET_LOW_REMAINING")!.DecimalParameter("pct").Should().Be(0.20m);
    }

    [Fact]
    public void Versions_are_max_per_layer_plus_engine()
    {
        var set = RuleResolution.Resolve(DemoRules.All(), Sep15);
        set.Versions.Engine.Should().Be(RuleResolution.EngineVersion);
        set.Versions.Core.Should().Be(1);
        set.Versions.Tenant.Should().Be(1);
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

/// <summary>Запись о правиле: включённость, версия, слой, severity, параметры, effective dating. Логика — в коде (GE-10).</summary>
public sealed class RuleDefinition
{
    public Guid Id { get; private set; }
    public string RuleId { get; private set; }
    public int Version { get; private set; }
    public ValidationStep Step { get; private set; }
    public RuleLayer Layer { get; private set; }
    /// <summary>null — правило само определяет severity (например, по режиму контроля фонда).</summary>
    public Severity? Severity { get; private set; }
    public IReadOnlyDictionary<string, string> Parameters { get; private set; }
    public IReadOnlyList<ApproverRole> OverridableBy { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public string Message { get; private set; }
    public string Resolution { get; private set; }
    public bool IsEnabled { get; private set; }

    public RuleDefinition(string ruleId, int version, ValidationStep step, RuleLayer layer, Severity? severity,
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
}
```

`ValueObjects/EffectiveRuleSet.cs`:
```csharp
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Набор правил, действующих на дату транзакции, после разрешения слоёв.</summary>
public sealed record EffectiveRuleSet(IReadOnlyList<RuleDefinition> Definitions, RuleSetVersions Versions)
{
    public IReadOnlyList<RuleDefinition> ForStep(ValidationStep step) =>
        Definitions.Where(d => d.Step == step).ToList();

    public RuleDefinition? Find(string ruleId) => Definitions.SingleOrDefault(d => d.RuleId == ruleId);
}
```

`DomainServices/RuleResolution.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Слои: Tenant > State > Federal > Core; внутри слоя — старшая версия. Ослаблять Core/Federal нельзя (проверяется при seed, не здесь).</summary>
public static class RuleResolution
{
    public const string EngineVersion = "engine-1.0.0";

    public static EffectiveRuleSet Resolve(IReadOnlyList<RuleDefinition> candidates, DateOnly onDate)
    {
        var effective = candidates
            .Where(r => r.IsEffectiveOn(onDate))
            .GroupBy(r => r.RuleId)
            .Select(g => g.OrderByDescending(r => r.Layer).ThenByDescending(r => r.Version).First())
            .OrderBy(r => r.Step).ThenBy(r => r.RuleId)
            .ToList();

        int Max(RuleLayer layer) => effective.Where(r => r.Layer == layer).Select(r => r.Version).DefaultIfEmpty(0).Max();

        return new EffectiveRuleSet(effective,
            new RuleSetVersions(EngineVersion, Max(RuleLayer.Core), Max(RuleLayer.Federal), Max(RuleLayer.State), Max(RuleLayer.Tenant)));
    }
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
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Validation.Tests`
Expected: 10 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: RuleDefinition, effective rule set with layer resolution

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: RuleOutcome, агрегация, Capabilities

**Files:**
- Create: `ValueObjects/RuleOutcome.cs`, `ValueObjects/Capabilities.cs`, `DomainServices/OutcomeAggregation.cs`, `DomainServices/IValidationRule.cs`
- Test: `OutcomeAggregationTests.cs`, `CapabilitiesTests.cs`

**Interfaces:**
- Produces: `RuleOutcome` (record: `RuleId`, `RuleVersion`, `Step`, `Layer`, `DistributionLine`, `Severity`, `Inputs`, `Computed`, `Message`, `Resolution`, `OverridableBy`, `OverriddenBy`) + `RuleOutcome.From(RuleDefinition, Severity, int? line, inputs, computed, message?)`; `OutcomeAggregation.Apply(outcomes, overrides) → (IReadOnlyList<RuleOutcome> WithOverrides, Severity Overall)`; `Capabilities.For(Severity overall, bool? postingPassed)`; `IValidationRule { string RuleId; IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject, RuleDefinition); }`.

- [ ] **Step 1: Тесты**

`OutcomeAggregationTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class OutcomeAggregationTests
{
    private static RuleOutcome Outcome(string id, Severity s, params ApproverRole[] overridable) =>
        RuleOutcome.From(DemoRules.Rule(id, ValidationStep.TransactionPurpose, RuleLayer.Core, s, overridableBy: overridable),
            s, null, new Dictionary<string, string>(), new Dictionary<string, string>());

    [Fact]
    public void Overall_is_strictest()
    {
        var (_, overall) = OutcomeAggregation.Apply(
            [Outcome("A", Severity.Warning), Outcome("B", Severity.SoftStop), Outcome("C", Severity.Allowed)], []);
        overall.Should().Be(Severity.SoftStop);
    }

    [Fact]
    public void No_outcomes_means_allowed() =>
        OutcomeAggregation.Apply([], []).Overall.Should().Be(Severity.Allowed);

    [Fact]
    public void Override_removes_soft_stop_from_overall_but_keeps_outcome()
    {
        var overrides = new[] { new OverrideSnapshot("B", SubjectBuilder.Clerk, "ok") };
        var (with, overall) = OutcomeAggregation.Apply(
            [Outcome("A", Severity.Warning), Outcome("B", Severity.SoftStop, ApproverRole.FinanceDirector)], overrides);
        overall.Should().Be(Severity.Warning);
        with.Single(o => o.RuleId == "B").Severity.Should().Be(Severity.SoftStop);
        with.Single(o => o.RuleId == "B").OverriddenBy.Should().NotBeNull();
    }

    [Fact]
    public void Override_does_not_apply_to_hard_stop_or_non_overridable()
    {
        var overrides = new[] { new OverrideSnapshot("H", SubjectBuilder.Clerk, "x"), new OverrideSnapshot("S", SubjectBuilder.Clerk, "y") };
        var (with, overall) = OutcomeAggregation.Apply(
            [Outcome("H", Severity.HardStop, ApproverRole.FinanceDirector), Outcome("S", Severity.SoftStop)], overrides);
        overall.Should().Be(Severity.HardStop);
        with.Should().OnlyContain(o => o.OverriddenBy == null);
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
        Capabilities.For(Severity.HardStop, null).Should().Be(new Capabilities(true, false, false, false, false));

    [Fact]
    public void SoftStop_can_save_and_submit_only() =>
        Capabilities.For(Severity.SoftStop, null).Should().Be(new Capabilities(true, true, false, false, false));

    [Fact]
    public void Warning_and_allowed_can_do_everything_pending_eligibility()
    {
        Capabilities.For(Severity.Warning, null).Should().Be(new Capabilities(true, true, true, true, true));
        Capabilities.For(Severity.Allowed, postingPassed: false).CanPost.Should().BeFalse();
        Capabilities.For(Severity.Allowed, postingPassed: true).CanPost.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

`ValueObjects/RuleOutcome.cs`:
```csharp
using GovErp.Domain.Validation.Entities;

namespace GovErp.Domain.Validation.ValueObjects;

public sealed record RuleOutcome(
    string RuleId, int RuleVersion, ValidationStep Step, RuleLayer Layer, int? DistributionLine,
    Severity Severity,
    IReadOnlyDictionary<string, string> Inputs,
    IReadOnlyDictionary<string, string> Computed,
    string Message, string Resolution,
    IReadOnlyList<ApproverRole> OverridableBy,
    OverrideSnapshot? OverriddenBy)
{
    public static RuleOutcome From(RuleDefinition rule, Severity severity, int? line,
        IReadOnlyDictionary<string, string> inputs, IReadOnlyDictionary<string, string> computed, string? message = null) =>
        new(rule.RuleId, rule.Version, rule.Step, rule.Layer, line, severity, inputs, computed,
            message ?? rule.Message, rule.Resolution, rule.OverridableBy, null);

    public bool IsOverridden => OverriddenBy is not null;
}
```

`ValueObjects/Capabilities.cs`:
```csharp
namespace GovErp.Domain.Validation.ValueObjects;

/// <summary>Матрица «что можно сделать» — функция от Overall (после overrides) и результата posting eligibility.</summary>
public sealed record Capabilities(bool CanSave, bool CanSubmit, bool CanApprove, bool CanPost, bool CanPay)
{
    public static Capabilities For(Severity overall, bool? postingPassed) => overall switch
    {
        Severity.HardStop => new(true, false, false, false, false),
        Severity.SoftStop => new(true, true, false, false, false),
        _ => new(true, true, true, postingPassed ?? true, postingPassed ?? true),
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
}
```

`DomainServices/OutcomeAggregation.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

public static class OutcomeAggregation
{
    /// <summary>Проставляет OverriddenBy на overridable Soft Stop'ы и считает Overall без них.</summary>
    public static (IReadOnlyList<RuleOutcome> WithOverrides, Severity Overall) Apply(
        IReadOnlyList<RuleOutcome> outcomes, IReadOnlyList<OverrideSnapshot> overrides)
    {
        var with = outcomes.Select(o =>
        {
            var ov = overrides.FirstOrDefault(x => x.RuleId == o.RuleId);
            var canOverride = o.Severity == Severity.SoftStop && o.OverridableBy.Count > 0 && ov is not null;
            return canOverride ? o with { OverriddenBy = ov } : o;
        }).ToList();

        var overall = with.Where(o => !o.IsOverridden).Select(o => o.Severity).DefaultIfEmpty(Severity.Allowed).Max();
        return (with, overall);
    }
}
```

- [ ] **Step 4: Прогнать**

Expected: 17 passed.

- [ ] **Step 5: Commit**

```bash
git commit -am "Validation: RuleOutcome, override-aware aggregation, Capabilities

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: Правила шагов 1–4 (сегменты, комбинация, ограничения, purpose)

**Files:**
- Create: `DomainServices/Rules/SegRequiredRule.cs`, `SegGrantForbiddenRule.cs`, `CoaCombinationActiveRule.cs`, `FundDeptObjectAllowedRule.cs`, `GrantEligibleRule.cs`, `VendorEligibleRule.cs`, `ProcurementThresholdRule.cs`, `InvoiceDuplicateRule.cs`
- Test: `tests/.../Rules/Step1To4RulesTests.cs`

**Interfaces:**
- Produces: восемь классов `IValidationRule` с `RuleId`, совпадающим со спекой. Общий помощник `RuleSupport` (внутренний статический класс) для формирования `Inputs`.

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
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100", 1000m, fund: SubjectBuilder.Grants701(), grant: null)).Build();
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
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE")).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Computed["eligibility"] == e.ToString());
    }

    [Fact]
    public void GrantEligible_skips_distributions_without_grant()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m)).Build();
        new GrantEligibleRule().Evaluate(s, Def("GRANT_ELIGIBLE")).Should().BeEmpty();
    }

    [Fact]
    public void VendorEligible_debarred_is_hard_stop_document_level()
    {
        var s = new SubjectBuilder().Vendor(debarred: true).Build();
        new VendorEligibleRule().Evaluate(s, Def("VENDOR_ELIGIBLE")).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.DistributionLine == null);
    }

    [Fact]
    public void VendorEligible_federal_grant_requires_sam()
    {
        var s = new SubjectBuilder().Vendor(sam: false).Build();   // 701 + G-COPS-26 (federal)
        new VendorEligibleRule().Evaluate(s, Def("VENDOR_ELIGIBLE")).Should().ContainSingle(x => x.Message.Contains("SAM"));
        var nonFederal = new SubjectBuilder().Vendor(sam: false).With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m)).Build();
        new VendorEligibleRule().Evaluate(nonFederal, Def("VENDOR_ELIGIBLE")).Should().BeEmpty();
    }

    [Fact]
    public void ProcurementThreshold_non_po_at_or_above_threshold_is_soft_stop()
    {
        var o = new ProcurementThresholdRule().Evaluate(SubjectBuilder.Exercise(), Def("PROCUREMENT_THRESHOLD"));
        o.Should().ContainSingle(x => x.Severity == Severity.SoftStop && x.OverridableBy.Contains(ApproverRole.FinanceDirector));
        o[0].Inputs["threshold"].Should().Be("25,000.00");
    }

    [Fact]
    public void ProcurementThreshold_skips_po_backed_and_small()
    {
        new ProcurementThresholdRule().Evaluate(new SubjectBuilder().PoBacked().Build(), Def("PROCUREMENT_THRESHOLD")).Should().BeEmpty();
        new ProcurementThresholdRule().Evaluate(new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 24_999m)).Build(),
            Def("PROCUREMENT_THRESHOLD")).Should().BeEmpty();
    }

    [Fact]
    public void InvoiceDuplicate_is_hard_stop() =>
        new InvoiceDuplicateRule().Evaluate(new SubjectBuilder().Duplicate().Build(), Def("INVOICE_DUPLICATE"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop);
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

`DomainServices/Rules/RuleSupport.cs` (internal):
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

internal static class RuleSupport
{
    public static Dictionary<string, string> Inputs(DistributionSnapshot d, params (string Key, string Value)[] extra)
    {
        var map = new Dictionary<string, string>
        {
            ["line"] = d.LineNo.ToString(),
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
}
```

`SegRequiredRule.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

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
public sealed class CoaCombinationActiveRule : IValidationRule
{
    public string RuleId => "COA_COMBINATION_ACTIVE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => !d.Combination.Exists || !d.Combination.IsActiveOnDate)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("exists", d.Combination.Exists.ToString()), ("status", d.Combination.Status), ("date", subject.Transaction.Date.ToString("yyyy-MM-dd"))),
                RuleSupport.Map(),
                d.Combination.Exists
                    ? $"Account combination {d.Account} is {d.Combination.Status} on {subject.Transaction.Date:yyyy-MM-dd} (line {d.LineNo})."
                    : $"Account combination {d.Account} does not exist in the chart of accounts (line {d.LineNo})."))
            .ToList();
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
public sealed class GrantEligibleRule : IValidationRule
{
    public string RuleId => "GRANT_ELIGIBLE";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition) =>
        subject.Distributions
            .Where(d => d.Grant is not null && d.Grant.Eligibility != GrantEligibilityResult.Eligible)
            .Select(d => RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, d.LineNo,
                RuleSupport.Inputs(d, ("grant", d.Grant!.Code), ("grantStatus", d.Grant.Status), ("date", subject.Transaction.Date.ToString("yyyy-MM-dd"))),
                RuleSupport.Map(("eligibility", d.Grant.Eligibility.ToString())),
                d.Grant.Eligibility switch
                {
                    GrantEligibilityResult.GrantNotActive => $"Grant {d.Grant.Code} is {d.Grant.Status} (line {d.LineNo}).",
                    GrantEligibilityResult.OutsidePeriod => $"Transaction date {subject.Transaction.Date:yyyy-MM-dd} is outside the period of grant {d.Grant.Code} (line {d.LineNo}).",
                    GrantEligibilityResult.DepartmentNotAllowed => $"Department {d.Account.Department} is not covered by grant {d.Grant.Code} (line {d.LineNo}).",
                    _ => $"Object {d.Account.Object} is not an allowable cost under grant {d.Grant.Code} (line {d.LineNo}).",
                }))
            .ToList();
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
        var inputs = RuleSupport.Map(("vendor", v.Name), ("debarred", v.IsDebarred.ToString()), ("samRegistered", v.SamRegistered.ToString()));
        if (v.IsDebarred)
        {
            return [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null, inputs, RuleSupport.Map(),
                $"Vendor {v.Name} is debarred from government contracts.")];
        }

        var touchesFederalGrant = subject.Distributions.Any(d => d.Grant is { IsFederal: true });
        if (touchesFederalGrant && !v.SamRegistered)
        {
            return [RuleOutcome.From(definition, definition.Severity ?? Severity.HardStop, null, inputs, RuleSupport.Map(("federalGrant", "true")),
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

Во всех файлах правил — `using GovErp.Domain.Validation.Entities; using GovErp.Domain.Validation.ValueObjects; namespace GovErp.Domain.Validation.DomainServices.Rules;`.

- [ ] **Step 4: Прогнать**

Expected: 33 passed.

- [ ] **Step 5: Commit**

```bash
git add -A src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: rules for steps 1-4 (segments, combination, restrictions, purpose)

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: Правила шагов 5–6 (бюджет, encumbrance)

**Files:**
- Create: `DomainServices/Rules/BudgetAvailabilityRule.cs`, `BudgetLowRemainingRule.cs`, `PoLiquidationRule.cs`
- Test: `tests/.../Rules/BudgetRulesTests.cs`

**Interfaces:**
- Produces: три правила. `BUDGET_AVAILABILITY` — severity по `Fund.Control`; `Computed["available"]`, `Computed["overage"]`, `Computed["availableAfter"]`. `PO_LIQUIDATION` — `Computed["liquidation"]`, `Computed["excess"]`, `Computed["excessPct"]`.

- [ ] **Step 1: Тесты**

`tests/.../Rules/BudgetRulesTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices.Rules;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests.Rules;

public class BudgetRulesTests
{
    private static Entities.RuleDefinition Def(string id) => DemoRules.All().Single(r => r.RuleId == id);

    [Fact]
    public void Exercise_scenario_hard_stop_with_13000_overage()
    {
        var o = new BudgetAvailabilityRule().Evaluate(SubjectBuilder.Exercise(), Def("BUDGET_AVAILABILITY"));
        o.Should().ContainSingle();
        o[0].Severity.Should().Be(Severity.HardStop);
        o[0].OverridableBy.Should().BeEmpty();                     // Hard-фонд: override невозможен
        o[0].Inputs["amended"].Should().Be("375,000.00");
        o[0].Inputs["actuals"].Should().Be("132,000.00");
        o[0].Inputs["encumbered"].Should().Be("96,000.00");
        o[0].Inputs["amountToCheck"].Should().Be("160,000.00");
        o[0].Computed["available"].Should().Be("147,000.00");
        o[0].Computed["overage"].Should().Be("13,000.00");
        o[0].Computed["availableAfter"].Should().Be("-13,000.00");
        o[0].Message.Should().Contain("13,000.00");
    }

    [Fact]
    public void After_amendment_passes()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 160_000m,
            budget: SubjectBuilder.Budget(388_000m, 132_000m, 96_000m))).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY")).Should().BeEmpty();
    }

    [Fact]
    public void Soft_fund_overage_is_soft_stop_overridable()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m,
            budget: SubjectBuilder.Budget(50_000m, 40_000m, 0m))).Build();
        var o = new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"));
        o.Should().ContainSingle(x => x.Severity == Severity.SoftStop);
        o[0].OverridableBy.Should().Contain(ApproverRole.BudgetOfficer);
        o[0].Computed["overage"].Should().Be("2,000.00");
    }

    [Fact]
    public void Missing_budget_line_is_hard_stop_regardless_of_mode()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 1m, budget: BudgetSnapshot.Missing)).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"))
            .Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Message.Contains("No budget"));
    }

    [Fact]
    public void Po_backed_within_remaining_checks_zero_and_passes()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 160_000m,
            budget: SubjectBuilder.Budget(500_000m, 100_000m, 160_000m),
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY")).Should().BeEmpty();
    }

    [Fact]
    public void Held_reservations_reduce_available()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 100_000m,
            budget: SubjectBuilder.Budget(375_000m, 132_000m, 96_000m, held: 100_000m))).Build();
        new BudgetAvailabilityRule().Evaluate(s, Def("BUDGET_AVAILABILITY"))
            .Should().ContainSingle(x => x.Computed["available"] == "47,000.00");
    }

    [Fact]
    public void LowRemaining_warns_below_10_percent()
    {
        // 25,000 − 5,000 = 20,000 available; после 18,500 остаётся 1,500 = 6% от 25,000 → Warning
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "202-4000-53100", 18_500m,
            budget: SubjectBuilder.Budget(25_000m, 5_000m, 0m))).Build();
        var o = new BudgetLowRemainingRule().Evaluate(s, Def("BUDGET_LOW_REMAINING"));
        o.Should().ContainSingle(x => x.Severity == Severity.Warning);
        o[0].Computed["remainingPct"].Should().Be("6.00");
    }

    [Fact]
    public void LowRemaining_silent_when_over_budget_or_healthy()
    {
        var healthy = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "202-4000-53100", 8_000m, budget: SubjectBuilder.Budget(25_000m, 5_000m, 0m))).Build();
        new BudgetLowRemainingRule().Evaluate(healthy, Def("BUDGET_LOW_REMAINING")).Should().BeEmpty();
        new BudgetLowRemainingRule().Evaluate(SubjectBuilder.Exercise(), Def("BUDGET_LOW_REMAINING")).Should().BeEmpty(); // overage — не Warning, а дело шага BUDGET_AVAILABILITY
    }

    [Fact]
    public void PoLiquidation_reports_within_remaining_as_allowed_info()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 160_000m,
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        var o = new PoLiquidationRule().Evaluate(s, Def("PO_LIQUIDATION"));
        o.Should().ContainSingle(x => x.Severity == Severity.Allowed);
        o[0].Computed["liquidation"].Should().Be("160,000.00");
        o[0].Computed["excess"].Should().Be("0.00");
    }

    [Fact]
    public void PoLiquidation_excess_within_tolerance_is_warning()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 164_000m,
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        var o = new PoLiquidationRule().Evaluate(s, Def("PO_LIQUIDATION"));
        o.Should().ContainSingle(x => x.Severity == Severity.Warning);
        o[0].Computed["excessPct"].Should().Be("2.50");
    }

    [Fact]
    public void PoLiquidation_excess_over_tolerance_is_hard_stop()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 172_800m,
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        new PoLiquidationRule().Evaluate(s, Def("PO_LIQUIDATION")).Should().ContainSingle(x => x.Severity == Severity.HardStop);
    }

    [Fact]
    public void PoLiquidation_missing_or_closed_encumbrance_on_po_line_is_hard_stop()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 1m,
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Zero, false))).Build();
        new PoLiquidationRule().Evaluate(s, Def("PO_LIQUIDATION")).Should().ContainSingle(x => x.Severity == Severity.HardStop && x.Message.Contains("closed"));
    }

    [Fact]
    public void PoLiquidation_skips_non_po() =>
        new PoLiquidationRule().Evaluate(SubjectBuilder.Exercise(), Def("PO_LIQUIDATION")).Should().BeEmpty();
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

`BudgetAvailabilityRule.cs`:
```csharp
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices.Rules;

/// <summary>Шаг 5. Severity — по режиму контроля фонда: Hard → HardStop (override невозможен), Soft → SoftStop.</summary>
public sealed class BudgetAvailabilityRule : IValidationRule
{
    public string RuleId => "BUDGET_AVAILABILITY";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var outcomes = new List<RuleOutcome>();
        foreach (var d in subject.Distributions)
        {
            var b = d.Budget;
            var inputs = RuleSupport.Inputs(d,
                ("amended", b.Amended.ToString()), ("actuals", b.Actuals.ToString()), ("encumbered", b.Encumbered.ToString()),
                ("held", b.Held.ToString()), ("amountToCheck", d.AmountToCheck.ToString()),
                ("controlMode", d.Fund?.Control.ToString() ?? "Unknown"));

            if (!b.Exists)
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, d.LineNo, inputs, RuleSupport.Map(),
                    $"No budget line exists for {d.Account} in the current fiscal year (line {d.LineNo}).")
                    with { OverridableBy = [] });
                continue;
            }

            var availableAfter = b.Available - d.AmountToCheck;
            if (!availableAfter.IsNegative)
            {
                continue;
            }

            var overage = -availableAfter;
            var hard = d.Fund is null || d.Fund.Control == BudgetControl.Hard;
            var outcome = RuleOutcome.From(definition, hard ? Severity.HardStop : Severity.SoftStop, d.LineNo, inputs,
                RuleSupport.Map(("available", b.Available.ToString()), ("overage", overage.ToString()), ("availableAfter", availableAfter.ToString())),
                $"Line {d.LineNo} exceeds available budget for {d.Account} by {overage} (available {b.Available}, checked {d.AmountToCheck}).");
            outcomes.Add(hard ? outcome with { OverridableBy = [], Resolution = "Budget amendment, grant-budget revision, or authorized coding change." } : outcome);
        }

        return outcomes;
    }
}
```

`BudgetLowRemainingRule.cs`:
```csharp
using System.Globalization;

public sealed class BudgetLowRemainingRule : IValidationRule
{
    public string RuleId => "BUDGET_LOW_REMAINING";

    public IReadOnlyList<RuleOutcome> Evaluate(ValidationSubject subject, RuleDefinition definition)
    {
        var pct = definition.DecimalParameter("pct");
        var outcomes = new List<RuleOutcome>();
        foreach (var d in subject.Distributions.Where(x => x.Budget.Exists && !x.Budget.Amended.IsZero))
        {
            var after = d.Budget.Available - d.AmountToCheck;
            if (after.IsNegative)
            {
                continue;   // превышение — предмет BUDGET_AVAILABILITY
            }

            var remainingPct = after.Amount / d.Budget.Amended.Amount;
            if (remainingPct >= pct)
            {
                continue;
            }

            outcomes.Add(RuleOutcome.From(definition, definition.Severity ?? Severity.Warning, d.LineNo,
                RuleSupport.Inputs(d, ("amended", d.Budget.Amended.ToString()), ("available", d.Budget.Available.ToString()), ("thresholdPct", (pct * 100).ToString("0.00", CultureInfo.InvariantCulture))),
                RuleSupport.Map(("availableAfter", after.ToString()), ("remainingPct", (remainingPct * 100).ToString("0.00", CultureInfo.InvariantCulture))),
                $"After line {d.LineNo}, only {after} ({remainingPct:P2}) of the {d.Account} budget remains."));
        }

        return outcomes;
    }
}
```

`PoLiquidationRule.cs`:
```csharp
using System.Globalization;

/// <summary>Шаг 6. Отчитывается о ликвидации; излишек в пределах tolerance — Warning (и идёт против available в шаге 5), сверх — Hard Stop.</summary>
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
        foreach (var d in subject.Distributions.Where(x => x.Encumbrance is not null))
        {
            var e = d.Encumbrance!;
            var inputs = RuleSupport.Inputs(d, ("poLine", e.PoLineRef), ("remaining", e.Remaining.ToString()), ("open", e.IsOpen.ToString()), ("tolerancePct", (tolerance * 100).ToString("0.00", CultureInfo.InvariantCulture)));

            if (!e.IsOpen)
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, d.LineNo, inputs, RuleSupport.Map(),
                    $"PO line {e.PoLineRef} is closed; line {d.LineNo} cannot liquidate it."));
                continue;
            }

            var excessPct = e.CumulativePoExcessPct; // вычислено по полной утверждённой PO-сумме, posted и всем billing claims
            var computed = RuleSupport.Map(("liquidation", d.LiquidationAmount.ToString()), ("excess", d.Excess.ToString()),
                ("excessPct", (excessPct * 100).ToString("0.00", CultureInfo.InvariantCulture)));

            if (d.Excess.IsZero)
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.Allowed, d.LineNo, inputs, computed,
                    $"Line {d.LineNo} liquidates {d.LiquidationAmount} of encumbrance {e.PoLineRef}; available budget is unaffected."));
            }
            else if (excessPct <= tolerance)
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.Warning, d.LineNo, inputs, computed,
                    $"Line {d.LineNo} exceeds PO line {e.PoLineRef} by {d.Excess} ({excessPct:P2}); the excess is checked against available budget."));
            }
            else
            {
                outcomes.Add(RuleOutcome.From(definition, Severity.HardStop, d.LineNo, inputs, computed,
                    $"Line {d.LineNo} exceeds PO line {e.PoLineRef} by {d.Excess} ({excessPct:P2}), over the {tolerance:P0} tolerance; a change order is required."));
            }
        }

        return outcomes;
    }
}
```

Замечание: `Severity.Allowed`-outcome от `PO_LIQUIDATION` — единственный «информационный» outcome; он нужен, чтобы в Results и в аудите была видна ликвидация.

- [ ] **Step 4: Прогнать**

Expected: 46 passed.

- [ ] **Step 5: Commit**

```bash
git add -A src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: budget availability, low remaining, PO liquidation rules

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: ApprovalRouting, PostingPreviewComposer, PostingEligibility

**Files:**
- Create: `ValueObjects/ApprovalRequirement.cs`, `PostingPreviewLine.cs`, `PostingCheck.cs`; `DomainServices/ApprovalRouting.cs`, `PostingPreviewComposer.cs`, `PostingEligibility.cs`
- Test: `ApprovalRoutingTests.cs`, `PostingPreviewTests.cs`, `PostingEligibilityTests.cs`

**Interfaces:**
- Produces: `ApprovalRequirement(ApproverRole Role, string? Department, string Reason, bool IsSatisfied)`; `ApprovalRouting.Build(subject, outcomesWithOverrides, RuleDefinition? routeRule) → IReadOnlyList<ApprovalRequirement>`; `PostingPreviewLine(AccountCode Account, string Family, Money Debit, Money Credit, string Description)` где `Family` ∈ `"Financial" | "Budgetary"`; `PostingPreviewComposer.Compose(subject) → IReadOnlyList<PostingPreviewLine>`; `PostingCheck(bool Passed, IReadOnlyList<string> Failures)`; `PostingEligibility.Check(subject, overall, route, preview, currentVersions) → PostingCheck`.

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
    public void Exercise_invoice_routes_to_dept_head_grants_manager_finance_director()
    {
        var route = ApprovalRouting.Build(SubjectBuilder.Exercise(), [], Route);
        route.Select(r => (r.Role, r.Department)).Should().BeEquivalentTo(new[]
        {
            (ApproverRole.DepartmentHead, "6000"), (ApproverRole.GrantsManager, (string?)null), (ApproverRole.FinanceDirector, (string?)null),
        });
    }

    [Fact]
    public void Small_general_fund_invoice_needs_only_department_head()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 4_000m)).Build();
        ApprovalRouting.Build(s, [], Route).Should().ContainSingle(r => r.Role == ApproverRole.DepartmentHead);
    }

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
    public void Unoverridden_soft_stop_adds_its_overrider()
    {
        var soft = RuleOutcome.From(DemoRules.All().Single(r => r.RuleId == "PROCUREMENT_THRESHOLD"), Severity.SoftStop, null,
            new Dictionary<string, string>(), new Dictionary<string, string>());
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "101-6000-53100", 30_000m)).Build();
        var route = ApprovalRouting.Build(s, [soft], Route);
        route.Should().Contain(r => r.Role == ApproverRole.FinanceDirector && r.Reason.Contains("PROCUREMENT_THRESHOLD"));
    }

    [Fact]
    public void Satisfied_flag_reflects_approvals_so_far()
    {
        var s = new SubjectBuilder().Approved(ApproverRole.DepartmentHead).Build();
        var route = ApprovalRouting.Build(s, [], Route);
        route.Single(r => r.Role == ApproverRole.DepartmentHead).IsSatisfied.Should().BeTrue();
        route.Single(r => r.Role == ApproverRole.GrantsManager).IsSatisfied.Should().BeFalse();
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
        lines[0].Description.Should().Contain("Expenditure");
        lines[1].Should().BeEquivalentTo(new { Account = AccountCode.Parse("701-0000-2100"), Family = "Financial", Debit = Money.Zero, Credit = Money.Of(160_000m) });
    }

    [Fact]
    public void Enterprise_fund_uses_expense_wording()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "501-5000-53100", 10_000m)).Build();
        PostingPreviewComposer.Compose(s)[0].Description.Should().Contain("Expense").And.NotContain("Expenditure");
    }

    [Fact]
    public void Po_backed_adds_budgetary_reversal_pair_first()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 160_000m,
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        var lines = PostingPreviewComposer.Compose(s);
        lines.Should().HaveCount(4);
        lines[0].Family.Should().Be("Budgetary");
        lines[0].Account.Should().Be(AccountCode.Parse("701-0000-2900-G-COPS-26"));
        lines[0].Debit.Should().Be(Money.Of(160_000m));
        lines[1].Account.Should().Be(AccountCode.Parse("701-3000-5900-G-COPS-26"));
        lines[1].Credit.Should().Be(Money.Of(160_000m));
        lines[2].Family.Should().Be("Financial");
    }

    [Fact]
    public void Multi_fund_has_one_ap_line_per_fund()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 8_000m))
            .With(SubjectBuilder.Distribution(3, "501-5000-53100", 10_000m)).Build();
        var lines = PostingPreviewComposer.Compose(s);
        lines.Where(l => !l.Credit.IsZero).Select(l => l.Account.Fund.Value).Should().BeEquivalentTo("101", "202", "501");
        PostingPreviewComposer.IsBalancedPerFund(lines).Should().BeTrue();
    }

    [Fact]
    public void Balance_check_detects_unbalanced_fund()
    {
        var lines = new[]
        {
            new PostingPreviewLine(AccountCode.Parse("101-6000-53100"), "Financial", Money.Of(10m), Money.Zero, "x"),
            new PostingPreviewLine(AccountCode.Parse("202-0000-2100"), "Financial", Money.Zero, Money.Of(10m), "x"),
        };
        PostingPreviewComposer.IsBalancedPerFund(lines).Should().BeFalse();
    }
}
```

`PostingEligibilityTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class PostingEligibilityTests
{
    private static readonly RuleSetVersions V1 = new("engine-1.0.0", 1, 1, 1, 1);
    private static Entities.RuleDefinition Route => DemoRules.All().Single(r => r.RuleId == "APPROVAL_ROUTE");

    private static (ValidationSubject, IReadOnlyList<ApprovalRequirement>, IReadOnlyList<PostingPreviewLine>) FullyApproved(Action<SubjectBuilder>? tweak = null)
    {
        var b = new SubjectBuilder().Approved(ApproverRole.DepartmentHead, ApproverRole.GrantsManager, ApproverRole.FinanceDirector).VersionsAtApproval(V1);
        tweak?.Invoke(b);
        var s = b.Build();
        return (s, ApprovalRouting.Build(s, [], Route), PostingPreviewComposer.Compose(s));
    }

    [Fact]
    public void Passes_when_everything_is_in_place()
    {
        var (s, route, preview) = FullyApproved();
        PostingEligibility.Check(s, Severity.Allowed, route, preview, V1).Passed.Should().BeTrue();
    }

    [Fact]
    public void Fails_on_missing_approval()
    {
        var s = new SubjectBuilder().Approved(ApproverRole.DepartmentHead).VersionsAtApproval(V1).Build();
        var check = PostingEligibility.Check(s, Severity.Allowed, ApprovalRouting.Build(s, [], Route), PostingPreviewComposer.Compose(s), V1);
        check.Passed.Should().BeFalse();
        check.Failures.Should().Contain(f => f.Contains("GrantsManager"));
    }

    [Fact]
    public void Fails_on_closed_period()
    {
        var (s, route, preview) = FullyApproved(b => b.PeriodClosed());
        PostingEligibility.Check(s, Severity.Allowed, route, preview, V1).Failures.Should().Contain(f => f.Contains("period"));
    }

    [Fact]
    public void Fails_when_rule_versions_changed_since_approval()
    {
        var (s, route, preview) = FullyApproved();
        var check = PostingEligibility.Check(s, Severity.Allowed, route, preview, V1 with { Tenant = 2 });
        check.Failures.Should().Contain(f => f.Contains("REVALIDATION_REQUIRED"));
    }

    [Fact]
    public void Fails_on_soft_or_hard_overall()
    {
        var (s, route, preview) = FullyApproved();
        PostingEligibility.Check(s, Severity.SoftStop, route, preview, V1).Passed.Should().BeFalse();
        PostingEligibility.Check(s, Severity.Warning, route, preview, V1).Passed.Should().BeTrue();
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
using GovErp.Domain.Validation.Entities;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Шаг 7. DepartmentHead каждого департамента; GrantsManager при гранте; FinanceDirector при total ≥ порога; overrider для каждого не снятого Soft Stop.</summary>
public static class ApprovalRouting
{
    public const decimal DefaultFinanceDirectorThreshold = 50_000m;

    public static IReadOnlyList<ApprovalRequirement> Build(ValidationSubject subject, IReadOnlyList<RuleOutcome> outcomes, RuleDefinition? routeRule)
    {
        var threshold = Money.Of(routeRule?.DecimalParameter("finance_director_threshold") ?? DefaultFinanceDirectorThreshold);
        var approved = subject.ApprovalsSoFar.ToHashSet();
        var route = new List<ApprovalRequirement>();

        foreach (var dept in subject.Distributions.Select(d => d.Account.Department.Value).Distinct())
        {
            route.Add(new ApprovalRequirement(ApproverRole.DepartmentHead, dept, $"Department {dept} is charged.", approved.Contains(ApproverRole.DepartmentHead)));
        }

        if (subject.Distributions.Any(d => d.Account.Grant is not null))
        {
            route.Add(new ApprovalRequirement(ApproverRole.GrantsManager, null, "Grant-funded distribution.", approved.Contains(ApproverRole.GrantsManager)));
        }

        if (subject.Transaction.Total >= threshold)
        {
            route.Add(new ApprovalRequirement(ApproverRole.FinanceDirector, null, $"Total {subject.Transaction.Total} ≥ {threshold}.", approved.Contains(ApproverRole.FinanceDirector)));
        }

        foreach (var soft in outcomes.Where(o => o.Severity == Severity.SoftStop && !o.IsOverridden && o.OverridableBy.Count > 0))
        {
            var role = soft.OverridableBy[0];
            if (route.All(r => r.Role != role))
            {
                route.Add(new ApprovalRequirement(role, null, $"Override required for {soft.RuleId}.", approved.Contains(role)));
            }
        }

        return route;
    }
}
```

Упрощение, зафиксированное здесь: `IsSatisfied` для `DepartmentHead` считается по роли, а не по департаменту — `ApprovalsSoFar` не несёт департамент. Для демо с одним согласующим на роль достаточно; в спеке 2.1 роль `DepartmentHead` одна.

`DomainServices/PostingPreviewComposer.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.DomainServices;

/// <summary>Строит проводки, которые создаст Post. Budgetary-пара сторно encumbrance идёт перед Financial-парой.</summary>
public static class PostingPreviewComposer
{
    public static IReadOnlyList<PostingPreviewLine> Compose(ValidationSubject subject)
    {
        var acc = subject.PostingAccounts;
        var lines = new List<PostingPreviewLine>();

        foreach (var d in subject.Distributions.Where(x => !x.LiquidationAmount.IsZero))
        {
            var reserve = new AccountCode(d.Account.Fund, acc.BalanceSheetDepartment, acc.ReserveForEncumbrances, d.Account.Grant);
            var enc = new AccountCode(d.Account.Fund, d.Account.Department, acc.Encumbrances, d.Account.Grant);
            lines.Add(new PostingPreviewLine(reserve, "Budgetary", d.LiquidationAmount, Money.Zero, $"Reserve for encumbrances — liquidate {d.Encumbrance!.PoLineRef}"));
            lines.Add(new PostingPreviewLine(enc, "Budgetary", Money.Zero, d.LiquidationAmount, $"Encumbrances — liquidate {d.Encumbrance.PoLineRef}"));
        }

        foreach (var d in subject.Distributions)
        {
            var wording = d.Fund?.Kind == FundKind.Enterprise ? "Expense" : "Expenditure";
            lines.Add(new PostingPreviewLine(d.Account, "Financial", d.Amount, Money.Zero, $"{wording} — line {d.LineNo}"));
        }

        foreach (var g in subject.Distributions.GroupBy(d => d.Account.Fund))
        {
            var total = g.Aggregate(Money.Zero, (s, d) => s + d.Amount);
            var ap = new AccountCode(g.Key, acc.BalanceSheetDepartment, acc.AccountsPayable, null);
            lines.Add(new PostingPreviewLine(ap, "Financial", Money.Zero, total, $"Accounts payable — fund {g.Key}"));
        }

        return lines;
    }

    public static bool IsBalancedPerFund(IReadOnlyList<PostingPreviewLine> lines) =>
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
        IReadOnlyList<PostingPreviewLine> preview, RuleSetVersions currentVersions)
    {
        var failures = new List<string>();
        if (overall > Severity.Warning)
        {
            failures.Add($"Overall result is {overall}.");
        }

        foreach (var r in route.Where(r => !r.IsSatisfied))
        {
            failures.Add($"Approval by {r.Role}{(r.Department is null ? "" : $" ({r.Department})")} is missing.");
        }

        if (!subject.PeriodIsOpen)
        {
            failures.Add("The fiscal period for the transaction date is closed.");
        }

        if (!PostingPreviewComposer.IsBalancedPerFund(preview))
        {
            failures.Add("Posting preview is not balanced per fund.");
        }

        if (subject.VersionsAtLastApproval is { } v && v != currentVersions)
        {
            failures.Add($"{RevalidationRequired}: rule versions changed since approval ({v} → {currentVersions}).");
        }

        return failures.Count == 0 ? PostingCheck.Ok : new PostingCheck(false, failures);
    }
}
```

- [ ] **Step 4: Прогнать**

Expected: 61 passed.

- [ ] **Step 5: Commit**

```bash
git add -A src/GovErp.Domain.Validation tests/GovErp.Domain.Validation.Tests
git commit -m "Validation: approval routing, posting preview, posting eligibility

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: EvaluationRecord, RuleCatalog, ValidationPipeline — сквозные сценарии

**Files:**
- Create: `Entities/EvaluationRecord.cs`, `DomainServices/RuleCatalog.cs`, `DomainServices/ValidationPipeline.cs`, `Repositories/IEvaluationRecordRepository.cs`
- Test: `PipelineScenarioTests.cs`
- Modify: `tests/GovErp.Architecture.Tests/ArchitectureFixture.cs` — добавить сборку Validation

**Interfaces:**
- Produces: `ValidationPipeline.Evaluate(ValidationSubject, EffectiveRuleSet, EvaluationTrigger, UserId evaluatedBy, DateTimeOffset at) → EvaluationRecord`; `EvaluationRecord` (все поля спеки 3.5; `Outcomes` уже с overrides; `PostingCheck` заполнен только при `Trigger == Post`); `RuleCatalog.Default` — все 11 правил; `IEvaluationRecordRepository { AddAsync; ListByTransactionAsync(string transactionRef); FindAsync(Guid) }`.

- [ ] **Step 1: Тесты**

`PipelineScenarioTests.cs`:
```csharp
using GovErp.Domain.Validation.DomainServices;
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Tests;

public class PipelineScenarioTests
{
    private static readonly DateTimeOffset At = new(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly EffectiveRuleSet Rules = RuleResolution.Resolve(DemoRules.All(), new DateOnly(2026, 9, 15));
    private static readonly ValidationPipeline Pipeline = new(RuleCatalog.Default);

    private static Entities.EvaluationRecord Run(ValidationSubject s, EvaluationTrigger t = EvaluationTrigger.Manual) =>
        Pipeline.Evaluate(s, Rules, t, SubjectBuilder.Clerk, At);

    [Fact]
    public void Scenario_NonPo_701_ExceedsAvailable_By13000_IsHardStop()
    {
        var r = Run(SubjectBuilder.Exercise());
        r.Overall.Should().Be(Severity.HardStop);
        r.Capabilities.Should().Be(new Capabilities(true, false, false, false, false));
        var budget = r.Outcomes.Single(o => o.RuleId == "BUDGET_AVAILABILITY");
        budget.Computed["overage"].Should().Be("13,000.00");
        r.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD");   // шаг 4 выполнился до шага 5
        r.ApprovalRoute.Should().NotBeEmpty();                                     // маршрут строится даже при стопе
        r.PostingPreview.Should().BeEmpty();                                       // превью — только если ≤ SoftStop
        r.RuleSetVersions.Engine.Should().Be(RuleResolution.EngineVersion);
        r.TransactionRef.Should().Be("INV-V-7781");
        r.Trigger.Should().Be(EvaluationTrigger.Manual);
    }

    [Fact]
    public void Scenario_NonPo_701_AfterAmendment13000_IsSoftStop_ProcurementThreshold()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 160_000m,
            budget: SubjectBuilder.Budget(388_000m, 132_000m, 96_000m))).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.SoftStop);
        r.Outcomes.Should().ContainSingle(o => o.RuleId == "PROCUREMENT_THRESHOLD");
        r.Outcomes.Should().Contain(o => o.RuleId == "BUDGET_LOW_REMAINING");     // после 160k остаётся 0 → Warning
        r.Capabilities.CanSubmit.Should().BeTrue();
        r.Capabilities.CanApprove.Should().BeFalse();
        r.PostingPreview.Should().HaveCount(2);
        r.ApprovalRoute.Select(a => a.Role).Should().Contain(ApproverRole.FinanceDirector);
    }

    [Fact]
    public void Scenario_NonPo_701_AfterAmendment_WithOverride_IsWarning()
    {
        var s = new SubjectBuilder().Overridden("PROCUREMENT_THRESHOLD", "Sole-source justification on file")
            .With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 160_000m, budget: SubjectBuilder.Budget(388_000m, 132_000m, 96_000m))).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.Warning);
        r.Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD").IsOverridden.Should().BeTrue();
        r.Capabilities.CanApprove.Should().BeTrue();
    }

    [Fact]
    public void Scenario_PoBacked_WithinRemaining_IsAllowed_AvailableUnchanged()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 160_000m,
            budget: SubjectBuilder.Budget(500_000m, 100_000m, 160_000m),
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.Allowed);
        r.Outcomes.Should().ContainSingle(o => o.RuleId == "PO_LIQUIDATION" && o.Severity == Severity.Allowed);
        r.Outcomes.Should().NotContain(o => o.RuleId == "BUDGET_AVAILABILITY");
        r.Outcomes.Should().NotContain(o => o.RuleId == "PROCUREMENT_THRESHOLD");
        r.PostingPreview.Should().HaveCount(4);
        r.PostingPreview.Count(l => l.Family == "Budgetary").Should().Be(2);
    }

    [Fact]
    public void Scenario_PoBacked_Excess3Pct_IsWarning()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 164_800m,
            budget: SubjectBuilder.Budget(500_000m, 100_000m, 160_000m),
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.Warning);
        r.Outcomes.Single(o => o.RuleId == "PO_LIQUIDATION").Computed["excess"].Should().Be("4,800.00");
    }

    [Fact]
    public void Scenario_PoBacked_Excess8Pct_IsHardStop()
    {
        var s = new SubjectBuilder().PoBacked().With(SubjectBuilder.Distribution(1, "701-3000-53100-G-COPS-26", 172_800m,
            budget: SubjectBuilder.Budget(500_000m, 100_000m, 160_000m),
            encumbrance: new EncumbranceSnapshot("PO-2026-0451/1", Money.Of(160_000m), true))).Build();
        Run(s).Overall.Should().Be(Severity.HardStop);
    }

    [Fact]
    public void Scenario_MultiFund_101_Overage_IsSoftStop_202_501_Allowed()
    {
        var s = new SubjectBuilder()
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 12_000m, budget: SubjectBuilder.Budget(50_000m, 40_000m, 0m)))
            .With(SubjectBuilder.Distribution(2, "202-4000-53100", 8_000m, budget: SubjectBuilder.Budget(25_000m, 5_000m, 0m)))
            .With(SubjectBuilder.Distribution(3, "501-5000-53100", 10_000m, budget: SubjectBuilder.Budget(60_000m, 30_000m, 0m))).Build();
        var r = Run(s);
        r.Overall.Should().Be(Severity.SoftStop);
        r.Outcomes.Where(o => o.RuleId == "BUDGET_AVAILABILITY").Should().ContainSingle(o => o.DistributionLine == 1 && o.Severity == Severity.SoftStop);
        r.Outcomes.Should().Contain(o => o.RuleId == "PROCUREMENT_THRESHOLD");  // 30,000 ≥ 25,000
        r.PostingPreview.Should().HaveCount(6);
        PostingPreviewComposer.IsBalancedPerFund(r.PostingPreview).Should().BeTrue();
    }

    [Fact]
    public void Scenario_MultiFund_501_UsesExpenseNotExpenditure()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "501-5000-53100", 10_000m, budget: SubjectBuilder.Budget(60_000m, 30_000m, 0m))).Build();
        Run(s).PostingPreview[0].Description.Should().StartWith("Expense");
    }

    [Fact]
    public void Pipeline_HardStopAtStep2_SkipsSteps3To6_StillBuildsRoute()
    {
        var s = new SubjectBuilder().With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 160_000m,
            combination: new CombinationSnapshot(false, false, "Missing"))).Build();
        var r = Run(s);
        r.Outcomes.Select(o => o.Step).Max().Should().Be(ValidationStep.ValidCombination);
        r.Outcomes.Should().NotContain(o => o.RuleId == "BUDGET_AVAILABILITY");
        r.ApprovalRoute.Should().NotBeEmpty();
    }

    [Fact]
    public void Pipeline_Override_ChangesOverall_NotOutcome()
    {
        var s = new SubjectBuilder().Overridden("PROCUREMENT_THRESHOLD")
            .With(SubjectBuilder.Distribution(1, "101-6000-53100", 30_000m, budget: SubjectBuilder.Budget(100_000m, 0m, 0m))).Build();
        var r = Run(s);
        r.Outcomes.Single(o => o.RuleId == "PROCUREMENT_THRESHOLD").Severity.Should().Be(Severity.SoftStop);
        r.Overall.Should().Be(Severity.Allowed);
    }

    [Fact]
    public void Post_trigger_fills_posting_check()
    {
        var v = Rules.Versions;
        var s = new SubjectBuilder().Approved(ApproverRole.DepartmentHead, ApproverRole.GrantsManager, ApproverRole.FinanceDirector).VersionsAtApproval(v)
            .With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 100_000m)).Build();
        var r = Run(s, EvaluationTrigger.Post);
        r.PostingCheck.Should().NotBeNull();
        r.PostingCheck!.Passed.Should().BeTrue();
        r.Capabilities.CanPost.Should().BeTrue();

        var manual = Run(s, EvaluationTrigger.Manual);
        manual.PostingCheck.Should().BeNull();
    }

    [Fact]
    public void PostingEligibility_RuleSetVersionChanged_RequiresRevalidation()
    {
        var stale = Rules.Versions with { Tenant = 0 };
        var s = new SubjectBuilder().Approved(ApproverRole.DepartmentHead, ApproverRole.GrantsManager, ApproverRole.FinanceDirector).VersionsAtApproval(stale)
            .With(SubjectBuilder.Distribution(1, "701-6000-53100-G-COPS-26", 100_000m)).Build();
        var r = Run(s, EvaluationTrigger.Post);
        r.PostingCheck!.Passed.Should().BeFalse();
        r.PostingCheck.Failures.Should().Contain(f => f.Contains(PostingEligibility.RevalidationRequired));
        r.Capabilities.CanPost.Should().BeFalse();
    }

    [Fact]
    public void Record_is_immutable_and_carries_input_snapshot()
    {
        var r = Run(SubjectBuilder.Exercise());
        r.InputSnapshot.Should().BeSameAs(r.InputSnapshot);
        r.InputSnapshot.Distributions.Should().HaveCount(1);
        typeof(Entities.EvaluationRecord).GetProperties().Should().OnlyContain(p => p.SetMethod == null || !p.SetMethod.IsPublic);
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

- [ ] **Step 3: Реализация**

`Entities/EvaluationRecord.cs`:
```csharp
using GovErp.Domain.Validation.ValueObjects;

namespace GovErp.Domain.Validation.Entities;

/// <summary>Неизменяемая запись одной оценки (GE-12). Создаётся только конвейером.</summary>
public sealed class EvaluationRecord
{
    public Guid Id { get; private set; }
    public string TransactionRef { get; private set; }
    public int TransactionVersion { get; private set; }
    public EvaluationTrigger Trigger { get; private set; }
    public DateTimeOffset EvaluatedAt { get; private set; }
    public UserId EvaluatedBy { get; private set; }
    public RuleSetVersions RuleSetVersions { get; private set; }
    public IReadOnlyList<RuleOutcome> Outcomes { get; private set; }
    public Severity Overall { get; private set; }
    public Capabilities Capabilities { get; private set; }
    public IReadOnlyList<ApprovalRequirement> ApprovalRoute { get; private set; }
    public IReadOnlyList<PostingPreviewLine> PostingPreview { get; private set; }
    public PostingCheck? PostingCheck { get; private set; }
    public ValidationSubject InputSnapshot { get; private set; }

    internal EvaluationRecord(string transactionRef, int transactionVersion, EvaluationTrigger trigger, DateTimeOffset evaluatedAt,
        UserId evaluatedBy, RuleSetVersions ruleSetVersions, IReadOnlyList<RuleOutcome> outcomes, Severity overall,
        Capabilities capabilities, IReadOnlyList<ApprovalRequirement> approvalRoute, IReadOnlyList<PostingPreviewLine> postingPreview,
        PostingCheck? postingCheck, ValidationSubject inputSnapshot)
    {
        Id = Guid.NewGuid();
        TransactionRef = transactionRef;
        TransactionVersion = transactionVersion;
        Trigger = trigger;
        EvaluatedAt = evaluatedAt;
        EvaluatedBy = evaluatedBy;
        RuleSetVersions = ruleSetVersions;
        Outcomes = outcomes;
        Overall = overall;
        Capabilities = capabilities;
        ApprovalRoute = approvalRoute;
        PostingPreview = postingPreview;
        PostingCheck = postingCheck;
        InputSnapshot = inputSnapshot;
    }

    private EvaluationRecord()
    {
        TransactionRef = null!;
        RuleSetVersions = null!;
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

/// <summary>Все известные коду правила. RuleDefinition без реализации здесь — ошибка конфигурации.</summary>
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
    public IReadOnlyCollection<string> KnownRuleIds => _byId.Keys.ToList();
}
```

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
public sealed class ValidationPipeline
{
    private readonly RuleCatalog _catalog;

    public ValidationPipeline(RuleCatalog catalog)
    {
        _catalog = catalog;
    }

    public EvaluationRecord Evaluate(ValidationSubject subject, EffectiveRuleSet rules, EvaluationTrigger trigger,
        UserId evaluatedBy, DateTimeOffset evaluatedAt)
    {
        var raw = new List<RuleOutcome>();
        foreach (var step in new[]
                 {
                     ValidationStep.RequiredSegments, ValidationStep.ValidCombination, ValidationStep.FundAndGrantRestrictions,
                     ValidationStep.TransactionPurpose, ValidationStep.BudgetAvailability, ValidationStep.EncumbranceImpact,
                 })
        {
            foreach (var definition in rules.ForStep(step))
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

        var (outcomes, overall) = OutcomeAggregation.Apply(raw, subject.OverridesSoFar);
        var route = ApprovalRouting.Build(subject, outcomes, rules.Find("APPROVAL_ROUTE"));
        var preview = overall <= Severity.SoftStop ? PostingPreviewComposer.Compose(subject) : [];
        var postingCheck = trigger == EvaluationTrigger.Post
            ? PostingEligibility.Check(subject, overall, route, preview, rules.Versions)
            : null;
        var capabilities = Capabilities.For(overall, postingCheck?.Passed);

        return new EvaluationRecord(subject.Transaction.TransactionRef, subject.Transaction.TransactionVersion, trigger,
            evaluatedAt, evaluatedBy, rules.Versions, outcomes, overall, capabilities, route, preview, postingCheck, subject);
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

- [ ] **Step 4: Прогнать всё**

Run: `dotnet test`
Expected: Validation.Tests — 74 passed; Architecture.Tests — зелёные (в т.ч. `Domain_services_have_no_mutable_instance_fields` — `ValidationPipeline._catalog` и `RuleCatalog._byId` — `readonly`).

- [ ] **Step 5: Commit и push**

```bash
git add -A
git commit -m "Validation: EvaluationRecord, rule catalog, pipeline with end-to-end scenarios

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push
```

---

## Self-review

**Покрытие спеки 3.5 / 4.1–4.3:** снимки и `ValidationSubject` — задача 1; `RuleDefinition`, слои, версии — задача 2; `RuleOutcome`, override-агрегация, `Capabilities` — задача 3; 11 правил — задачи 4–5; `ApprovalRouting`, posting preview, `PostingEligibility` — задача 6; `EvaluationRecord`, `RuleCatalog`, `ValidationPipeline`, все `Scenario_*` и `Pipeline_*` тесты из спеки 7 — задача 7. Синтетический `BUDGET_CONCURRENCY` (4.2) — формируется слоем сценариев в плане 3 через `RuleOutcome`-конструктор (все поля публичны в record).

**Отклонения от спеки, фиксируемые планом:** (1) `AmountToCheck` вычисляется в `DistributionSnapshot`, а не в assembler — проще и без дублирования; (2) `IsSatisfied` для `DepartmentHead` — по роли, не по департаменту; (3) `PO_LIQUIDATION` выдаёт информационный outcome `Allowed`.

**Согласованность имён:** `RuleSetVersions` — record с value-равенством, поэтому сравнение `v != currentVersions` в `PostingEligibility` корректно. `Capabilities.For(Severity, bool?)` — используется в `ValidationPipeline` и тестах. `RuleOutcome.From(...)` — 6 параметров, последний опционален. `PostingPreviewLine.Family` — строка, чтобы не заводить enum, дублирующий `LedgerFamily` из Ledger; план 3 маппит `"Budgetary"` → `LedgerFamily.Budgetary`.
