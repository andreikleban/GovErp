# План 1: Foundation + Domain (Shared, ChartOfAccounts, Ledger, Payables)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать решение с проверяемыми границами слоёв и реализовать три доменных контекста (ChartOfAccounts, Ledger, Payables) поверх общего ядра значений — с тестами всех инвариантов, без базы и без UI.

**Architecture:** Каждый слой и каждый контекст — отдельный проект; транзитивные ссылки отключены, граф ссылок проверяется NetArchTest. Домены чистые (только BCL + `GovErp.Domain.Shared`). Агрегаты — классы с закрытыми сеттерами и методами-переходами; значения — `record`/`record struct` с самопроверкой в конструкторе. Репозитории — только интерфейсы в `Repositories/`.

**Tech Stack:** .NET 10 (SDK 10.0.401), C# 14, xUnit 2.9, FluentAssertions 7, NetArchTest.Rules 1.3, Central Package Management.

**Spec:** `docs/superpowers/specs/2026-09-21-validation-engine-design.md` (разделы 3.1–3.4, 6.1, 7). Манифест: `docs/ARCHITECTURE.md`.

**Следующие планы:** План 2 — контекст Validation (конвейер, правила). План 3 — Infrastructure + Application + Docker. План 4 — Blazor UI + Explanation.

## Global Constraints

- `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `DisableTransitiveProjectReferences=true` — в `Directory.Build.props` для всех проектов.
- Версии пакетов — только в `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`).
- Имена проектов: `GovErp.Domain.<Context>`, `GovErp.Application.Web`, `GovErp.Infrastructure`, `GovErp.Web`; тесты — `tests/GovErp.<Проект>.Tests` (`NM-1`, `NM-7`).
- Внутри Domain-проекта папки: `Entities/`, `ValueObjects/`, `Repositories/`, `DomainServices/`, `Exceptions/` (`NM-2`). Пространство имён = путь папки, один публичный тип — один файл (`NM-5`).
- Domain-проекты ссылаются только на `GovErp.Domain.Shared` и BCL. Ни одного NuGet-пакета в Domain.
- В слое правил запрещены имена с `Ui`, `View`, `Vm`, `Dto` (`NM-13`); суффикс `Service` — только в `DomainServices/` (`NM-11`); `Handler`, `Manager`, `Store`, `Registry` — нигде (`NM-12`).
- Деньги — `Money` (decimal, 2 знака); суммы distribution/бюджета/резервов ≥ 0; отрицательные `Money` допустимы только как дельты и результаты вычислений.
- Финансовый год: с 1 июля; `FY2026` = 2025-07-01 … 2026-06-30.
- Коммит после каждой задачи; сообщения на английском, в конце — `Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>`.

**Отклонение от спеки, фиксируется этим планом:** сущность `CombinationRule` и `ICombinationRuleRepository` не реализуются — правила «Grant обязателен для 701» и «202 только с 4000» уже выражены атрибутами `Fund` (`GrantPolicy`, `AllowedDepartments`, `AllowedObjects`), а допустимость адреса — whitelist `AccountCombination`. Отдельная таблица правил комбинаций дублировала бы это (`DDD-15`). Задача 1 вносит правку в спеку.

---

## Структура файлов

```
GovErp.sln
Directory.Build.props
Directory.Packages.props
.editorconfig
src/
  GovErp.Domain.Shared/
    GovErp.Domain.Shared.csproj
    ValueObjects/Money.cs
    ValueObjects/SegmentCode.cs          ← абстрактная база для кодов сегментов
    ValueObjects/FundCode.cs
    ValueObjects/DepartmentCode.cs
    ValueObjects/ObjectCode.cs
    ValueObjects/GrantCode.cs
    ValueObjects/AccountCode.cs
    ValueObjects/FiscalYear.cs
    ValueObjects/DatePeriod.cs
    ValueObjects/TenantId.cs
    ValueObjects/UserId.cs
  GovErp.Domain.ChartOfAccounts/
    Entities/Fund.cs, FundType.cs, AccountingBasis.cs, BudgetControlMode.cs, GrantPolicy.cs, FundRestrictionCheck.cs
    Entities/Department.cs
    Entities/ObjectCodeDefinition.cs, ObjectCategory.cs
    Entities/Grant.cs, GrantStatus.cs, GrantEligibility.cs
    Entities/AccountCombination.cs, CombinationStatus.cs, CombinationSource.cs
    Repositories/IFundRepository.cs, IGrantRepository.cs, IAccountCombinationRepository.cs, IReferenceDataRepository.cs
    Exceptions/ChartOfAccountsException.cs
  GovErp.Domain.Ledger/
    Entities/BudgetLine.cs, BudgetAmendment.cs, BudgetReservation.cs, ReservationStatus.cs, ReservationResult.cs
    Entities/Encumbrance.cs, EncumbranceLiquidation.cs, EncumbranceStatus.cs
    Entities/JournalEntry.cs, JournalLine.cs, LedgerFamily.cs
    Entities/FiscalPeriod.cs, PeriodStatus.cs
    Repositories/IBudgetLineRepository.cs, IEncumbranceRepository.cs, IJournalRepository.cs, IFiscalPeriodRepository.cs
    Exceptions/LedgerException.cs, BudgetConcurrencyException.cs
  GovErp.Domain.Payables/
    Entities/Vendor.cs, VendorStatus.cs
    Entities/PurchaseOrder.cs, PurchaseOrderLine.cs, PurchaseOrderStatus.cs
    Entities/VendorInvoice.cs, InvoiceDistribution.cs, InvoiceStatus.cs, InvoiceApproval.cs, ApprovalDecision.cs, InvoiceOverride.cs, ApproverRole.cs
    Repositories/IVendorRepository.cs, IPurchaseOrderRepository.cs, IVendorInvoiceRepository.cs
    Exceptions/PayablesException.cs
  GovErp.Domain.Validation/           ← пустой в этом плане (заполняется планом 2)
  GovErp.Application.Web/             ← пустой
  GovErp.Infrastructure/              ← пустой
  GovErp.Web/                         ← пустой Blazor-шаблон
tests/
  GovErp.Domain.Shared.Tests/
  GovErp.Domain.ChartOfAccounts.Tests/
  GovErp.Domain.Ledger.Tests/
  GovErp.Domain.Payables.Tests/
  GovErp.Architecture.Tests/
```

`ObjectCodeDefinition` — сущность-справочник, потому что имя `ObjectCode` занято value object'ом в Shared.

---

### Task 1: Скелет решения и граф ссылок

**Files:**
- Create: `GovErp.sln`, `Directory.Build.props`, `Directory.Packages.props`, `.editorconfig`
- Create: все проекты `src/*` и `tests/*` (пустые)
- Modify: `docs/superpowers/specs/2026-09-21-validation-engine-design.md` — убрать `CombinationRule`

**Interfaces:**
- Produces: имена проектов и граф ссылок, на которые опираются все последующие задачи.

- [ ] **Step 1: Создать корневые файлы**

`Directory.Build.props`:
```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <DisableTransitiveProjectReferences>true</DisableTransitiveProjectReferences>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
    <InvariantGlobalization>true</InvariantGlobalization>
  </PropertyGroup>
  <ItemGroup Condition="$(MSBuildProjectName.StartsWith('GovErp.Domain.')) And !$(MSBuildProjectName.EndsWith('.Tests'))">
    <Using Include="GovErp.Domain.Shared.ValueObjects" />
  </ItemGroup>
  <ItemGroup Condition="$(MSBuildProjectName.EndsWith('.Tests'))">
    <Using Include="Xunit" />
    <Using Include="FluentAssertions" />
    <Using Include="GovErp.Domain.Shared.ValueObjects" />
  </ItemGroup>
  <PropertyGroup Condition="$(MSBuildProjectName.EndsWith('.Tests'))">
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
</Project>
```

`Directory.Packages.props`:
```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.13.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.0.2" />
    <PackageVersion Include="FluentAssertions" Version="7.2.0" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  </ItemGroup>
</Project>
```

`.editorconfig`:
```ini
root = true

[*]
charset = utf-8
end_of_line = crlf
insert_final_newline = true
indent_style = space
indent_size = 4
trim_trailing_whitespace = true

[*.{csproj,props,targets,xml,json,yml,yaml}]
indent_size = 2

[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
csharp_prefer_braces = true:warning
dotnet_sort_system_directives_first = true
dotnet_diagnostic.IDE0005.severity = warning
```

- [ ] **Step 2: Создать проекты**

Выполнить из корня `D:\testtask` (PowerShell):
```powershell
dotnet new sln -n GovErp
foreach ($p in "Shared","ChartOfAccounts","Ledger","Payables","Validation") {
  dotnet new classlib -n "GovErp.Domain.$p" -o "src/GovErp.Domain.$p"
  Remove-Item "src/GovErp.Domain.$p/Class1.cs"
}
dotnet new classlib -n GovErp.Application.Web -o src/GovErp.Application.Web
Remove-Item src/GovErp.Application.Web/Class1.cs
dotnet new classlib -n GovErp.Infrastructure -o src/GovErp.Infrastructure
Remove-Item src/GovErp.Infrastructure/Class1.cs
dotnet new blazor -n GovErp.Web -o src/GovErp.Web --interactivity Server --empty
foreach ($t in "Domain.Shared","Domain.ChartOfAccounts","Domain.Ledger","Domain.Payables","Architecture") {
  dotnet new xunit -n "GovErp.$t.Tests" -o "tests/GovErp.$t.Tests"
  Remove-Item "tests/GovErp.$t.Tests/UnitTest1.cs"
}
Get-ChildItem -Recurse -Filter *.csproj | ForEach-Object { dotnet sln add $_.FullName }
```

`dotnet new xunit` создаёт csproj с версиями пакетов — их нужно убрать (CPM). Заменить содержимое каждого тестового csproj на:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="xunit" />
    <PackageReference Include="xunit.runner.visualstudio" />
    <PackageReference Include="FluentAssertions" />
  </ItemGroup>
</Project>
```

Для `tests/GovErp.Architecture.Tests` добавить ещё `<PackageReference Include="NetArchTest.Rules" />`.

Все `src/*.csproj` classlib привести к минимуму:
```xml
<Project Sdk="Microsoft.NET.Sdk">
</Project>
```
(свойства приходят из `Directory.Build.props`).

- [ ] **Step 3: Проставить ссылки между проектами**

```powershell
foreach ($p in "ChartOfAccounts","Ledger","Payables","Validation") {
  dotnet add "src/GovErp.Domain.$p" reference src/GovErp.Domain.Shared
}
foreach ($p in "Shared","ChartOfAccounts","Ledger","Payables","Validation") {
  dotnet add src/GovErp.Application.Web reference "src/GovErp.Domain.$p"
  dotnet add src/GovErp.Infrastructure reference "src/GovErp.Domain.$p"
}
dotnet add src/GovErp.Infrastructure reference src/GovErp.Application.Web
dotnet add src/GovErp.Web reference src/GovErp.Application.Web
dotnet add src/GovErp.Web reference src/GovErp.Infrastructure

dotnet add tests/GovErp.Domain.Shared.Tests reference src/GovErp.Domain.Shared
foreach ($p in "ChartOfAccounts","Ledger","Payables") {
  dotnet add "tests/GovErp.Domain.$p.Tests" reference "src/GovErp.Domain.$p"
  dotnet add "tests/GovErp.Domain.$p.Tests" reference src/GovErp.Domain.Shared
}
foreach ($p in "Shared","ChartOfAccounts","Ledger","Payables","Validation") {
  dotnet add tests/GovErp.Architecture.Tests reference "src/GovErp.Domain.$p"
}
dotnet add tests/GovErp.Architecture.Tests reference src/GovErp.Application.Web
dotnet add tests/GovErp.Architecture.Tests reference src/GovErp.Infrastructure
dotnet add tests/GovErp.Architecture.Tests reference src/GovErp.Web
```

- [ ] **Step 4: Собрать**

Run: `dotnet build`
Expected: `Build succeeded`, 0 warnings. Если `GovErp.Web` ругается на `TreatWarningsAsErrors` из-за шаблона — исправить предупреждение, не отключать флаг.

- [ ] **Step 5: Правка спеки**

В `docs/superpowers/specs/2026-09-21-validation-engine-design.md`:
- в таблице 3.2 удалить строку `CombinationRule`;
- в списке репозиториев 3.2 удалить `ICombinationRuleRepository`;
- в 6.2 в схеме `coa` удалить `CombinationRules`;
- после таблицы 3.2 добавить абзац: «Правила сочетаний выражены атрибутами `Fund` (`GrantPolicy`, `AllowedDepartments`, `AllowedObjects`) и whitelist `AccountCombination`; отдельная сущность `CombinationRule` не вводится (`DDD-15`).»

В `docs/GLOSSARY.md` строку **Combination rule** заменить на: `Fund.GrantPolicy`, `Fund.AllowedDepartments`, `Fund.AllowedObjects` | Ограничения сочетаний сегментов, заданные атрибутами фонда.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "Scaffold solution: layers, contexts, reference graph, CPM

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 2: Shared kernel — Money и коды сегментов

**Files:**
- Create: `src/GovErp.Domain.Shared/ValueObjects/Money.cs`, `SegmentCode.cs`, `FundCode.cs`, `DepartmentCode.cs`, `ObjectCode.cs`, `GrantCode.cs`
- Test: `tests/GovErp.Domain.Shared.Tests/MoneyTests.cs`, `SegmentCodeTests.cs`

**Interfaces:**
- Produces: `Money` (`Amount`, `Zero`, `Of`, `+`, `-`, унарный `-`, `<`, `>`, `<=`, `>=`, `Min`, `IsNegative`, `IsZero`), `FundCode`, `DepartmentCode`, `ObjectCode`, `GrantCode` (все — `record` с `Value`, `ToString()` возвращает `Value`).

- [ ] **Step 1: Тесты Money**

`tests/GovErp.Domain.Shared.Tests/MoneyTests.cs`:
```csharp
namespace GovErp.Domain.Shared.Tests;

public class MoneyTests
{
    [Fact]
    public void Rounds_to_two_decimals_bankers()
    {
        Money.Of(1.005m).Amount.Should().Be(1.00m);
        Money.Of(1.015m).Amount.Should().Be(1.02m);
    }

    [Fact]
    public void Arithmetic_returns_new_values()
    {
        var a = Money.Of(160_000m);
        var b = Money.Of(147_000m);
        (a - b).Should().Be(Money.Of(13_000m));
        (b - a).Should().Be(Money.Of(-13_000m));
        (a + b).Should().Be(Money.Of(307_000m));
        (-a).IsNegative.Should().BeTrue();
    }

    [Fact]
    public void Comparison_and_min()
    {
        (Money.Of(1) < Money.Of(2)).Should().BeTrue();
        (Money.Of(2) >= Money.Of(2)).Should().BeTrue();
        Money.Min(Money.Of(5), Money.Of(3)).Should().Be(Money.Of(3));
        Money.Zero.IsZero.Should().BeTrue();
    }

    [Fact]
    public void Equality_is_by_amount()
    {
        Money.Of(10).Should().Be(Money.Of(10.00m));
        Money.Of(10).Should().NotBe(Money.Of(10.01m));
    }
}
```

`tests/GovErp.Domain.Shared.Tests/SegmentCodeTests.cs`:
```csharp
namespace GovErp.Domain.Shared.Tests;

public class SegmentCodeTests
{
    [Theory]
    [InlineData("101")]
    [InlineData("701")]
    public void FundCode_accepts_three_digits(string v) => new FundCode(v).Value.Should().Be(v);

    [Theory]
    [InlineData("")]
    [InlineData("10")]
    [InlineData("1010")]
    [InlineData("70A")]
    public void FundCode_rejects_other(string v) =>
        FluentActions.Invoking(() => new FundCode(v)).Should().Throw<ArgumentException>();

    [Fact]
    public void DepartmentCode_is_four_digits()
    {
        new DepartmentCode("6000").Value.Should().Be("6000");
        FluentActions.Invoking(() => new DepartmentCode("600")).Should().Throw<ArgumentException>();
    }

    [Theory]
    [InlineData("1010")]
    [InlineData("53100")]
    public void ObjectCode_is_four_or_five_digits(string v) => new ObjectCode(v).Value.Should().Be(v);

    [Fact]
    public void GrantCode_allows_letters_digits_dashes()
    {
        new GrantCode("G-COPS-26").Value.Should().Be("G-COPS-26");
        FluentActions.Invoking(() => new GrantCode("g cops")).Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Different_segment_types_with_same_text_are_not_equal()
    {
        object fund = new FundCode("101");
        object dept = new DepartmentCode("1010");
        fund.Equals(dept).Should().BeFalse();
        new FundCode("101").Should().Be(new FundCode("101"));
    }
}
```

- [ ] **Step 2: Убедиться, что тесты не компилируются**

Run: `dotnet test tests/GovErp.Domain.Shared.Tests`
Expected: ошибка компиляции — `Money`, `FundCode` не определены.

- [ ] **Step 3: Реализация**

`src/GovErp.Domain.Shared/ValueObjects/Money.cs`:
```csharp
using System.Globalization;

namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>Денежная сумма в USD с точностью до цента. Знак допустим (дельты, результаты вычислений).</summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        Amount = decimal.Round(amount, 2, MidpointRounding.ToEven);
    }

    public static readonly Money Zero = new(0m);

    public static Money Of(decimal amount) => new(amount);

    public bool IsNegative => Amount < 0;
    public bool IsZero => Amount == 0;

    public static Money operator +(Money a, Money b) => new(a.Amount + b.Amount);
    public static Money operator -(Money a, Money b) => new(a.Amount - b.Amount);
    public static Money operator -(Money a) => new(-a.Amount);
    public static bool operator <(Money a, Money b) => a.Amount < b.Amount;
    public static bool operator >(Money a, Money b) => a.Amount > b.Amount;
    public static bool operator <=(Money a, Money b) => a.Amount <= b.Amount;
    public static bool operator >=(Money a, Money b) => a.Amount >= b.Amount;

    public static Money Min(Money a, Money b) => a < b ? a : b;
    public static Money Max(Money a, Money b) => a > b ? a : b;

    public int CompareTo(Money other) => Amount.CompareTo(other.Amount);

    public override string ToString() => Amount.ToString("N2", CultureInfo.InvariantCulture);
}
```

`src/GovErp.Domain.Shared/ValueObjects/SegmentCode.cs`:
```csharp
using System.Text.RegularExpressions;

namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>Код сегмента плана счетов. Равенство — по типу и значению: FundCode("101") ≠ DepartmentCode("101").</summary>
public abstract record SegmentCode
{
    public string Value { get; }

    protected SegmentCode(string value, string pattern, string segmentName)
    {
        if (value is null || !Regex.IsMatch(value, pattern))
        {
            throw new ArgumentException($"{segmentName} code '{value}' does not match {pattern}.", nameof(value));
        }

        Value = value;
    }

    public sealed override string ToString() => Value;
}
```

`src/GovErp.Domain.Shared/ValueObjects/FundCode.cs`:
```csharp
namespace GovErp.Domain.Shared.ValueObjects;

public sealed record FundCode : SegmentCode
{
    public FundCode(string value) : base(value, "^[0-9]{3}$", "Fund") { }
}
```

`DepartmentCode.cs` — то же с `"^[0-9]{4}$"`, `"Department"`.
`ObjectCode.cs` — `"^[0-9]{4,5}$"`, `"Object"`.
`GrantCode.cs` — `"^[A-Z][A-Z0-9-]{1,30}$"`, `"Grant"`.

- [ ] **Step 4: Прогнать тесты**

Run: `dotnet test tests/GovErp.Domain.Shared.Tests`
Expected: 10 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Shared tests/GovErp.Domain.Shared.Tests
git commit -m "Shared kernel: Money and segment codes

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 3: Shared kernel — AccountCode, FiscalYear, DatePeriod, идентификаторы

**Files:**
- Create: `src/GovErp.Domain.Shared/ValueObjects/AccountCode.cs`, `FiscalYear.cs`, `DatePeriod.cs`, `TenantId.cs`, `UserId.cs`
- Test: `tests/GovErp.Domain.Shared.Tests/AccountCodeTests.cs`, `FiscalYearTests.cs`, `DatePeriodTests.cs`

**Interfaces:**
- Produces: `AccountCode(FundCode Fund, DepartmentCode Department, ObjectCode Object, GrantCode? Grant)` с `Parse(string)`, `ToString()`, `WithObject(ObjectCode)`; `FiscalYear(int Year)` с `Start`, `End`, `Contains(DateOnly)`, `FromDate(DateOnly)`; `DatePeriod(DateOnly From, DateOnly? To)` с `Contains`; `TenantId(string Value)`; `UserId(Guid Value)` с `New()`.

- [ ] **Step 1: Тесты**

`AccountCodeTests.cs`:
```csharp
namespace GovErp.Domain.Shared.Tests;

public class AccountCodeTests
{
    [Fact]
    public void Parses_four_segments_with_dashes_inside_grant()
    {
        var code = AccountCode.Parse("701-6000-53100-G-COPS-26");
        code.Fund.Value.Should().Be("701");
        code.Department.Value.Should().Be("6000");
        code.Object.Value.Should().Be("53100");
        code.Grant!.Value.Should().Be("G-COPS-26");
        code.ToString().Should().Be("701-6000-53100-G-COPS-26");
    }

    [Fact]
    public void Parses_three_segments_without_grant()
    {
        var code = AccountCode.Parse("101-6000-53100");
        code.Grant.Should().BeNull();
        code.ToString().Should().Be("101-6000-53100");
    }

    [Fact]
    public void Rejects_less_than_three_segments() =>
        FluentActions.Invoking(() => AccountCode.Parse("101-6000")).Should().Throw<ArgumentException>();

    [Fact]
    public void WithObject_replaces_only_object()
    {
        var code = AccountCode.Parse("701-6000-53100-G-COPS-26").WithObject(new ObjectCode("2100"));
        code.ToString().Should().Be("701-6000-2100-G-COPS-26");
    }

    [Fact]
    public void Equality_is_structural() =>
        AccountCode.Parse("101-6000-53100").Should().Be(AccountCode.Parse("101-6000-53100"));
}
```

`FiscalYearTests.cs`:
```csharp
namespace GovErp.Domain.Shared.Tests;

public class FiscalYearTests
{
    [Fact]
    public void FY2026_runs_from_july_2025_to_june_2026()
    {
        var fy = new FiscalYear(2026);
        fy.Start.Should().Be(new DateOnly(2025, 7, 1));
        fy.End.Should().Be(new DateOnly(2026, 6, 30));
    }

    [Theory]
    [InlineData(2025, 7, 1, 2026)]
    [InlineData(2026, 6, 30, 2026)]
    [InlineData(2026, 7, 1, 2027)]
    [InlineData(2026, 9, 21, 2027)]
    public void FromDate_uses_july_first(int y, int m, int d, int expected) =>
        FiscalYear.FromDate(new DateOnly(y, m, d)).Year.Should().Be(expected);

    [Fact]
    public void Contains_is_inclusive()
    {
        var fy = new FiscalYear(2026);
        fy.Contains(new DateOnly(2025, 7, 1)).Should().BeTrue();
        fy.Contains(new DateOnly(2026, 6, 30)).Should().BeTrue();
        fy.Contains(new DateOnly(2026, 7, 1)).Should().BeFalse();
    }
}
```

`DatePeriodTests.cs`:
```csharp
namespace GovErp.Domain.Shared.Tests;

public class DatePeriodTests
{
    [Fact]
    public void Open_ended_period_contains_any_later_date()
    {
        var p = new DatePeriod(new DateOnly(2025, 7, 1), null);
        p.Contains(new DateOnly(2030, 1, 1)).Should().BeTrue();
        p.Contains(new DateOnly(2025, 6, 30)).Should().BeFalse();
    }

    [Fact]
    public void To_before_From_is_rejected() =>
        FluentActions.Invoking(() => new DatePeriod(new DateOnly(2026, 1, 1), new DateOnly(2025, 1, 1)))
            .Should().Throw<ArgumentException>();

    [Fact]
    public void Identifiers_reject_empty()
    {
        FluentActions.Invoking(() => new TenantId("")).Should().Throw<ArgumentException>();
        FluentActions.Invoking(() => new UserId(Guid.Empty)).Should().Throw<ArgumentException>();
        UserId.New().Value.Should().NotBe(Guid.Empty);
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Shared.Tests`
Expected: ошибки компиляции по `AccountCode`, `FiscalYear`, `DatePeriod`, `TenantId`, `UserId`.

- [ ] **Step 3: Реализация**

`AccountCode.cs`:
```csharp
namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>Полный адрес счёта: Fund-Department-Object[-Grant]. Grant может содержать дефисы.</summary>
public sealed record AccountCode(FundCode Fund, DepartmentCode Department, ObjectCode Object, GrantCode? Grant)
{
    public static AccountCode Parse(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        var parts = text.Split('-');
        if (parts.Length < 3)
        {
            throw new ArgumentException($"Account code '{text}' must have at least Fund-Department-Object.", nameof(text));
        }

        var grant = parts.Length > 3 ? new GrantCode(string.Join('-', parts[3..])) : null;
        return new AccountCode(new FundCode(parts[0]), new DepartmentCode(parts[1]), new ObjectCode(parts[2]), grant);
    }

    public AccountCode WithObject(ObjectCode objectCode) => this with { Object = objectCode };

    public override string ToString() =>
        Grant is null ? $"{Fund}-{Department}-{Object}" : $"{Fund}-{Department}-{Object}-{Grant}";
}
```

`FiscalYear.cs`:
```csharp
namespace GovErp.Domain.Shared.ValueObjects;

/// <summary>Финансовый год с 1 июля предыдущего календарного года по 30 июня.</summary>
public readonly record struct FiscalYear(int Year)
{
    public DateOnly Start => new(Year - 1, 7, 1);
    public DateOnly End => new(Year, 6, 30);

    public bool Contains(DateOnly date) => date >= Start && date <= End;

    public static FiscalYear FromDate(DateOnly date) => new(date.Month >= 7 ? date.Year + 1 : date.Year);

    public override string ToString() => $"FY{Year}";
}
```

`DatePeriod.cs`:
```csharp
namespace GovErp.Domain.Shared.ValueObjects;

public sealed record DatePeriod
{
    public DateOnly From { get; }
    public DateOnly? To { get; }

    public DatePeriod(DateOnly from, DateOnly? to)
    {
        if (to is { } t && t < from)
        {
            throw new ArgumentException($"Period end {t} is before start {from}.", nameof(to));
        }

        From = from;
        To = to;
    }

    public bool Contains(DateOnly date) => date >= From && (To is null || date <= To);
}
```

`TenantId.cs`:
```csharp
namespace GovErp.Domain.Shared.ValueObjects;

public sealed record TenantId
{
    public string Value { get; }

    public TenantId(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        Value = value;
    }

    public override string ToString() => Value;
}
```

`UserId.cs`:
```csharp
namespace GovErp.Domain.Shared.ValueObjects;

public readonly record struct UserId
{
    public Guid Value { get; }

    public UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("UserId cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static UserId New() => new(Guid.NewGuid());

    public override string ToString() => Value.ToString();
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Shared.Tests`
Expected: все зелёные (≈21).

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Shared tests/GovErp.Domain.Shared.Tests
git commit -m "Shared kernel: AccountCode, FiscalYear, DatePeriod, identifiers

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 4: ChartOfAccounts — Fund, Department, ObjectCodeDefinition, Grant

**Files:**
- Create: `src/GovErp.Domain.ChartOfAccounts/Entities/Fund.cs`, `FundType.cs`, `AccountingBasis.cs`, `BudgetControlMode.cs`, `GrantPolicy.cs`, `FundRestrictionCheck.cs`, `Department.cs`, `ObjectCodeDefinition.cs`, `ObjectCategory.cs`, `Grant.cs`, `GrantStatus.cs`, `GrantEligibility.cs`
- Create: `src/GovErp.Domain.ChartOfAccounts/Exceptions/ChartOfAccountsException.cs`
- Test: `tests/GovErp.Domain.ChartOfAccounts.Tests/FundTests.cs`, `GrantTests.cs`

**Interfaces:**
- Produces: `Fund.Check(DepartmentCode, ObjectCode) → FundRestrictionCheck`, `Fund.ControlMode`, `Fund.GrantPolicy`, `Fund.Basis`, `Fund.Type`; `Grant.CheckEligibility(DateOnly, DepartmentCode, ObjectCode) → GrantEligibility`; `Grant.IsFederal`. План 2 использует эти результаты в снимках.

- [ ] **Step 1: Тесты**

`FundTests.cs`:
```csharp
using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class FundTests
{
    private static Fund StreetFund() => new(
        new FundCode("202"), "Street Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual,
        BudgetControlMode.Hard, GrantPolicy.Forbidden,
        allowedDepartments: [new DepartmentCode("4000")],
        allowedObjects: [new ObjectCode("53100"), new ObjectCode("54000"), new ObjectCode("55000")]);

    private static Fund GeneralFund() => new(
        new FundCode("101"), "General Fund", FundType.Governmental, AccountingBasis.ModifiedAccrual,
        BudgetControlMode.Soft, GrantPolicy.Forbidden, allowedDepartments: [], allowedObjects: []);

    [Fact]
    public void Restricted_fund_rejects_other_department() =>
        StreetFund().Check(new DepartmentCode("3000"), new ObjectCode("53100"))
            .Should().Be(FundRestrictionCheck.DepartmentNotAllowed);

    [Fact]
    public void Restricted_fund_rejects_other_object() =>
        StreetFund().Check(new DepartmentCode("4000"), new ObjectCode("51000"))
            .Should().Be(FundRestrictionCheck.ObjectNotAllowed);

    [Fact]
    public void Restricted_fund_allows_listed_pair() =>
        StreetFund().Check(new DepartmentCode("4000"), new ObjectCode("54000"))
            .Should().Be(FundRestrictionCheck.Allowed);

    [Fact]
    public void Empty_lists_mean_unrestricted() =>
        GeneralFund().Check(new DepartmentCode("9999"), new ObjectCode("99999"))
            .Should().Be(FundRestrictionCheck.Allowed);

    [Fact]
    public void Name_is_required() =>
        FluentActions.Invoking(() => new Fund(new FundCode("101"), " ", FundType.Governmental,
                AccountingBasis.ModifiedAccrual, BudgetControlMode.Soft, GrantPolicy.Forbidden, [], []))
            .Should().Throw<ArgumentException>();
}
```

`GrantTests.cs`:
```csharp
using GovErp.Domain.ChartOfAccounts.Entities;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class GrantTests
{
    private static Grant Cops(GrantStatus status = GrantStatus.Active) => new(
        new GrantCode("G-COPS-26"), "COPS Hiring Program", "US DOJ", isFederal: true,
        new DatePeriod(new DateOnly(2025, 7, 1), new DateOnly(2027, 6, 30)),
        allowedDepartments: [new DepartmentCode("3000"), new DepartmentCode("6000")],
        allowableObjects: [new ObjectCode("53100"), new ObjectCode("54000")],
        status);

    private static readonly DateOnly InPeriod = new(2026, 9, 15);

    [Fact]
    public void Eligible_when_all_match() =>
        Cops().CheckEligibility(InPeriod, new DepartmentCode("6000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.Eligible);

    [Fact]
    public void Closed_grant_is_not_active() =>
        Cops(GrantStatus.Closed).CheckEligibility(InPeriod, new DepartmentCode("6000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.GrantNotActive);

    [Fact]
    public void Date_outside_period() =>
        Cops().CheckEligibility(new DateOnly(2025, 6, 30), new DepartmentCode("6000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.OutsidePeriod);

    [Fact]
    public void Department_not_allowed() =>
        Cops().CheckEligibility(InPeriod, new DepartmentCode("4000"), new ObjectCode("53100"))
            .Should().Be(GrantEligibility.DepartmentNotAllowed);

    [Fact]
    public void Object_not_allowable() =>
        Cops().CheckEligibility(InPeriod, new DepartmentCode("6000"), new ObjectCode("55000"))
            .Should().Be(GrantEligibility.ObjectNotAllowed);
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.ChartOfAccounts.Tests`
Expected: ошибки компиляции.

- [ ] **Step 3: Реализация**

Перечисления, по одному файлу каждое, `namespace GovErp.Domain.ChartOfAccounts.Entities;`:
```csharp
public enum FundType { Governmental, Enterprise }
public enum AccountingBasis { ModifiedAccrual, FullAccrual }
public enum BudgetControlMode { Hard, Soft }
public enum GrantPolicy { Required, Forbidden, Optional }
public enum FundRestrictionCheck { Allowed, DepartmentNotAllowed, ObjectNotAllowed }
public enum ObjectCategory { Expenditure, Asset, Liability, Budgetary }
public enum GrantStatus { Active, Suspended, Closed }
public enum GrantEligibility { Eligible, GrantNotActive, OutsidePeriod, DepartmentNotAllowed, ObjectNotAllowed }
```

`Exceptions/ChartOfAccountsException.cs`:
```csharp
namespace GovErp.Domain.ChartOfAccounts.Exceptions;

public sealed class ChartOfAccountsException(string message) : Exception(message);
```

`Entities/Fund.cs`:
```csharp
namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>Фонд — самостоятельная учётная единица с ограничениями на использование. Справочник (анемичный), кроме Check.</summary>
public sealed class Fund
{
    public FundCode Code { get; private set; }
    public string Name { get; private set; }
    public FundType Type { get; private set; }
    public AccountingBasis Basis { get; private set; }
    public BudgetControlMode ControlMode { get; private set; }
    public GrantPolicy GrantPolicy { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<DepartmentCode> AllowedDepartments { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<ObjectCode> AllowedObjects { get; private set; }
    public bool IsActive { get; private set; }

    public Fund(FundCode code, string name, FundType type, AccountingBasis basis, BudgetControlMode controlMode,
        GrantPolicy grantPolicy, IReadOnlyList<DepartmentCode> allowedDepartments, IReadOnlyList<ObjectCode> allowedObjects,
        bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code;
        Name = name;
        Type = type;
        Basis = basis;
        ControlMode = controlMode;
        GrantPolicy = grantPolicy;
        AllowedDepartments = allowedDepartments.ToList();
        AllowedObjects = allowedObjects.ToList();
        IsActive = isActive;
    }

    /// <summary>Для восстановления из хранилища (DDD-9).</summary>
    private Fund()
    {
        Code = null!;
        Name = null!;
        AllowedDepartments = [];
        AllowedObjects = [];
    }

    public FundRestrictionCheck Check(DepartmentCode department, ObjectCode objectCode)
    {
        if (AllowedDepartments.Count > 0 && !AllowedDepartments.Contains(department))
        {
            return FundRestrictionCheck.DepartmentNotAllowed;
        }

        if (AllowedObjects.Count > 0 && !AllowedObjects.Contains(objectCode))
        {
            return FundRestrictionCheck.ObjectNotAllowed;
        }

        return FundRestrictionCheck.Allowed;
    }
}
```

`Entities/Department.cs`:
```csharp
namespace GovErp.Domain.ChartOfAccounts.Entities;

public sealed class Department
{
    public DepartmentCode Code { get; private set; }
    public string Name { get; private set; }
    public bool IsActive { get; private set; }

    public Department(DepartmentCode code, string name, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code;
        Name = name;
        IsActive = isActive;
    }

    private Department() { Code = null!; Name = null!; }
}
```

`Entities/ObjectCodeDefinition.cs`:
```csharp
namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>Справочная запись object-кода: вид расхода / актива / обязательства / бюджетный счёт.</summary>
public sealed class ObjectCodeDefinition
{
    public ObjectCode Code { get; private set; }
    public string Name { get; private set; }
    public ObjectCategory Category { get; private set; }
    public bool IsActive { get; private set; }

    public ObjectCodeDefinition(ObjectCode code, string name, ObjectCategory category, bool isActive = true)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Code = code;
        Name = name;
        Category = category;
        IsActive = isActive;
    }

    private ObjectCodeDefinition() { Code = null!; Name = null!; }
}
```

`Entities/Grant.cs`:
```csharp
namespace GovErp.Domain.ChartOfAccounts.Entities;

public sealed class Grant
{
    public GrantCode Code { get; private set; }
    public string Name { get; private set; }
    public string Sponsor { get; private set; }
    public bool IsFederal { get; private set; }
    public DatePeriod Period { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<DepartmentCode> AllowedDepartments { get; private set; }
    /// <summary>Пусто — без ограничений.</summary>
    public IReadOnlyList<ObjectCode> AllowableObjects { get; private set; }
    public GrantStatus Status { get; private set; }

    public Grant(GrantCode code, string name, string sponsor, bool isFederal, DatePeriod period,
        IReadOnlyList<DepartmentCode> allowedDepartments, IReadOnlyList<ObjectCode> allowableObjects,
        GrantStatus status = GrantStatus.Active)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(sponsor);
        Code = code;
        Name = name;
        Sponsor = sponsor;
        IsFederal = isFederal;
        Period = period;
        AllowedDepartments = allowedDepartments.ToList();
        AllowableObjects = allowableObjects.ToList();
        Status = status;
    }

    private Grant()
    {
        Code = null!;
        Name = null!;
        Sponsor = null!;
        Period = null!;
        AllowedDepartments = [];
        AllowableObjects = [];
    }

    public GrantEligibility CheckEligibility(DateOnly transactionDate, DepartmentCode department, ObjectCode objectCode)
    {
        if (Status != GrantStatus.Active)
        {
            return GrantEligibility.GrantNotActive;
        }

        if (!Period.Contains(transactionDate))
        {
            return GrantEligibility.OutsidePeriod;
        }

        if (AllowedDepartments.Count > 0 && !AllowedDepartments.Contains(department))
        {
            return GrantEligibility.DepartmentNotAllowed;
        }

        if (AllowableObjects.Count > 0 && !AllowableObjects.Contains(objectCode))
        {
            return GrantEligibility.ObjectNotAllowed;
        }

        return GrantEligibility.Eligible;
    }
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.ChartOfAccounts.Tests`
Expected: 10 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.ChartOfAccounts tests/GovErp.Domain.ChartOfAccounts.Tests
git commit -m "ChartOfAccounts: Fund, Department, ObjectCodeDefinition, Grant

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 5: ChartOfAccounts — AccountCombination и репозитории

**Files:**
- Create: `src/GovErp.Domain.ChartOfAccounts/Entities/AccountCombination.cs`, `CombinationStatus.cs`, `CombinationSource.cs`
- Create: `src/GovErp.Domain.ChartOfAccounts/Repositories/IFundRepository.cs`, `IGrantRepository.cs`, `IAccountCombinationRepository.cs`, `IReferenceDataRepository.cs`
- Test: `tests/GovErp.Domain.ChartOfAccounts.Tests/AccountCombinationTests.cs`

**Interfaces:**
- Produces: `AccountCombination.Request(...)`, `.Approve(UserId, DateTimeOffset)`, `.Deactivate(DateOnly)`, `.IsActiveOn(DateOnly)`, `.Status`; четыре интерфейса репозиториев (сигнатуры ниже — план 3 их реализует).

- [ ] **Step 1: Тесты**

`AccountCombinationTests.cs`:
```csharp
using GovErp.Domain.ChartOfAccounts.Entities;
using GovErp.Domain.ChartOfAccounts.Exceptions;

namespace GovErp.Domain.ChartOfAccounts.Tests;

public class AccountCombinationTests
{
    private static readonly UserId Clerk = UserId.New();
    private static readonly UserId Controller = UserId.New();
    private static readonly DateTimeOffset Now = new(2026, 9, 21, 10, 0, 0, TimeSpan.Zero);
    private static readonly AccountCode Code = AccountCode.Parse("701-6000-53100-G-COPS-26");

    private static AccountCombination Pending() =>
        AccountCombination.Request(Code, new DateOnly(2025, 7, 1), Clerk, Now, CombinationSource.Manual);

    [Fact]
    public void Requested_combination_is_pending_and_not_active()
    {
        var c = Pending();
        c.Status.Should().Be(CombinationStatus.Pending);
        c.IsActiveOn(new DateOnly(2026, 9, 21)).Should().BeFalse();
    }

    [Fact]
    public void Approve_activates_and_records_approver()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.Status.Should().Be(CombinationStatus.Active);
        c.ApprovedBy.Should().Be(Controller);
        c.ApprovedAt.Should().Be(Now);
        c.IsActiveOn(new DateOnly(2026, 9, 21)).Should().BeTrue();
    }

    [Fact]
    public void Approve_twice_is_rejected()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        FluentActions.Invoking(() => c.Approve(Controller, Now)).Should().Throw<ChartOfAccountsException>();
    }

    [Fact]
    public void Deactivate_sets_effective_to_and_status()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.Deactivate(new DateOnly(2026, 6, 30));
        c.Status.Should().Be(CombinationStatus.Inactive);
        c.IsActiveOn(new DateOnly(2026, 6, 30)).Should().BeTrue();
        c.IsActiveOn(new DateOnly(2026, 7, 1)).Should().BeFalse();
    }

    [Fact]
    public void Deactivate_before_effective_from_is_rejected()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        FluentActions.Invoking(() => c.Deactivate(new DateOnly(2025, 6, 30)))
            .Should().Throw<ChartOfAccountsException>();
    }

    [Fact]
    public void Not_active_before_effective_from()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.IsActiveOn(new DateOnly(2025, 6, 30)).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.ChartOfAccounts.Tests`

- [ ] **Step 3: Реализация**

```csharp
namespace GovErp.Domain.ChartOfAccounts.Entities;
public enum CombinationStatus { Pending, Active, Inactive }
public enum CombinationSource { Manual, DynamicInsert, Generated }
```

`Entities/AccountCombination.cs`:
```csharp
using GovErp.Domain.ChartOfAccounts.Exceptions;

namespace GovErp.Domain.ChartOfAccounts.Entities;

/// <summary>Заведённый и утверждённый полный адрес счёта (whitelist) с effective dating.</summary>
public sealed class AccountCombination
{
    public Guid Id { get; private set; }
    public AccountCode Code { get; private set; }
    public CombinationStatus Status { get; private set; }
    public DateOnly EffectiveFrom { get; private set; }
    public DateOnly? EffectiveTo { get; private set; }
    public CombinationSource Source { get; private set; }
    public UserId CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public UserId? ApprovedBy { get; private set; }
    public DateTimeOffset? ApprovedAt { get; private set; }

    private AccountCombination(Guid id, AccountCode code, DateOnly effectiveFrom, UserId createdBy,
        DateTimeOffset createdAt, CombinationSource source)
    {
        Id = id;
        Code = code;
        Status = CombinationStatus.Pending;
        EffectiveFrom = effectiveFrom;
        Source = source;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
    }

    private AccountCombination() { Code = null!; }

    public static AccountCombination Request(AccountCode code, DateOnly effectiveFrom, UserId requestedBy,
        DateTimeOffset at, CombinationSource source) =>
        new(Guid.NewGuid(), code, effectiveFrom, requestedBy, at, source);

    public void Approve(UserId approvedBy, DateTimeOffset at)
    {
        if (Status != CombinationStatus.Pending)
        {
            throw new ChartOfAccountsException($"Combination {Code} is {Status}; only Pending can be approved.");
        }

        Status = CombinationStatus.Active;
        ApprovedBy = approvedBy;
        ApprovedAt = at;
    }

    public void Deactivate(DateOnly effectiveTo)
    {
        if (Status == CombinationStatus.Inactive)
        {
            throw new ChartOfAccountsException($"Combination {Code} is already inactive.");
        }

        if (effectiveTo < EffectiveFrom)
        {
            throw new ChartOfAccountsException($"EffectiveTo {effectiveTo} is before EffectiveFrom {EffectiveFrom}.");
        }

        Status = CombinationStatus.Inactive;
        EffectiveTo = effectiveTo;
    }

    public bool IsActiveOn(DateOnly date) =>
        Status != CombinationStatus.Pending
        && date >= EffectiveFrom
        && (EffectiveTo is null || date <= EffectiveTo);
}
```

Обрати внимание: `IsActiveOn` для `Inactive` с `EffectiveTo` в будущем возвращает `true` до этой даты — деактивация с датой в будущем означает «действует до».

Репозитории, `namespace GovErp.Domain.ChartOfAccounts.Repositories;`:

```csharp
using GovErp.Domain.ChartOfAccounts.Entities;

public interface IFundRepository
{
    Task<Fund?> FindAsync(FundCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Fund>> ListAsync(CancellationToken ct = default);
}

public interface IGrantRepository
{
    Task<Grant?> FindAsync(GrantCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Grant>> ListAsync(CancellationToken ct = default);
}

public interface IAccountCombinationRepository
{
    Task<AccountCombination?> FindAsync(AccountCode code, CancellationToken ct = default);
    Task<IReadOnlyList<AccountCombination>> ListAsync(CancellationToken ct = default);
    Task AddAsync(AccountCombination combination, CancellationToken ct = default);
}

public interface IReferenceDataRepository
{
    Task<Department?> FindDepartmentAsync(DepartmentCode code, CancellationToken ct = default);
    Task<ObjectCodeDefinition?> FindObjectAsync(ObjectCode code, CancellationToken ct = default);
    Task<IReadOnlyList<Department>> ListDepartmentsAsync(CancellationToken ct = default);
    Task<IReadOnlyList<ObjectCodeDefinition>> ListObjectsAsync(CancellationToken ct = default);
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.ChartOfAccounts.Tests`
Expected: 16 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.ChartOfAccounts tests/GovErp.Domain.ChartOfAccounts.Tests
git commit -m "ChartOfAccounts: AccountCombination whitelist and repository ports

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 6: Ledger — BudgetLine с резервированием

**Files:**
- Create: `src/GovErp.Domain.Ledger/Entities/BudgetLine.cs`, `BudgetAmendment.cs`, `BudgetReservation.cs`, `ReservationStatus.cs`, `ReservationResult.cs`, `BudgetControlMode.cs`
- Create: `src/GovErp.Domain.Ledger/Exceptions/LedgerException.cs`, `BudgetConcurrencyException.cs`
- Test: `tests/GovErp.Domain.Ledger.Tests/BudgetLineTests.cs`

**Interfaces:**
- Produces: `BudgetLine` (`Account`, `FiscalYear`, `ControlMode`, `Adopted`, `Amended`, `Actuals`, `Encumbered`, `Held`, `Available`, `Amendments`, `Reservations`), методы `Amend`, `Reserve → ReservationResult`, `Commit(Guid)`, `Release(Guid)`, `RecordEncumbrance(Money)`, `RecordLiquidation(Money)`; `ReservationResult` (`IsReserved`, `ReservationId`, `AvailableBefore`, `Shortfall`, `IsOverage`).
- `BudgetControlMode` в Ledger — **свой** enum, не из ChartOfAccounts (контексты не ссылаются друг на друга; маппинг — в Application, план 3).

- [ ] **Step 1: Тесты**

`BudgetLineTests.cs`:
```csharp
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class BudgetLineTests
{
    private static readonly AccountCode Cops = AccountCode.Parse("701-6000-53100-G-COPS-26");
    private static readonly FiscalYear Fy = new(2026);
    private static readonly DateOnly Today = new(2026, 9, 21);

    /// <summary>Строка задания: 375,000 − 132,000 − 96,000 = 147,000.</summary>
    private static BudgetLine ExerciseLine(BudgetControlMode mode = BudgetControlMode.Hard)
    {
        var line = new BudgetLine(Cops, Fy, mode, adopted: Money.Of(375_000m));
        line.RecordActuals(Money.Of(132_000m));
        line.RecordEncumbrance(Money.Of(96_000m));
        return line;
    }

    [Fact]
    public void Available_is_amended_minus_actuals_minus_encumbered_minus_held()
    {
        var line = ExerciseLine();
        line.Amended.Should().Be(Money.Of(375_000m));
        line.Available.Should().Be(Money.Of(147_000m));
    }

    [Fact]
    public void Hard_control_refuses_reservation_over_available()
    {
        var line = ExerciseLine();
        var result = line.Reserve(Money.Of(160_000m), "INV-1");
        result.IsReserved.Should().BeFalse();
        result.Shortfall.Should().Be(Money.Of(13_000m));
        result.AvailableBefore.Should().Be(Money.Of(147_000m));
        line.Held.Should().Be(Money.Zero);
        line.Reservations.Should().BeEmpty();
    }

    [Fact]
    public void Soft_control_reserves_with_overage_flag()
    {
        var line = ExerciseLine(BudgetControlMode.Soft);
        var result = line.Reserve(Money.Of(160_000m), "INV-1");
        result.IsReserved.Should().BeTrue();
        result.IsOverage.Should().BeTrue();
        line.Held.Should().Be(Money.Of(160_000m));
        line.Available.Should().Be(Money.Of(-13_000m));
    }

    [Fact]
    public void Reservation_reduces_available_for_the_next_one()
    {
        var line = ExerciseLine();
        line.Reserve(Money.Of(100_000m), "INV-A").IsReserved.Should().BeTrue();
        line.Available.Should().Be(Money.Of(47_000m));
        line.Reserve(Money.Of(100_000m), "INV-B").IsReserved.Should().BeFalse();
    }

    [Fact]
    public void Amend_raises_amended_and_available()
    {
        var line = ExerciseLine();
        line.Amend(Money.Of(13_000m), "BA-2026-14", Today);
        line.Amended.Should().Be(Money.Of(388_000m));
        line.Available.Should().Be(Money.Of(160_000m));
        line.Amendments.Should().ContainSingle(a => a.Reference == "BA-2026-14" && a.Amount == Money.Of(13_000m));
        line.Reserve(Money.Of(160_000m), "INV-1").IsReserved.Should().BeTrue();
    }

    [Fact]
    public void Commit_moves_held_to_actuals()
    {
        var line = ExerciseLine();
        var r = line.Reserve(Money.Of(100_000m), "INV-A");
        line.Commit(r.ReservationId!.Value);
        line.Held.Should().Be(Money.Zero);
        line.Actuals.Should().Be(Money.Of(232_000m));
        line.Available.Should().Be(Money.Of(47_000m));
        line.Reservations.Single().Status.Should().Be(ReservationStatus.Committed);
    }

    [Fact]
    public void Commit_twice_is_rejected()
    {
        var line = ExerciseLine();
        var r = line.Reserve(Money.Of(100_000m), "INV-A");
        line.Commit(r.ReservationId!.Value);
        FluentActions.Invoking(() => line.Commit(r.ReservationId!.Value)).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Release_frees_held()
    {
        var line = ExerciseLine();
        var r = line.Reserve(Money.Of(100_000m), "INV-A");
        line.Release(r.ReservationId!.Value);
        line.Held.Should().Be(Money.Zero);
        line.Available.Should().Be(Money.Of(147_000m));
        line.Reservations.Single().Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public void Unknown_reservation_is_rejected() =>
        FluentActions.Invoking(() => ExerciseLine().Commit(Guid.NewGuid())).Should().Throw<LedgerException>();

    [Fact]
    public void Liquidation_reduces_encumbered()
    {
        var line = ExerciseLine();
        line.RecordLiquidation(Money.Of(36_000m));
        line.Encumbered.Should().Be(Money.Of(60_000m));
        line.Available.Should().Be(Money.Of(183_000m));
    }

    [Fact]
    public void Liquidation_over_encumbered_is_rejected() =>
        FluentActions.Invoking(() => ExerciseLine().RecordLiquidation(Money.Of(96_001m)))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Negative_or_zero_amounts_are_rejected()
    {
        var line = ExerciseLine();
        FluentActions.Invoking(() => line.Reserve(Money.Zero, "X")).Should().Throw<LedgerException>();
        FluentActions.Invoking(() => line.Amend(Money.Zero, "X", Today)).Should().Throw<LedgerException>();
        FluentActions.Invoking(() => line.RecordEncumbrance(Money.Of(-1))).Should().Throw<LedgerException>();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`

- [ ] **Step 3: Реализация**

`Exceptions/LedgerException.cs`:
```csharp
namespace GovErp.Domain.Ledger.Exceptions;

public class LedgerException(string message) : Exception(message);
```

`Exceptions/BudgetConcurrencyException.cs`:
```csharp
namespace GovErp.Domain.Ledger.Exceptions;

/// <summary>Бросается реализацией репозитория при конфликте rowversion. Слой сценариев ловит без ссылки на EF.</summary>
public sealed class BudgetConcurrencyException(AccountCode account, FiscalYear fiscalYear)
    : LedgerException($"Budget line {account} {fiscalYear} was modified concurrently.")
{
    public AccountCode Account { get; } = account;
    public FiscalYear FiscalYear { get; } = fiscalYear;
}
```

`Entities/BudgetControlMode.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public enum BudgetControlMode { Hard, Soft }
```

`Entities/ReservationStatus.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public enum ReservationStatus { Held, Committed, Released }
```

`Entities/BudgetAmendment.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public sealed record BudgetAmendment(Money Amount, string Reference, DateOnly EffectiveDate);
```

`Entities/BudgetReservation.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

/// <summary>Вложенная сущность BudgetLine: резерв под транзакцию между Submit и Post.</summary>
public sealed class BudgetReservation
{
    public Guid Id { get; private set; }
    public string SourceRef { get; private set; }
    public Money Amount { get; private set; }
    public ReservationStatus Status { get; private set; }

    internal BudgetReservation(Guid id, string sourceRef, Money amount)
    {
        Id = id;
        SourceRef = sourceRef;
        Amount = amount;
        Status = ReservationStatus.Held;
    }

    private BudgetReservation() { SourceRef = null!; }

    internal void Commit() => Status = ReservationStatus.Committed;
    internal void Release() => Status = ReservationStatus.Released;
}
```

`Entities/ReservationResult.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public sealed record ReservationResult(bool IsReserved, Guid? ReservationId, Money AvailableBefore, Money Shortfall, bool IsOverage)
{
    public static ReservationResult Reserved(Guid id, Money availableBefore, bool overage) =>
        new(true, id, availableBefore, Money.Zero, overage);

    public static ReservationResult Refused(Money availableBefore, Money shortfall) =>
        new(false, null, availableBefore, shortfall, true);
}
```

`Entities/BudgetLine.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>
/// Бюджетная строка по одной комбинации счетов и финансовому году.
/// Available = Amended − Actuals − Encumbered − Held. Резервирование — единственная точка защиты от двойного списания.
/// </summary>
public sealed class BudgetLine
{
    private readonly List<BudgetAmendment> _amendments = [];
    private readonly List<BudgetReservation> _reservations = [];

    public Guid Id { get; private set; }
    public AccountCode Account { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public BudgetControlMode ControlMode { get; private set; }
    public Money Adopted { get; private set; }
    public Money Actuals { get; private set; }
    public Money Encumbered { get; private set; }

    public IReadOnlyList<BudgetAmendment> Amendments => _amendments;
    public IReadOnlyList<BudgetReservation> Reservations => _reservations;

    public Money Amended => _amendments.Aggregate(Adopted, (sum, a) => sum + a.Amount);
    public Money Held => _reservations.Where(r => r.Status == ReservationStatus.Held)
        .Aggregate(Money.Zero, (sum, r) => sum + r.Amount);
    public Money Available => Amended - Actuals - Encumbered - Held;

    public BudgetLine(AccountCode account, FiscalYear fiscalYear, BudgetControlMode controlMode, Money adopted)
    {
        RequirePositiveOrZero(adopted, nameof(adopted));
        Id = Guid.NewGuid();
        Account = account;
        FiscalYear = fiscalYear;
        ControlMode = controlMode;
        Adopted = adopted;
        Actuals = Money.Zero;
        Encumbered = Money.Zero;
    }

    private BudgetLine() { Account = null!; }

    public void Amend(Money amount, string reference, DateOnly effectiveDate)
    {
        if (amount.IsZero)
        {
            throw new LedgerException("Amendment amount cannot be zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        _amendments.Add(new BudgetAmendment(amount, reference, effectiveDate));
    }

    public ReservationResult Reserve(Money amount, string sourceRef)
    {
        RequirePositive(amount, nameof(amount));
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);

        var availableBefore = Available;
        var overage = amount > availableBefore;
        if (overage && ControlMode == BudgetControlMode.Hard)
        {
            return ReservationResult.Refused(availableBefore, amount - availableBefore);
        }

        var reservation = new BudgetReservation(Guid.NewGuid(), sourceRef, amount);
        _reservations.Add(reservation);
        return ReservationResult.Reserved(reservation.Id, availableBefore, overage);
    }

    public void Commit(Guid reservationId)
    {
        var reservation = FindHeld(reservationId);
        reservation.Commit();
        Actuals += reservation.Amount;
    }

    public void Release(Guid reservationId) => FindHeld(reservationId).Release();

    /// <summary>Только для seed и восстановления состояния: прямое начисление actuals без резерва.</summary>
    public void RecordActuals(Money amount)
    {
        RequirePositive(amount, nameof(amount));
        Actuals += amount;
    }

    public void RecordEncumbrance(Money amount)
    {
        RequirePositive(amount, nameof(amount));
        Encumbered += amount;
    }

    public void RecordLiquidation(Money amount)
    {
        RequirePositive(amount, nameof(amount));
        if (amount > Encumbered)
        {
            throw new LedgerException($"Cannot liquidate {amount}: only {Encumbered} encumbered on {Account}.");
        }

        Encumbered -= amount;
    }

    private BudgetReservation FindHeld(Guid reservationId)
    {
        var reservation = _reservations.SingleOrDefault(r => r.Id == reservationId)
            ?? throw new LedgerException($"Reservation {reservationId} not found on {Account}.");
        if (reservation.Status != ReservationStatus.Held)
        {
            throw new LedgerException($"Reservation {reservationId} is {reservation.Status}, expected Held.");
        }

        return reservation;
    }

    private static void RequirePositive(Money amount, string name)
    {
        if (amount <= Money.Zero)
        {
            throw new LedgerException($"{name} must be positive, got {amount}.");
        }
    }

    private static void RequirePositiveOrZero(Money amount, string name)
    {
        if (amount.IsNegative)
        {
            throw new LedgerException($"{name} cannot be negative, got {amount}.");
        }
    }
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`
Expected: 12 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Ledger tests/GovErp.Domain.Ledger.Tests
git commit -m "Ledger: BudgetLine with reservation, commit, release, amendment

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 7: Ledger — Encumbrance, JournalEntry, FiscalPeriod, репозитории

**Files:**
- Create: `src/GovErp.Domain.Ledger/Entities/Encumbrance.cs`, `EncumbranceLiquidation.cs`, `EncumbranceStatus.cs`, `JournalEntry.cs`, `JournalLine.cs`, `LedgerFamily.cs`, `FiscalPeriod.cs`, `PeriodStatus.cs`
- Create: `src/GovErp.Domain.Ledger/Repositories/IBudgetLineRepository.cs`, `IEncumbranceRepository.cs`, `IJournalRepository.cs`, `IFiscalPeriodRepository.cs`
- Test: `tests/GovErp.Domain.Ledger.Tests/EncumbranceTests.cs`, `JournalEntryTests.cs`

**Interfaces:**
- Produces: `Encumbrance` (`PoLineRef`, `Account`, `Original`, `Liquidated`, `Released`, `Remaining`, `Status`, `Liquidate(Money, string)`, `ReleaseRemainder(string)`); `JournalLine(AccountCode, LedgerFamily, Money Debit, Money Credit, string Description)`; `JournalEntry.Create(string sourceRef, IReadOnlyList<JournalLine>, FiscalPeriod, UserId, DateTimeOffset)`; `FiscalPeriod(int Year, int Month)` с `IsOpen`, `Close()`, `FiscalPeriod.KeyFor(DateOnly) → (int Year, int Month)`.

- [ ] **Step 1: Тесты**

`EncumbranceTests.cs`:
```csharp
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class EncumbranceTests
{
    private static readonly AccountCode Cops = AccountCode.Parse("701-3000-53100-G-COPS-26");

    private static Encumbrance Open() => new("PO-2026-0451/1", Cops, Money.Of(160_000m));

    [Fact]
    public void New_encumbrance_is_open_with_full_remaining()
    {
        var e = Open();
        e.Remaining.Should().Be(Money.Of(160_000m));
        e.Status.Should().Be(EncumbranceStatus.Open);
    }

    [Fact]
    public void Partial_liquidation_keeps_it_open()
    {
        var e = Open();
        e.Liquidate(Money.Of(32_000m), "INV-1");
        e.Liquidated.Should().Be(Money.Of(32_000m));
        e.Remaining.Should().Be(Money.Of(128_000m));
        e.Status.Should().Be(EncumbranceStatus.Open);
        e.Liquidations.Should().ContainSingle(l => l.SourceRef == "INV-1" && l.Amount == Money.Of(32_000m));
    }

    [Fact]
    public void Full_liquidation_closes()
    {
        var e = Open();
        e.Liquidate(Money.Of(160_000m), "INV-1");
        e.Remaining.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void Liquidating_more_than_remaining_is_rejected()
    {
        var e = Open();
        FluentActions.Invoking(() => e.Liquidate(Money.Of(160_001m), "INV-1")).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Release_remainder_closes_and_records_released()
    {
        var e = Open();
        e.Liquidate(Money.Of(90_000m), "INV-1");
        e.ReleaseRemainder("final invoice");
        e.Released.Should().Be(Money.Of(70_000m));
        e.Remaining.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void Closed_encumbrance_rejects_liquidation()
    {
        var e = Open();
        e.ReleaseRemainder("cancelled");
        FluentActions.Invoking(() => e.Liquidate(Money.Of(1), "INV-9")).Should().Throw<LedgerException>();
    }
}
```

`JournalEntryTests.cs`:
```csharp
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class JournalEntryTests
{
    private static readonly UserId Poster = UserId.New();
    private static readonly DateTimeOffset At = new(2026, 9, 21, 12, 0, 0, TimeSpan.Zero);
    private static readonly FiscalPeriod OpenSep = new(2026, 9);

    private static JournalLine Dr(string account, decimal amount, LedgerFamily family = LedgerFamily.Financial) =>
        new(AccountCode.Parse(account), family, Money.Of(amount), Money.Zero, "test");

    private static JournalLine Cr(string account, decimal amount, LedgerFamily family = LedgerFamily.Financial) =>
        new(AccountCode.Parse(account), family, Money.Zero, Money.Of(amount), "test");

    [Fact]
    public void Balanced_single_fund_entry_is_created()
    {
        var entry = JournalEntry.Create("INV-1",
            [Dr("701-6000-53100-G-COPS-26", 160_000m), Cr("701-0000-2100", 160_000m)], OpenSep, Poster, At);
        entry.Lines.Should().HaveCount(2);
        entry.SourceRef.Should().Be("INV-1");
    }

    [Fact]
    public void Multi_fund_entry_must_balance_per_fund()
    {
        // Итог сходится (30k/30k), но фонд 101 — Дт 12k / Кт 8k, фонд 202 — Дт 8k / Кт 12k.
        FluentActions.Invoking(() => JournalEntry.Create("INV-2",
            [Dr("101-6000-53100", 12_000m), Cr("101-0000-2100", 8_000m),
             Dr("202-4000-53100", 8_000m),  Cr("202-0000-2100", 12_000m),
             Dr("501-5000-53100", 10_000m), Cr("501-0000-2100", 10_000m)], OpenSep, Poster, At))
            .Should().Throw<LedgerException>().WithMessage("*101*");
    }

    [Fact]
    public void Multi_fund_balanced_entry_is_created()
    {
        var entry = JournalEntry.Create("INV-2",
            [Dr("101-6000-53100", 12_000m), Cr("101-0000-2100", 12_000m),
             Dr("202-4000-53100", 8_000m),  Cr("202-0000-2100", 8_000m),
             Dr("501-5000-53100", 10_000m), Cr("501-0000-2100", 10_000m)], OpenSep, Poster, At);
        entry.Lines.Should().HaveCount(6);
    }

    [Fact]
    public void Budgetary_and_financial_families_balance_separately()
    {
        // Сторно encumbrance (Budgetary) + расход (Financial) — обе пары внутри своих семейств.
        var entry = JournalEntry.Create("INV-3",
            [Dr("701-0000-2900-G-COPS-26", 160_000m, LedgerFamily.Budgetary), Cr("701-3000-5900-G-COPS-26", 160_000m, LedgerFamily.Budgetary),
             Dr("701-3000-53100-G-COPS-26", 160_000m), Cr("701-0000-2100", 160_000m)], OpenSep, Poster, At);
        entry.Lines.Should().HaveCount(4);

        FluentActions.Invoking(() => JournalEntry.Create("INV-4",
            [Dr("701-0000-2900-G-COPS-26", 160_000m, LedgerFamily.Budgetary), Cr("701-0000-2100", 160_000m)], OpenSep, Poster, At))
            .Should().Throw<LedgerException>();
    }

    [Fact]
    public void Closed_period_is_rejected()
    {
        var closed = new FiscalPeriod(2026, 8);
        closed.Close();
        FluentActions.Invoking(() => JournalEntry.Create("INV-1",
            [Dr("701-6000-53100-G-COPS-26", 1m), Cr("701-0000-2100", 1m)], closed, Poster, At))
            .Should().Throw<LedgerException>();
    }

    [Fact]
    public void Fewer_than_two_lines_is_rejected() =>
        FluentActions.Invoking(() => JournalEntry.Create("INV-1", [Dr("701-6000-53100-G-COPS-26", 1m)], OpenSep, Poster, At))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Line_with_both_debit_and_credit_is_rejected() =>
        FluentActions.Invoking(() => new JournalLine(AccountCode.Parse("101-6000-53100"), LedgerFamily.Financial,
                Money.Of(1), Money.Of(1), "bad"))
            .Should().Throw<LedgerException>();

    [Fact]
    public void FiscalPeriod_key_from_date() =>
        FiscalPeriod.KeyFor(new DateOnly(2026, 9, 21)).Should().Be((2026, 9));
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`

- [ ] **Step 3: Реализация**

```csharp
namespace GovErp.Domain.Ledger.Entities;
public enum EncumbranceStatus { Open, Closed }
public enum LedgerFamily { Financial, Budgetary }
public enum PeriodStatus { Open, Closed }
```

`Entities/EncumbranceLiquidation.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public sealed record EncumbranceLiquidation(Money Amount, string SourceRef);
```

`Entities/Encumbrance.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>Резерв бюджета под строку PO. Remaining = Original − Liquidated − Released.</summary>
public sealed class Encumbrance
{
    private readonly List<EncumbranceLiquidation> _liquidations = [];

    public Guid Id { get; private set; }
    public string PoLineRef { get; private set; }
    public AccountCode Account { get; private set; }
    public Money Original { get; private set; }
    public Money Liquidated { get; private set; }
    public Money Released { get; private set; }
    public EncumbranceStatus Status { get; private set; }
    public IReadOnlyList<EncumbranceLiquidation> Liquidations => _liquidations;

    public Money Remaining => Original - Liquidated - Released;

    public Encumbrance(string poLineRef, AccountCode account, Money original)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poLineRef);
        if (original <= Money.Zero)
        {
            throw new LedgerException($"Encumbrance amount must be positive, got {original}.");
        }

        Id = Guid.NewGuid();
        PoLineRef = poLineRef;
        Account = account;
        Original = original;
        Liquidated = Money.Zero;
        Released = Money.Zero;
        Status = EncumbranceStatus.Open;
    }

    private Encumbrance() { PoLineRef = null!; Account = null!; }

    public void Liquidate(Money amount, string sourceRef)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        if (Status == EncumbranceStatus.Closed)
        {
            throw new LedgerException($"Encumbrance {PoLineRef} is closed.");
        }

        if (amount <= Money.Zero || amount > Remaining)
        {
            throw new LedgerException($"Cannot liquidate {amount} from {PoLineRef}: remaining {Remaining}.");
        }

        Liquidated += amount;
        _liquidations.Add(new EncumbranceLiquidation(amount, sourceRef));
        if (Remaining.IsZero)
        {
            Status = EncumbranceStatus.Closed;
        }
    }

    public void ReleaseRemainder(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (Status == EncumbranceStatus.Closed)
        {
            throw new LedgerException($"Encumbrance {PoLineRef} is already closed.");
        }

        Released += Remaining;
        Status = EncumbranceStatus.Closed;
    }
}
```

`Entities/JournalLine.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

public sealed record JournalLine
{
    public AccountCode Account { get; }
    public LedgerFamily Family { get; }
    public Money Debit { get; }
    public Money Credit { get; }
    public string Description { get; }

    public JournalLine(AccountCode account, LedgerFamily family, Money debit, Money credit, string description)
    {
        if (debit.IsNegative || credit.IsNegative)
        {
            throw new LedgerException("Debit and credit cannot be negative.");
        }

        if (debit.IsZero == credit.IsZero)
        {
            throw new LedgerException("A journal line must have exactly one of debit or credit.");
        }

        Account = account;
        Family = family;
        Debit = debit;
        Credit = credit;
        Description = description;
    }
}
```

`Entities/FiscalPeriod.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

public sealed class FiscalPeriod
{
    public int Year { get; private set; }
    public int Month { get; private set; }
    public PeriodStatus Status { get; private set; }

    public FiscalPeriod(int year, int month)
    {
        if (month is < 1 or > 12)
        {
            throw new LedgerException($"Month {month} is out of range.");
        }

        Year = year;
        Month = month;
        Status = PeriodStatus.Open;
    }

    private FiscalPeriod() { }

    public bool IsOpen => Status == PeriodStatus.Open;

    public void Close()
    {
        if (!IsOpen)
        {
            throw new LedgerException($"Period {Year}-{Month:00} is already closed.");
        }

        Status = PeriodStatus.Closed;
    }

    public static (int Year, int Month) KeyFor(DateOnly date) => (date.Year, date.Month);
}
```

`Entities/JournalEntry.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>Проведённая запись журнала. Неизменяема после создания. Баланс — по каждому фонду и семейству счетов.</summary>
public sealed class JournalEntry
{
    private readonly List<JournalLine> _lines = [];

    public Guid Id { get; private set; }
    public string SourceRef { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public UserId PostedBy { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }
    public IReadOnlyList<JournalLine> Lines => _lines;

    private JournalEntry(string sourceRef, IEnumerable<JournalLine> lines, FiscalPeriod period, UserId postedBy, DateTimeOffset postedAt)
    {
        Id = Guid.NewGuid();
        SourceRef = sourceRef;
        PeriodYear = period.Year;
        PeriodMonth = period.Month;
        PostedBy = postedBy;
        PostedAt = postedAt;
        _lines.AddRange(lines);
    }

    private JournalEntry() { SourceRef = null!; }

    public static JournalEntry Create(string sourceRef, IReadOnlyList<JournalLine> lines, FiscalPeriod period,
        UserId postedBy, DateTimeOffset postedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        if (!period.IsOpen)
        {
            throw new LedgerException($"Period {period.Year}-{period.Month:00} is closed.");
        }

        if (lines.Count < 2)
        {
            throw new LedgerException("A journal entry needs at least two lines.");
        }

        foreach (var group in lines.GroupBy(l => (l.Account.Fund, l.Family)))
        {
            var debit = group.Aggregate(Money.Zero, (s, l) => s + l.Debit);
            var credit = group.Aggregate(Money.Zero, (s, l) => s + l.Credit);
            if (debit != credit)
            {
                throw new LedgerException(
                    $"Entry {sourceRef} is unbalanced for fund {group.Key.Fund} ({group.Key.Family}): Dr {debit} / Cr {credit}.");
            }
        }

        return new JournalEntry(sourceRef, lines, period, postedBy, postedAt);
    }
}
```

Репозитории, `namespace GovErp.Domain.Ledger.Repositories;`:
```csharp
using GovErp.Domain.Ledger.Entities;

public interface IBudgetLineRepository
{
    Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fiscalYear, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default);
    Task AddAsync(BudgetLine line, CancellationToken ct = default);
}

public interface IEncumbranceRepository
{
    Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default);
    Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default);
}

public interface IJournalRepository
{
    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default);
}

public interface IFiscalPeriodRepository
{
    Task<FiscalPeriod?> FindAsync(int year, int month, CancellationToken ct = default);
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`
Expected: 26 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Ledger tests/GovErp.Domain.Ledger.Tests
git commit -m "Ledger: Encumbrance, JournalEntry with per-fund balance, FiscalPeriod, ports

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 8: Payables — Vendor, PurchaseOrder, VendorInvoice

**Files:**
- Create: `src/GovErp.Domain.Payables/Entities/Vendor.cs`, `VendorStatus.cs`, `PurchaseOrder.cs`, `PurchaseOrderLine.cs`, `PurchaseOrderStatus.cs`, `VendorInvoice.cs`, `InvoiceDistribution.cs`, `InvoiceStatus.cs`, `InvoiceApproval.cs`, `ApprovalDecision.cs`, `InvoiceOverride.cs`, `ApproverRole.cs`
- Create: `src/GovErp.Domain.Payables/Repositories/IVendorRepository.cs`, `IPurchaseOrderRepository.cs`, `IVendorInvoiceRepository.cs`
- Create: `src/GovErp.Domain.Payables/Exceptions/PayablesException.cs`
- Test: `tests/GovErp.Domain.Payables.Tests/VendorInvoiceTests.cs`, `PurchaseOrderTests.cs`

**Interfaces:**
- Produces: `VendorInvoice` с методами `AddDistribution(AccountCode, Money, string? poLineRef)`, `RemoveDistribution(int lineNo)`, `Submit(Guid evaluationRef, IReadOnlyList<Guid> reservationIds)`, `RecordApproval(ApproverRole, UserId, Guid evaluationRef, DateTimeOffset)`, `MarkApproved()`, `Override(string ruleId, UserId, string reason, DateTimeOffset)`, `Reject(UserId, string reason)`, `ReturnToDraft()`, `Post(Guid evaluationRef, DateTimeOffset)`, `MarkPayable()`; свойства `Status`, `Version`, `Reference` (`INV-<Number>`), `Distributions`, `Approvals`, `Overrides`, `ReservationRefs`, `LastEvaluationRef`, `IsPoBacked`. `ApproverRole { DepartmentHead, GrantsManager, BudgetOfficer, FinanceDirector }`. `PurchaseOrder.LineRef(int lineNo) → "<Number>/<lineNo>"`.

- [ ] **Step 1: Тесты**

`PurchaseOrderTests.cs`:
```csharp
using GovErp.Domain.Payables.Entities;

namespace GovErp.Domain.Payables.Tests;

public class PurchaseOrderTests
{
    [Fact]
    public void Line_ref_is_number_slash_line()
    {
        var po = new PurchaseOrder("PO-2026-0451", Guid.NewGuid(),
            [new PurchaseOrderLine(1, AccountCode.Parse("701-3000-53100-G-COPS-26"), Money.Of(160_000m))]);
        po.LineRef(1).Should().Be("PO-2026-0451/1");
        po.Total.Should().Be(Money.Of(160_000m));
        po.Status.Should().Be(PurchaseOrderStatus.Open);
    }

    [Fact]
    public void Unknown_line_is_rejected()
    {
        var po = new PurchaseOrder("PO-1", Guid.NewGuid(), [new PurchaseOrderLine(1, AccountCode.Parse("101-6000-53100"), Money.Of(1))]);
        FluentActions.Invoking(() => po.LineRef(2)).Should().Throw<Exceptions.PayablesException>();
    }
}
```

`VendorInvoiceTests.cs`:
```csharp
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Tests;

public class VendorInvoiceTests
{
    private static readonly UserId Clerk = UserId.New();
    private static readonly UserId Chief = UserId.New();
    private static readonly Guid VendorId = Guid.NewGuid();
    private static readonly DateTimeOffset At = new(2026, 9, 21, 9, 0, 0, TimeSpan.Zero);
    private static readonly AccountCode Cops = AccountCode.Parse("701-6000-53100-G-COPS-26");

    private static VendorInvoice Draft(decimal total = 160_000m) =>
        new("V-7781", VendorId, new DateOnly(2026, 9, 15), Money.Of(total), poRef: null, Clerk, At);

    private static VendorInvoice Submitted()
    {
        var inv = Draft();
        inv.AddDistribution(Cops, Money.Of(160_000m), null);
        inv.Submit(Guid.NewGuid(), [Guid.NewGuid()]);
        return inv;
    }

    [Fact]
    public void New_invoice_is_draft_with_version_one()
    {
        var inv = Draft();
        inv.Status.Should().Be(InvoiceStatus.Draft);
        inv.Version.Should().Be(1);
        inv.Reference.Should().Be("INV-V-7781");
        inv.IsPoBacked.Should().BeFalse();
    }

    [Fact]
    public void Adding_distributions_bumps_version_and_numbers_lines()
    {
        var inv = Draft(30_000m);
        inv.AddDistribution(AccountCode.Parse("101-6000-53100"), Money.Of(12_000m), null);
        inv.AddDistribution(AccountCode.Parse("202-4000-53100"), Money.Of(8_000m), null);
        inv.Distributions.Select(d => d.LineNo).Should().Equal(1, 2);
        inv.Version.Should().Be(3);
        inv.DistributedTotal.Should().Be(Money.Of(20_000m));
    }

    [Fact]
    public void Remove_distribution_renumbers()
    {
        var inv = Draft(30_000m);
        inv.AddDistribution(AccountCode.Parse("101-6000-53100"), Money.Of(12_000m), null);
        inv.AddDistribution(AccountCode.Parse("202-4000-53100"), Money.Of(8_000m), null);
        inv.RemoveDistribution(1);
        inv.Distributions.Should().ContainSingle(d => d.LineNo == 1 && d.Amount == Money.Of(8_000m));
    }

    [Fact]
    public void Submit_requires_distributions_to_equal_total()
    {
        var inv = Draft();
        inv.AddDistribution(Cops, Money.Of(150_000m), null);
        FluentActions.Invoking(() => inv.Submit(Guid.NewGuid(), [])).Should().Throw<PayablesException>().WithMessage("*160,000*");
    }

    [Fact]
    public void Submit_moves_to_submitted_and_keeps_refs()
    {
        var eval = Guid.NewGuid();
        var res = Guid.NewGuid();
        var inv = Draft();
        inv.AddDistribution(Cops, Money.Of(160_000m), null);
        inv.Submit(eval, [res]);
        inv.Status.Should().Be(InvoiceStatus.Submitted);
        inv.LastEvaluationRef.Should().Be(eval);
        inv.ReservationRefs.Should().Equal(res);
    }

    [Fact]
    public void Submitted_invoice_cannot_be_edited()
    {
        var inv = Submitted();
        FluentActions.Invoking(() => inv.AddDistribution(Cops, Money.Of(1), null)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => inv.RemoveDistribution(1)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Author_cannot_approve()
    {
        var inv = Submitted();
        FluentActions.Invoking(() => inv.RecordApproval(ApproverRole.DepartmentHead, Clerk, Guid.NewGuid(), At))
            .Should().Throw<PayablesException>().WithMessage("*separation of duties*");
    }

    [Fact]
    public void Same_role_cannot_approve_twice()
    {
        var inv = Submitted();
        inv.RecordApproval(ApproverRole.DepartmentHead, Chief, Guid.NewGuid(), At);
        FluentActions.Invoking(() => inv.RecordApproval(ApproverRole.DepartmentHead, Chief, Guid.NewGuid(), At))
            .Should().Throw<PayablesException>();
        inv.Approvals.Should().ContainSingle(a => a.Role == ApproverRole.DepartmentHead && a.Decision == ApprovalDecision.Approved);
    }

    [Fact]
    public void MarkApproved_requires_submitted()
    {
        var inv = Submitted();
        inv.MarkApproved();
        inv.Status.Should().Be(InvoiceStatus.Approved);
        FluentActions.Invoking(() => Draft().MarkApproved()).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Override_is_recorded_only_while_submitted()
    {
        var inv = Submitted();
        inv.Override("PROCUREMENT_THRESHOLD", Chief, "Sole-source justification on file", At);
        inv.Overrides.Should().ContainSingle(o => o.RuleId == "PROCUREMENT_THRESHOLD" && o.UserId == Chief);
        FluentActions.Invoking(() => Draft().Override("X", Chief, "r", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => inv.Override("X", Chief, " ", At)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Post_requires_approved_and_records_evaluation()
    {
        var inv = Submitted();
        FluentActions.Invoking(() => inv.Post(Guid.NewGuid(), At)).Should().Throw<PayablesException>();
        inv.MarkApproved();
        var eval = Guid.NewGuid();
        inv.Post(eval, At);
        inv.Status.Should().Be(InvoiceStatus.Posted);
        inv.LastEvaluationRef.Should().Be(eval);
        inv.PostedAt.Should().Be(At);
    }

    [Fact]
    public void MarkPayable_requires_posted()
    {
        var inv = Submitted();
        inv.MarkApproved();
        inv.Post(Guid.NewGuid(), At);
        inv.MarkPayable();
        inv.Status.Should().Be(InvoiceStatus.Payable);
    }

    [Fact]
    public void Reject_from_submitted_or_approved_then_return_to_draft()
    {
        var inv = Submitted();
        inv.Reject(Chief, "wrong coding");
        inv.Status.Should().Be(InvoiceStatus.Rejected);
        inv.Approvals.Should().ContainSingle(a => a.Decision == ApprovalDecision.Rejected);
        inv.ReturnToDraft();
        inv.Status.Should().Be(InvoiceStatus.Draft);
        inv.ReservationRefs.Should().BeEmpty();
        inv.Approvals.Should().BeEmpty();
        inv.Overrides.Should().BeEmpty();
    }

    [Fact]
    public void Posted_invoice_cannot_be_rejected()
    {
        var inv = Submitted();
        inv.MarkApproved();
        inv.Post(Guid.NewGuid(), At);
        FluentActions.Invoking(() => inv.Reject(Chief, "late")).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Po_backed_invoice_exposes_flag()
    {
        var inv = new VendorInvoice("V-9", VendorId, new DateOnly(2026, 9, 15), Money.Of(1), "PO-2026-0451", Clerk, At);
        inv.IsPoBacked.Should().BeTrue();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Payables.Tests`

- [ ] **Step 3: Реализация**

`Exceptions/PayablesException.cs`:
```csharp
namespace GovErp.Domain.Payables.Exceptions;

public sealed class PayablesException(string message) : Exception(message);
```

Перечисления, `namespace GovErp.Domain.Payables.Entities;`:
```csharp
public enum VendorStatus { Active, Debarred }
public enum PurchaseOrderStatus { Open, Closed, Cancelled }
public enum InvoiceStatus { Draft, Submitted, Approved, Posted, Payable, Rejected }
public enum ApprovalDecision { Approved, Rejected }
public enum ApproverRole { DepartmentHead, GrantsManager, BudgetOfficer, FinanceDirector }
```

`Entities/Vendor.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed class Vendor
{
    public Guid Id { get; private set; }
    public string Code { get; private set; }
    public string Name { get; private set; }
    public VendorStatus Status { get; private set; }
    public bool SamRegistered { get; private set; }

    public Vendor(Guid id, string code, string name, VendorStatus status, bool samRegistered)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Id = id;
        Code = code;
        Name = name;
        Status = status;
        SamRegistered = samRegistered;
    }

    private Vendor() { Code = null!; Name = null!; }
}
```

`Entities/PurchaseOrderLine.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record PurchaseOrderLine(int LineNo, AccountCode Account, Money Amount);
```

`Entities/PurchaseOrder.cs`:
```csharp
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderLine> _lines = [];

    public Guid Id { get; private set; }
    public string Number { get; private set; }
    public Guid VendorId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    public Money Total => _lines.Aggregate(Money.Zero, (s, l) => s + l.Amount);

    public PurchaseOrder(string number, Guid vendorId, IReadOnlyList<PurchaseOrderLine> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (lines.Count == 0)
        {
            throw new PayablesException("Purchase order needs at least one line.");
        }

        Id = Guid.NewGuid();
        Number = number;
        VendorId = vendorId;
        Status = PurchaseOrderStatus.Open;
        _lines.AddRange(lines);
    }

    private PurchaseOrder() { Number = null!; }

    public string LineRef(int lineNo)
    {
        if (_lines.All(l => l.LineNo != lineNo))
        {
            throw new PayablesException($"PO {Number} has no line {lineNo}.");
        }

        return $"{Number}/{lineNo}";
    }
}
```

`Entities/InvoiceDistribution.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceDistribution(int LineNo, AccountCode Account, Money Amount, string? PoLineRef);
```

`Entities/InvoiceApproval.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceApproval(ApproverRole Role, UserId UserId, ApprovalDecision Decision, Guid? EvaluationRef, string? Reason, DateTimeOffset At);
```

`Entities/InvoiceOverride.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceOverride(string RuleId, UserId UserId, string Reason, DateTimeOffset At);
```

`Entities/VendorInvoice.cs`:
```csharp
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// AP-инвойс: header + distributions + согласования + статус.
/// Draft → Submitted → Approved → Posted → Payable; Rejected из Submitted/Approved; Rejected → Draft.
/// </summary>
public sealed class VendorInvoice
{
    private readonly List<InvoiceDistribution> _distributions = [];
    private readonly List<InvoiceApproval> _approvals = [];
    private readonly List<InvoiceOverride> _overrides = [];
    private readonly List<Guid> _reservationRefs = [];

    public Guid Id { get; private set; }
    public string Number { get; private set; }
    public Guid VendorId { get; private set; }
    public DateOnly InvoiceDate { get; private set; }
    public Money Total { get; private set; }
    public string? PoRef { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public UserId CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }
    public Guid? LastEvaluationRef { get; private set; }
    /// <summary>Растёт при каждом изменении содержимого; пишется в EvaluationRecord.TransactionVersion.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<InvoiceDistribution> Distributions => _distributions;
    public IReadOnlyList<InvoiceApproval> Approvals => _approvals;
    public IReadOnlyList<InvoiceOverride> Overrides => _overrides;
    public IReadOnlyList<Guid> ReservationRefs => _reservationRefs;

    public string Reference => $"INV-{Number}";
    public bool IsPoBacked => PoRef is not null;
    public Money DistributedTotal => _distributions.Aggregate(Money.Zero, (s, d) => s + d.Amount);

    public VendorInvoice(string number, Guid vendorId, DateOnly invoiceDate, Money total, string? poRef,
        UserId createdBy, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (total <= Money.Zero)
        {
            throw new PayablesException($"Invoice total must be positive, got {total}.");
        }

        Id = Guid.NewGuid();
        Number = number;
        VendorId = vendorId;
        InvoiceDate = invoiceDate;
        Total = total;
        PoRef = string.IsNullOrWhiteSpace(poRef) ? null : poRef;
        Status = InvoiceStatus.Draft;
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Version = 1;
    }

    private VendorInvoice() { Number = null!; }

    public void AddDistribution(AccountCode account, Money amount, string? poLineRef)
    {
        RequireStatus(InvoiceStatus.Draft, "add a distribution");
        if (amount <= Money.Zero)
        {
            throw new PayablesException($"Distribution amount must be positive, got {amount}.");
        }

        _distributions.Add(new InvoiceDistribution(_distributions.Count + 1, account, amount, poLineRef));
        Version++;
    }

    public void RemoveDistribution(int lineNo)
    {
        RequireStatus(InvoiceStatus.Draft, "remove a distribution");
        var removed = _distributions.RemoveAll(d => d.LineNo == lineNo);
        if (removed == 0)
        {
            throw new PayablesException($"Invoice {Reference} has no line {lineNo}.");
        }

        for (var i = 0; i < _distributions.Count; i++)
        {
            _distributions[i] = _distributions[i] with { LineNo = i + 1 };
        }

        Version++;
    }

    public void Submit(Guid evaluationRef, IReadOnlyList<Guid> reservationIds)
    {
        RequireStatus(InvoiceStatus.Draft, "submit");
        if (_distributions.Count == 0)
        {
            throw new PayablesException($"Invoice {Reference} has no distributions.");
        }

        if (DistributedTotal != Total)
        {
            throw new PayablesException($"Distributions {DistributedTotal} do not equal invoice total {Total}.");
        }

        Status = InvoiceStatus.Submitted;
        LastEvaluationRef = evaluationRef;
        _reservationRefs.Clear();
        _reservationRefs.AddRange(reservationIds);
    }

    public void RecordApproval(ApproverRole role, UserId userId, Guid evaluationRef, DateTimeOffset at)
    {
        RequireStatus(InvoiceStatus.Submitted, "approve");
        if (userId == CreatedBy)
        {
            throw new PayablesException($"User {userId} created {Reference} and cannot approve it (separation of duties).");
        }

        if (_approvals.Any(a => a.Role == role && a.Decision == ApprovalDecision.Approved))
        {
            throw new PayablesException($"Role {role} has already approved {Reference}.");
        }

        _approvals.Add(new InvoiceApproval(role, userId, ApprovalDecision.Approved, evaluationRef, null, at));
        LastEvaluationRef = evaluationRef;
    }

    public void MarkApproved()
    {
        RequireStatus(InvoiceStatus.Submitted, "mark approved");
        Status = InvoiceStatus.Approved;
    }

    public void Override(string ruleId, UserId userId, string reason, DateTimeOffset at)
    {
        RequireStatus(InvoiceStatus.Submitted, "override");
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new PayablesException("An override requires a reason.");
        }

        _overrides.Add(new InvoiceOverride(ruleId, userId, reason, at));
    }

    public void Reject(UserId userId, string reason)
    {
        if (Status is not (InvoiceStatus.Submitted or InvoiceStatus.Approved))
        {
            throw new PayablesException($"Invoice {Reference} is {Status} and cannot be rejected.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _approvals.Add(new InvoiceApproval(ApproverRole.DepartmentHead, userId, ApprovalDecision.Rejected, LastEvaluationRef, reason, DateTimeOffset.UtcNow));
        Status = InvoiceStatus.Rejected;
    }

    public void ReturnToDraft()
    {
        RequireStatus(InvoiceStatus.Rejected, "return to draft");
        Status = InvoiceStatus.Draft;
        _approvals.Clear();
        _overrides.Clear();
        _reservationRefs.Clear();
        LastEvaluationRef = null;
        Version++;
    }

    public void Post(Guid evaluationRef, DateTimeOffset at)
    {
        RequireStatus(InvoiceStatus.Approved, "post");
        Status = InvoiceStatus.Posted;
        LastEvaluationRef = evaluationRef;
        PostedAt = at;
    }

    public void MarkPayable()
    {
        RequireStatus(InvoiceStatus.Posted, "mark payable");
        Status = InvoiceStatus.Payable;
    }

    private void RequireStatus(InvoiceStatus expected, string action)
    {
        if (Status != expected)
        {
            throw new PayablesException($"Cannot {action}: invoice {Reference} is {Status}, expected {expected}.");
        }
    }
}
```

`Reject` записывает роль `DepartmentHead` как заглушку — роль отклонившего в этой версии не различается; `InvoiceApproval.Reason` хранит причину. Если план 3 передаст роль явно — добавить параметр.

Репозитории, `namespace GovErp.Domain.Payables.Repositories;`:
```csharp
using GovErp.Domain.Payables.Entities;

public interface IVendorRepository
{
    Task<Vendor?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<Vendor>> ListAsync(CancellationToken ct = default);
}

public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder?> FindByNumberAsync(string number, CancellationToken ct = default);
}

public interface IVendorInvoiceRepository
{
    Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default);
    Task<bool> ExistsAsync(Guid vendorId, string number, CancellationToken ct = default);
    Task AddAsync(VendorInvoice invoice, CancellationToken ct = default);
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Payables.Tests`
Expected: 17 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Payables tests/GovErp.Domain.Payables.Tests
git commit -m "Payables: Vendor, PurchaseOrder, VendorInvoice state machine and ports

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
```

---

### Task 9: Архитектурные тесты

**Files:**
- Create: `tests/GovErp.Architecture.Tests/LayerReferenceTests.cs`, `DomainPurityTests.cs`, `ArchitectureFixture.cs`

**Interfaces:**
- Consumes: сборки всех проектов.
- Produces: исполняемая проверка `CA-2`, `CA-7`, `DDD-6`, `DDD-7.2`, `DDD-13.1`. План 3 добавит сюда тест `CA-10` (Application не отдаёт `Entities`).

- [ ] **Step 1: Фикстура со списком сборок**

`ArchitectureFixture.cs`:
```csharp
using System.Reflection;

namespace GovErp.Architecture.Tests;

public static class ArchitectureFixture
{
    public static readonly Assembly Shared = typeof(Domain.Shared.ValueObjects.Money).Assembly;
    public static readonly Assembly ChartOfAccounts = typeof(Domain.ChartOfAccounts.Entities.Fund).Assembly;
    public static readonly Assembly Ledger = typeof(Domain.Ledger.Entities.BudgetLine).Assembly;
    public static readonly Assembly Payables = typeof(Domain.Payables.Entities.VendorInvoice).Assembly;

    public static IEnumerable<Assembly> DomainContexts => [ChartOfAccounts, Ledger, Payables];
    public static IEnumerable<Assembly> AllDomain => [Shared, ChartOfAccounts, Ledger, Payables];

    public static readonly string[] ForbiddenInDomain =
    [
        "Microsoft.EntityFrameworkCore", "Microsoft.AspNetCore", "Microsoft.Extensions", "System.Data"
    ];
}
```

Сборка `GovErp.Domain.Validation` пока пуста — в фикстуру добавит план 2.

- [ ] **Step 2: Тесты границ**

`LayerReferenceTests.cs`:
```csharp
using NetArchTest.Rules;

namespace GovErp.Architecture.Tests;

public class LayerReferenceTests
{
    [Fact]
    public void Domain_assemblies_do_not_reference_frameworks()
    {
        foreach (var assembly in ArchitectureFixture.AllDomain)
        {
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();
            referenced.Should().NotContain(n => ArchitectureFixture.ForbiddenInDomain.Any(f => n.StartsWith(f)),
                because: $"{assembly.GetName().Name} must depend only on BCL and Domain.Shared");
        }
    }

    [Fact]
    public void Domain_contexts_do_not_reference_each_other()
    {
        var contextNames = ArchitectureFixture.DomainContexts.Select(a => a.GetName().Name!).ToList();
        foreach (var assembly in ArchitectureFixture.DomainContexts)
        {
            var referenced = assembly.GetReferencedAssemblies().Select(a => a.Name!);
            referenced.Should().NotContain(n => contextNames.Contains(n) && n != assembly.GetName().Name,
                because: $"{assembly.GetName().Name} is a bounded context (DDD-6)");
        }
    }

    [Fact]
    public void Shared_kernel_has_no_entities_or_ports()
    {
        var result = Types.InAssembly(ArchitectureFixture.Shared)
            .That().ArePublic()
            .Should().ResideInNamespace("GovErp.Domain.Shared.ValueObjects")
            .GetResult();
        result.IsSuccessful.Should().BeTrue(because: string.Join(", ", result.FailingTypeNames ?? []));
    }
}
```

`DomainPurityTests.cs`:
```csharp
using System.Reflection;
using System.Runtime.CompilerServices;

namespace GovErp.Architecture.Tests;

public class DomainPurityTests
{
    [Fact]
    public void Value_objects_are_immutable()
    {
        var offenders = ArchitectureFixture.AllDomain
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.EndsWith(".ValueObjects") == true && t.IsPublic && !t.IsEnum)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } set
                            && !set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        offenders.Should().BeEmpty(because: "value objects must not have public setters (DDD-7.2)");
    }

    [Fact]
    public void Domain_services_have_no_mutable_instance_fields()
    {
        var offenders = ArchitectureFixture.AllDomain
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.Contains(".DomainServices") == true && t.IsClass)
            .SelectMany(t => t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(f => !f.IsInitOnly)
                .Select(f => $"{t.Name}.{f.Name}"))
            .ToList();

        offenders.Should().BeEmpty(because: "domain services are stateless (DDD-13.1)");
    }

    [Fact]
    public void Entities_have_no_public_setters()
    {
        var offenders = ArchitectureFixture.DomainContexts
            .SelectMany(a => a.GetTypes())
            .Where(t => t.Namespace?.EndsWith(".Entities") == true && t.IsClass && t.IsPublic)
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.SetMethod is { IsPublic: true } set
                            && !set.ReturnParameter.GetRequiredCustomModifiers().Contains(typeof(IsExternalInit)))
                .Select(p => $"{t.Name}.{p.Name}"))
            .ToList();

        offenders.Should().BeEmpty(because: "state changes only through methods (DDD-8); records with init are fine");
    }

    [Fact]
    public void Forbidden_names_are_absent_in_domain()
    {
        string[] forbidden = ["Handler", "Manager", "Store", "Registry", "Vm", "Dto", "View"];
        var offenders = ArchitectureFixture.AllDomain
            .SelectMany(a => a.GetTypes())
            .Where(t => t.IsPublic && forbidden.Any(f => t.Name.EndsWith(f)))
            .Select(t => t.FullName)
            .ToList();

        offenders.Should().BeEmpty(because: "NM-12 / NM-13");
    }
}
```

- [ ] **Step 3: Прогнать**

Run: `dotnet test tests/GovErp.Architecture.Tests`
Expected: 7 passed.

- [ ] **Step 4: Прогнать всё решение**

Run: `dotnet test`
Expected: все проекты зелёные; общий счёт ≈ 80 тестов.

- [ ] **Step 5: Commit и push**

```bash
git add tests/GovErp.Architecture.Tests
git commit -m "Architecture tests: layer references, context isolation, domain purity

Co-Authored-By: Claude Opus 5 <noreply@anthropic.com>"
git push
```

---

## Self-review

**Покрытие спеки (разделы 3.1–3.4):** Shared kernel — задачи 2–3 (все VO из 3.1). ChartOfAccounts — задачи 4–5 (Fund, Department, ObjectCodeDefinition, Grant, AccountCombination; CombinationRule — сознательно исключён, спека правится в задаче 1). Ledger — задачи 6–7 (BudgetLine с Reserve/Commit/Release/Amend, Encumbrance, JournalEntry с балансом по фонду, FiscalPeriod, четыре порта). Payables — задача 8 (Vendor, PurchaseOrder, VendorInvoice с конечным автоматом, три порта). Раздел 6.1 (структура решения, `Directory.Build.props`) — задача 1. Раздел 7 (архитектурные тесты, кроме `CA-10`) — задача 9.

**Не покрыто этим планом (намеренно):** Validation (план 2), `IAuditTrail`, `ITenantContext`, EF, seed, UI.

**Согласованность имён между задачами:** `BudgetControlMode` существует в двух контекстах (ChartOfAccounts и Ledger) — намеренно, маппинг в Application. `ReservationResult.ReservationId` — `Guid?`; `VendorInvoice.Submit` принимает `IReadOnlyList<Guid>`. `JournalLine` — positional-подобный record с проверкой в конструкторе; `JournalEntry.Create` принимает `IReadOnlyList<JournalLine>`. `FiscalPeriod` — сущность (не VO), у `JournalEntry` — `PeriodYear`/`PeriodMonth`. Тестовые счета AP `701-0000-2100` требуют `DepartmentCode("0000")` — формат `^[0-9]{4}$` допускает.
