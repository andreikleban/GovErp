# План 1: Foundation + Domain (Shared, ChartOfAccounts, Ledger, Payables)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Собрать решение с проверяемыми границами слоёв и реализовать три доменных контекста (ChartOfAccounts, Ledger, Payables) поверх общего ядра значений — с тестами всех инвариантов, без базы и без UI.

**Architecture:** Каждый слой и каждый контекст — отдельный проект; транзитивные ссылки отключены, граф ссылок проверяется NetArchTest. Домены чистые (только BCL + `GovErp.Domain.Shared`). Агрегаты — классы с закрытыми сеттерами и методами-переходами; значения — `record`/`record struct` с самопроверкой в конструкторе. Репозитории — только интерфейсы в `Repositories/`.

**Tech Stack:** .NET 10 (SDK 10.0.401), C# 14, xUnit 2.9, FluentAssertions 7, NetArchTest.Rules 1.3, Central Package Management.

**Spec:** `docs/superpowers/specs/2026-09-21-validation-engine-design.md` (разделы 2.5, 3.1–3.4, 5 «Завершённые правила жизненного цикла», 6.1, 7). Манифест: `docs/ARCHITECTURE.md`.

**Следующие планы:** План 2 — контекст Validation (конвейер, правила). План 3 — Infrastructure + Application + Docker. План 4 — Blazor UI + Explanation.

## Global Constraints

- Уточнения из `2026-09-22-plan-consistency.md` (§3, §4, §6) и правила жизненного цикла spec §5 уже внесены в задачи 6–8: резервы и claims принадлежат `InvoiceId + ContentVersion`, `ContentVersion` отделён от SQL `RowVersion`, `ApprovalCycleId` отделяет повторные согласования, история approvals/overrides не удаляется, статусов `Rejected`/`Payable` нет.
- Демо-дата документа — 2026-06-15 (FY2026); в тестах использовать июнь 2026, закрытый период — май 2026.
- `TargetFramework=net10.0`, `LangVersion=latest`, `Nullable=enable`, `ImplicitUsings=enable`, `TreatWarningsAsErrors=true`, `DisableTransitiveProjectReferences=true` — в `Directory.Build.props` для всех проектов.
- Версии пакетов — только в `Directory.Packages.props` (`ManagePackageVersionsCentrally=true`).
- Имена проектов: `GovErp.Domain.<Context>`, `GovErp.Application.Web`, `GovErp.Infrastructure`, `GovErp.Web`; тесты — `tests/GovErp.<Проект>.Tests` (`NM-1`, `NM-7`).
- Внутри Domain-проекта папки: `Entities/`, `ValueObjects/`, `Repositories/`, `DomainServices/`, `Exceptions/` (`NM-2`). Пространство имён = путь папки, один публичный тип — один файл (`NM-5`).
- Domain-проекты ссылаются только на `GovErp.Domain.Shared` и BCL. Ни одного NuGet-пакета в Domain.
- В слое правил запрещены имена с `Ui`, `View`, `Vm`, `Dto` (`NM-13`); суффикс `Service` — только в `DomainServices/` (`NM-11`); `Handler`, `Manager`, `Store`, `Registry` — нигде (`NM-12`).
- Деньги — `Money` (decimal, не более 2 знаков — иначе `ArgumentException`, без округления); суммы distribution/бюджета/резервов ≥ 0; отрицательные `Money` допустимы только как дельты и результаты вычислений.
- Финансовый год: с 1 июля; `FY2026` = 2025-07-01 … 2026-06-30.
- Коммит после каждой задачи; сообщения на английском, в конце — `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.

**Решение спеки §3.2, которое план выполняет:** сущность `CombinationRule` и `ICombinationRuleRepository` не реализуются — правила «Grant обязателен для 701» и «202 только с 4000» уже выражены атрибутами `Fund` (`GrantPolicy`, `AllowedDepartments`, `AllowedObjects`), а допустимость адреса — whitelist `AccountCombination`. Отдельная таблица правил комбинаций дублировала бы это (`DDD-15`).

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
    Entities/OpeningBalance.cs, BudgetLine.cs, BudgetAmendment.cs, BudgetReservation.cs, ReservationStatus.cs, ReservationResult.cs, BudgetControlMode.cs
    Entities/Encumbrance.cs, EncumbranceClaim.cs, ClaimStatus.cs, EncumbranceStatus.cs
    Entities/JournalEntry.cs, JournalLine.cs, LedgerFamily.cs, PostingKind.cs
    Entities/FiscalPeriod.cs, PeriodStatus.cs
    Repositories/IBudgetLineRepository.cs, IEncumbranceRepository.cs, IOpeningBalanceRepository.cs, IJournalRepository.cs, IFiscalPeriodRepository.cs
    Exceptions/LedgerException.cs, BudgetConcurrencyException.cs
  GovErp.Domain.Payables/
    Entities/Vendor.cs, VendorStatus.cs
    Entities/PurchaseOrder.cs, PurchaseOrderLine.cs, PoBillingClaim.cs, ClaimStatus.cs, PurchaseOrderStatus.cs
    Entities/VendorInvoice.cs, InvoiceDates.cs, DistributionInput.cs, InvoiceDistribution.cs, InvoiceStatus.cs, InvoiceApproval.cs, ApprovalDecision.cs, InvoiceOverride.cs, ApproverRole.cs
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

- [ ] **Step 5: Commit**

```bash
git add -A
git commit -m "Scaffold solution: layers, contexts, reference graph, CPM

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
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
    public void Accepts_up_to_two_decimals()
    {
        Money.Of(1.5m).Amount.Should().Be(1.50m);
        Money.Of(160_000.25m).Amount.Should().Be(160_000.25m);
    }

    [Theory]
    [InlineData("1.005")]
    [InlineData("0.001")]
    public void Rejects_more_than_two_decimals(string value) =>
        FluentActions.Invoking(() => Money.Of(decimal.Parse(value, System.Globalization.CultureInfo.InvariantCulture)))
            .Should().Throw<ArgumentException>();

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

/// <summary>Денежная сумма в USD с точностью до цента. Больше двух знаков — ошибка, не округление. Знак допустим (дельты, результаты вычислений).</summary>
public readonly record struct Money : IComparable<Money>
{
    public decimal Amount { get; }

    public Money(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
        {
            throw new ArgumentException($"Amount {amount} has more than two decimal places; USD amounts are not rounded silently.", nameof(amount));
        }

        Amount = amount;
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
Expected: 17 passed (xUnit считает каждую строку `InlineData` отдельным тестом).

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Shared tests/GovErp.Domain.Shared.Tests
git commit -m "Shared kernel: Money and segment codes

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
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

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
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

    private static readonly DateOnly InPeriod = new(2026, 6, 15);

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

    public GrantEligibility CheckEligibility(DateOnly serviceDate, DepartmentCode department, ObjectCode objectCode)
    {
        if (Status != GrantStatus.Active)
        {
            return GrantEligibility.GrantNotActive;
        }

        if (!Period.Contains(serviceDate))
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

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
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
    private static readonly DateTimeOffset Now = new(2026, 6, 15, 10, 0, 0, TimeSpan.Zero);
    private static readonly AccountCode Code = AccountCode.Parse("701-6000-53100-G-COPS-26");

    private static AccountCombination Pending() =>
        AccountCombination.Request(Code, new DateOnly(2025, 7, 1), Clerk, Now, CombinationSource.Manual);

    [Fact]
    public void Requested_combination_is_pending_and_not_active()
    {
        var c = Pending();
        c.Status.Should().Be(CombinationStatus.Pending);
        c.IsActiveOn(new DateOnly(2026, 6, 15)).Should().BeFalse();
    }

    [Fact]
    public void Approve_activates_and_records_approver()
    {
        var c = Pending();
        c.Approve(Controller, Now);
        c.Status.Should().Be(CombinationStatus.Active);
        c.ApprovedBy.Should().Be(Controller);
        c.ApprovedAt.Should().Be(Now);
        c.IsActiveOn(new DateOnly(2026, 6, 15)).Should().BeTrue();
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

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 6: Ledger — OpeningBalance и BudgetLine с собственными резервами

**Files:**
- Create: `src/GovErp.Domain.Ledger/Entities/OpeningBalance.cs`, `BudgetLine.cs`, `BudgetAmendment.cs`, `BudgetReservation.cs`, `ReservationStatus.cs`, `ReservationResult.cs`, `BudgetControlMode.cs`
- Create: `src/GovErp.Domain.Ledger/Exceptions/LedgerException.cs`, `BudgetConcurrencyException.cs`
- Test: `tests/GovErp.Domain.Ledger.Tests/BudgetLineTests.cs`

**Interfaces:**
- Produces: `OpeningBalance(AccountCode, FiscalYear, DateOnly asOfDate, Money initialActuals, Money initialEncumbered, string sourceReference)`; `BudgetLine.Open(OpeningBalance, BudgetControlMode, Money adopted)`; свойства `Account`, `FiscalYear`, `ControlMode`, `Adopted`, `Amended`, `Actuals`, `Encumbered`, `Held`, `Available`, `Amendments`, `Reservations`, `ChangeStamp`; методы `HeldFor(Guid invoiceId, int contentVersion)`, `AvailableFor(Guid invoiceId, int contentVersion)`, `Amend(Money, string, DateOnly)`, `Reserve(Guid invoiceId, int contentVersion, Money amount) → ReservationResult`, `Commit(Guid reservationId)`, `Release(Guid reservationId)`, `ReleaseAllFor(Guid invoiceId) → int`, `RecordLiquidation(Money)`; `ReservationResult(IsReserved, ReservationId, AvailableBefore, Shortfall, IsOverage)`; `BudgetConcurrencyException(AccountCode, FiscalYear)`.
- `BudgetControlMode` в Ledger — **свой** enum, не из ChartOfAccounts (контексты не ссылаются друг на друга; маппинг — в Application, план 3).
- Правила из spec §3.3: `Available = Amended − Actuals − Encumbered − ΣHeld`; `AvailableFor = Available + OwnHeld` — собственный резерв не вычитается повторно при перевалидации на Approve/Post. `RecordLiquidation` переносит сумму из `Encumbered` в `Actuals` (available не меняется). Каждое изменение увеличивает `ChangeStamp` — это гарантирует UPDATE строки владельца и срабатывание `rowversion`, даже когда меняется только owned-коллекция резервов (план 3).

- [ ] **Step 1: Тесты**

`tests/GovErp.Domain.Ledger.Tests/BudgetLineTests.cs`:
```csharp
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class BudgetLineTests
{
    private static readonly AccountCode Cops = AccountCode.Parse("701-6000-53100-G-COPS-26");
    private static readonly FiscalYear Fy = new(2026);
    private static readonly DateOnly Jun15 = new(2026, 6, 15);
    private static readonly Guid InvoiceA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid InvoiceB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    /// <summary>Строка задания: 375,000 − 132,000 − 96,000 = 147,000.</summary>
    private static BudgetLine ExerciseLine(BudgetControlMode mode = BudgetControlMode.Hard) =>
        BudgetLine.Open(
            new OpeningBalance(Cops, Fy, new DateOnly(2026, 6, 1), Money.Of(132_000m), Money.Of(96_000m), "FY2026 opening load"),
            mode, adopted: Money.Of(375_000m));

    [Fact]
    public void Opens_from_opening_balance()
    {
        var line = ExerciseLine();
        line.Actuals.Should().Be(Money.Of(132_000m));
        line.Encumbered.Should().Be(Money.Of(96_000m));
        line.Amended.Should().Be(Money.Of(375_000m));
        line.Available.Should().Be(Money.Of(147_000m));
        line.ChangeStamp.Should().Be(0);
    }

    [Fact]
    public void Opening_balance_rejects_negative_amounts() =>
        FluentActions.Invoking(() => new OpeningBalance(Cops, Fy, Jun15, Money.Of(-1m), Money.Zero, "x"))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Hard_control_refuses_reservation_over_available()
    {
        var line = ExerciseLine();
        var result = line.Reserve(InvoiceA, 1, Money.Of(160_000m));
        result.IsReserved.Should().BeFalse();
        result.Shortfall.Should().Be(Money.Of(13_000m));
        result.AvailableBefore.Should().Be(Money.Of(147_000m));
        line.Held.Should().Be(Money.Zero);
        line.Reservations.Should().BeEmpty();
        line.ChangeStamp.Should().Be(0);
    }

    [Fact]
    public void Soft_control_reserves_with_overage_flag()
    {
        var line = ExerciseLine(BudgetControlMode.Soft);
        var result = line.Reserve(InvoiceA, 1, Money.Of(160_000m));
        result.IsReserved.Should().BeTrue();
        result.IsOverage.Should().BeTrue();
        line.Reservations.Single().IsOverage.Should().BeTrue();
        line.Held.Should().Be(Money.Of(160_000m));
        line.Available.Should().Be(Money.Of(-13_000m));
    }

    [Fact]
    public void Reservation_reduces_available_for_other_invoices_only()
    {
        var line = ExerciseLine();
        line.Reserve(InvoiceA, 1, Money.Of(100_000m)).IsReserved.Should().BeTrue();
        line.Available.Should().Be(Money.Of(47_000m));
        line.HeldFor(InvoiceA, 1).Should().Be(Money.Of(100_000m));
        line.AvailableFor(InvoiceA, 1).Should().Be(Money.Of(147_000m));   // свой резерв не считается дважды
        line.AvailableFor(InvoiceB, 1).Should().Be(Money.Of(47_000m));
        line.Reserve(InvoiceB, 1, Money.Of(100_000m)).IsReserved.Should().BeFalse();
    }

    [Fact]
    public void Held_of_another_content_version_is_not_own()
    {
        var line = ExerciseLine();
        line.Reserve(InvoiceA, 1, Money.Of(100_000m));
        line.HeldFor(InvoiceA, 2).Should().Be(Money.Zero);
        line.AvailableFor(InvoiceA, 2).Should().Be(Money.Of(47_000m));
    }

    [Fact]
    public void Second_reservation_for_same_invoice_version_is_rejected()
    {
        var line = ExerciseLine();
        line.Reserve(InvoiceA, 1, Money.Of(10_000m));
        FluentActions.Invoking(() => line.Reserve(InvoiceA, 1, Money.Of(10_000m)))
            .Should().Throw<LedgerException>().WithMessage("*already*");
    }

    [Fact]
    public void Amend_raises_amended_and_available()
    {
        var line = ExerciseLine();
        line.Amend(Money.Of(13_000m), "BA-2026-14", Jun15);
        line.Amended.Should().Be(Money.Of(388_000m));
        line.Available.Should().Be(Money.Of(160_000m));
        line.Amendments.Should().ContainSingle(a => a.Reference == "BA-2026-14" && a.Amount == Money.Of(13_000m));
        line.Reserve(InvoiceA, 1, Money.Of(160_000m)).IsReserved.Should().BeTrue();
    }

    [Fact]
    public void Commit_moves_held_to_actuals()
    {
        var line = ExerciseLine();
        var r = line.Reserve(InvoiceA, 1, Money.Of(100_000m));
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
        var r = line.Reserve(InvoiceA, 1, Money.Of(100_000m));
        line.Commit(r.ReservationId!.Value);
        FluentActions.Invoking(() => line.Commit(r.ReservationId!.Value)).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Release_frees_held()
    {
        var line = ExerciseLine();
        var r = line.Reserve(InvoiceA, 1, Money.Of(100_000m));
        line.Release(r.ReservationId!.Value);
        line.Held.Should().Be(Money.Zero);
        line.Available.Should().Be(Money.Of(147_000m));
        line.Reservations.Single().Status.Should().Be(ReservationStatus.Released);
    }

    [Fact]
    public void ReleaseAllFor_releases_every_held_reservation_of_the_invoice()
    {
        var line = ExerciseLine(BudgetControlMode.Soft);
        line.Reserve(InvoiceA, 1, Money.Of(10_000m));
        line.Reserve(InvoiceB, 1, Money.Of(20_000m));
        line.ReleaseAllFor(InvoiceA).Should().Be(1);
        line.HeldFor(InvoiceA, 1).Should().Be(Money.Zero);
        line.Held.Should().Be(Money.Of(20_000m));
        line.ReleaseAllFor(InvoiceA).Should().Be(0);
    }

    [Fact]
    public void Unknown_reservation_is_rejected() =>
        FluentActions.Invoking(() => ExerciseLine().Commit(Guid.NewGuid())).Should().Throw<LedgerException>();

    [Fact]
    public void Liquidation_moves_encumbered_to_actuals_and_keeps_available()
    {
        var line = ExerciseLine();
        line.RecordLiquidation(Money.Of(36_000m));
        line.Encumbered.Should().Be(Money.Of(60_000m));
        line.Actuals.Should().Be(Money.Of(168_000m));
        line.Available.Should().Be(Money.Of(147_000m));
    }

    [Fact]
    public void Liquidation_over_encumbered_is_rejected() =>
        FluentActions.Invoking(() => ExerciseLine().RecordLiquidation(Money.Of(96_001m)))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Every_change_bumps_change_stamp()
    {
        var line = ExerciseLine();
        var r = line.Reserve(InvoiceA, 1, Money.Of(10_000m));
        line.ChangeStamp.Should().Be(1);
        line.Commit(r.ReservationId!.Value);
        line.ChangeStamp.Should().Be(2);
        line.Amend(Money.Of(1_000m), "BA-1", Jun15);
        line.RecordLiquidation(Money.Of(1_000m));
        line.ChangeStamp.Should().Be(4);
    }

    [Fact]
    public void Zero_or_negative_amounts_are_rejected()
    {
        var line = ExerciseLine();
        FluentActions.Invoking(() => line.Reserve(InvoiceA, 1, Money.Zero)).Should().Throw<LedgerException>();
        FluentActions.Invoking(() => line.Amend(Money.Zero, "X", Jun15)).Should().Throw<LedgerException>();
        FluentActions.Invoking(() => line.RecordLiquidation(Money.Of(-1m))).Should().Throw<LedgerException>();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`
Expected: ошибки компиляции — `BudgetLine`, `OpeningBalance` не определены.

- [ ] **Step 3: Реализация**

`Exceptions/LedgerException.cs`:
```csharp
namespace GovErp.Domain.Ledger.Exceptions;

public class LedgerException(string message) : Exception(message);
```

`Exceptions/BudgetConcurrencyException.cs`:
```csharp
namespace GovErp.Domain.Ledger.Exceptions;

/// <summary>Бросается реализацией репозитория / unit of work при конфликте rowversion. Слой сценариев ловит его без ссылки на EF.</summary>
public sealed class BudgetConcurrencyException(AccountCode account, FiscalYear fiscalYear)
    : LedgerException($"Budget line {account} {fiscalYear} was modified concurrently.")
{
    public AccountCode Account { get; } = account;
    public FiscalYear FiscalYear { get; } = fiscalYear;
}
```

Перечисления, `namespace GovErp.Domain.Ledger.Entities;`, по файлу на тип:
```csharp
public enum BudgetControlMode { Hard, Soft }
public enum ReservationStatus { Held, Committed, Released }
```

`Entities/OpeningBalance.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>Начальный снимок остатков на дату загрузки. Не проводка; неизменяем (spec §5, правило 7).</summary>
public sealed class OpeningBalance
{
    public Guid Id { get; private set; }
    public AccountCode Account { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public DateOnly AsOfDate { get; private set; }
    public Money InitialActuals { get; private set; }
    public Money InitialEncumbered { get; private set; }
    public string SourceReference { get; private set; }

    public OpeningBalance(AccountCode account, FiscalYear fiscalYear, DateOnly asOfDate, Money initialActuals,
        Money initialEncumbered, string sourceReference)
    {
        if (initialActuals.IsNegative || initialEncumbered.IsNegative)
        {
            throw new LedgerException($"Opening balance for {account} cannot be negative.");
        }

        if (!fiscalYear.Contains(asOfDate))
        {
            throw new LedgerException($"Opening date {asOfDate} is outside {fiscalYear}.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(sourceReference);
        Id = Guid.NewGuid();
        Account = account;
        FiscalYear = fiscalYear;
        AsOfDate = asOfDate;
        InitialActuals = initialActuals;
        InitialEncumbered = initialEncumbered;
        SourceReference = sourceReference;
    }

    private OpeningBalance() { Account = null!; SourceReference = null!; }
}
```

`Entities/BudgetAmendment.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public sealed record BudgetAmendment(Money Amount, string Reference, DateOnly EffectiveDate);
```

`Entities/BudgetReservation.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

/// <summary>Вложенная сущность BudgetLine: резерв новой части инвойса между Submit и Post. Владелец — InvoiceId + ContentVersion.</summary>
public sealed class BudgetReservation
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public Money Amount { get; private set; }
    public bool IsOverage { get; private set; }
    public ReservationStatus Status { get; private set; }

    internal BudgetReservation(Guid id, Guid invoiceId, int contentVersion, Money amount, bool isOverage)
    {
        Id = id;
        InvoiceId = invoiceId;
        ContentVersion = contentVersion;
        Amount = amount;
        IsOverage = isOverage;
        Status = ReservationStatus.Held;
    }

    private BudgetReservation() { }

    internal void Commit() => Status = ReservationStatus.Committed;
    internal void Release() => Status = ReservationStatus.Released;
}
```

`Entities/ReservationResult.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

public sealed record ReservationResult(bool IsReserved, Guid? ReservationId, Money AvailableBefore, Money Shortfall, bool IsOverage)
{
    public static ReservationResult Reserved(Guid id, Money availableBefore, Money shortfall, bool overage) =>
        new(true, id, availableBefore, shortfall, overage);

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
/// Available = Amended − Actuals − Encumbered − Held; AvailableFor = Available + собственный резерв инвойса.
/// Резервирование — единственная точка защиты от двойного списания (GE-14).
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
    /// <summary>Растёт при каждом изменении; гарантирует UPDATE строки владельца и срабатывание rowversion.</summary>
    public int ChangeStamp { get; private set; }

    public IReadOnlyList<BudgetAmendment> Amendments => _amendments;
    public IReadOnlyList<BudgetReservation> Reservations => _reservations;

    public Money Amended => _amendments.Aggregate(Adopted, (sum, a) => sum + a.Amount);
    public Money Held => SumHeld(_ => true);
    public Money Available => Amended - Actuals - Encumbered - Held;

    private BudgetLine(AccountCode account, FiscalYear fiscalYear, BudgetControlMode controlMode, Money adopted,
        Money actuals, Money encumbered)
    {
        if (adopted.IsNegative)
        {
            throw new LedgerException($"Adopted budget for {account} cannot be negative.");
        }

        Id = Guid.NewGuid();
        Account = account;
        FiscalYear = fiscalYear;
        ControlMode = controlMode;
        Adopted = adopted;
        Actuals = actuals;
        Encumbered = encumbered;
    }

    private BudgetLine() { Account = null!; }

    public static BudgetLine Open(OpeningBalance opening, BudgetControlMode controlMode, Money adopted) =>
        new(opening.Account, opening.FiscalYear, controlMode, adopted, opening.InitialActuals, opening.InitialEncumbered);

    public Money HeldFor(Guid invoiceId, int contentVersion) =>
        SumHeld(r => r.InvoiceId == invoiceId && r.ContentVersion == contentVersion);

    public Money AvailableFor(Guid invoiceId, int contentVersion) => Available + HeldFor(invoiceId, contentVersion);

    public void Amend(Money amount, string reference, DateOnly effectiveDate)
    {
        if (amount.IsZero)
        {
            throw new LedgerException("Amendment amount cannot be zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(reference);
        if (!FiscalYear.Contains(effectiveDate))
        {
            throw new LedgerException($"Amendment effective date {effectiveDate} is outside {FiscalYear}.");
        }

        _amendments.Add(new BudgetAmendment(amount, reference, effectiveDate));
        ChangeStamp++;
    }

    /// <summary>Один резерв на (инвойс, версия содержания) на строку: несколько distributions одного ключа суммируются до вызова.</summary>
    public ReservationResult Reserve(Guid invoiceId, int contentVersion, Money amount)
    {
        RequirePositive(amount, nameof(amount));
        if (!HeldFor(invoiceId, contentVersion).IsZero)
        {
            throw new LedgerException($"Invoice {invoiceId} v{contentVersion} already holds a reservation on {Account}.");
        }

        var availableBefore = AvailableFor(invoiceId, contentVersion);
        var shortfall = amount > availableBefore ? amount - availableBefore : Money.Zero;
        if (!shortfall.IsZero && ControlMode == BudgetControlMode.Hard)
        {
            return ReservationResult.Refused(availableBefore, shortfall);
        }

        var reservation = new BudgetReservation(Guid.NewGuid(), invoiceId, contentVersion, amount, !shortfall.IsZero);
        _reservations.Add(reservation);
        ChangeStamp++;
        return ReservationResult.Reserved(reservation.Id, availableBefore, shortfall, !shortfall.IsZero);
    }

    public void Commit(Guid reservationId)
    {
        var reservation = FindHeld(reservationId);
        reservation.Commit();
        Actuals += reservation.Amount;
        ChangeStamp++;
    }

    public void Release(Guid reservationId)
    {
        FindHeld(reservationId).Release();
        ChangeStamp++;
    }

    /// <summary>Reject / Withdraw: освобождает все удерживаемые резервы инвойса любой версии содержания.</summary>
    public int ReleaseAllFor(Guid invoiceId)
    {
        var held = _reservations.Where(r => r.InvoiceId == invoiceId && r.Status == ReservationStatus.Held).ToList();
        held.ForEach(r => r.Release());
        if (held.Count > 0)
        {
            ChangeStamp++;
        }

        return held.Count;
    }

    /// <summary>Ликвидация encumbrance: резерв под PO превращается в реальный расход. Available не меняется.</summary>
    public void RecordLiquidation(Money amount)
    {
        RequirePositive(amount, nameof(amount));
        if (amount > Encumbered)
        {
            throw new LedgerException($"Cannot liquidate {amount}: only {Encumbered} encumbered on {Account}.");
        }

        Encumbered -= amount;
        Actuals += amount;
        ChangeStamp++;
    }

    private Money SumHeld(Func<BudgetReservation, bool> filter) =>
        _reservations.Where(r => r.Status == ReservationStatus.Held && filter(r)).Aggregate(Money.Zero, (s, r) => s + r.Amount);

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
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`
Expected: 17 passed.

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Ledger tests/GovErp.Domain.Ledger.Tests
git commit -m "Ledger: opening balance and budget line with owned reservations

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 7: Ledger — Encumbrance с claims, JournalEntry, FiscalPeriod, репозитории

**Files:**
- Create: `src/GovErp.Domain.Ledger/Entities/Encumbrance.cs`, `EncumbranceClaim.cs`, `ClaimStatus.cs`, `EncumbranceStatus.cs`, `JournalEntry.cs`, `JournalLine.cs`, `LedgerFamily.cs`, `PostingKind.cs`, `FiscalPeriod.cs`, `PeriodStatus.cs`
- Create: `src/GovErp.Domain.Ledger/Repositories/IBudgetLineRepository.cs`, `IEncumbranceRepository.cs`, `IOpeningBalanceRepository.cs`, `IJournalRepository.cs`, `IFiscalPeriodRepository.cs`
- Test: `tests/GovErp.Domain.Ledger.Tests/EncumbranceTests.cs`, `JournalEntryTests.cs`

**Interfaces:**
- Produces: `Encumbrance(string poLineRef, AccountCode, FiscalYear, Money original)` с `Remaining`, `HeldClaims`, `ClaimableFor(Guid invoiceId)`, `ClaimOf(Guid invoiceId, int contentVersion)`, `Claim(Guid invoiceId, int contentVersion, Money) → EncumbranceClaim`, `ConsumeClaim(Guid claimId) → Money`, `ReleaseClaim(Guid claimId)`, `ReleaseAllFor(Guid invoiceId) → int`, `ReleaseRemainder(string reason)`, `ChangeStamp`; `ClaimStatus { Held, Consumed, Released }` (используется и в Payables — у каждого контекста свой enum с тем же именем); `JournalLine(AccountCode, LedgerFamily, Money Debit, Money Credit, string Description)`; `JournalEntry.Create(string sourceRef, PostingKind kind, IReadOnlyList<JournalLine>, FiscalPeriod, DateOnly postingDate, UserId, DateTimeOffset)`; `FiscalPeriod(int Year, int Month)` с `IsOpen`, `Contains(DateOnly)`, `Close()`, `FiscalPeriod.KeyFor(DateOnly)`; пять портов (сигнатуры ниже — план 3 их реализует).
- Правила из spec §3.3: `Σ Held claims ≤ Remaining`; claim меняет только его владелец; `ConsumeClaim` увеличивает `Liquidated` ровно на сумму claim'а — Post погашает закреплённый claim, а не пересчитывает ликвидацию заново.

- [ ] **Step 1: Тесты**

`EncumbranceTests.cs`:
```csharp
using GovErp.Domain.Ledger.Entities;
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Tests;

public class EncumbranceTests
{
    private static readonly AccountCode Police = AccountCode.Parse("701-3000-53100-G-COPS-26");
    private static readonly Guid InvoiceA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid InvoiceB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static Encumbrance Open(decimal amount = 160_000m) => new("PO-2026-0451/1", Police, new FiscalYear(2026), Money.Of(amount));

    [Fact]
    public void New_encumbrance_is_open_with_full_remaining()
    {
        var e = Open();
        e.Remaining.Should().Be(Money.Of(160_000m));
        e.ClaimableFor(InvoiceA).Should().Be(Money.Of(160_000m));
        e.Status.Should().Be(EncumbranceStatus.Open);
    }

    [Fact]
    public void Claim_reduces_claimable_for_other_invoices_only()
    {
        var e = Open(96_000m);
        e.Claim(InvoiceA, 1, Money.Of(96_000m));
        e.HeldClaims.Should().Be(Money.Of(96_000m));
        e.Remaining.Should().Be(Money.Of(96_000m));                // claim — не ликвидация
        e.ClaimableFor(InvoiceB).Should().Be(Money.Zero);
        e.ClaimableFor(InvoiceA).Should().Be(Money.Of(96_000m));   // свой claim не считается чужим
        e.ClaimOf(InvoiceA, 1)!.Amount.Should().Be(Money.Of(96_000m));
        e.ChangeStamp.Should().Be(1);
    }

    [Fact]
    public void Claim_over_claimable_is_rejected()
    {
        var e = Open(96_000m);
        e.Claim(InvoiceA, 1, Money.Of(96_000m));
        FluentActions.Invoking(() => e.Claim(InvoiceB, 1, Money.Of(1m))).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Second_claim_for_same_invoice_version_is_rejected()
    {
        var e = Open();
        e.Claim(InvoiceA, 1, Money.Of(10m));
        FluentActions.Invoking(() => e.Claim(InvoiceA, 1, Money.Of(10m))).Should().Throw<LedgerException>().WithMessage("*already*");
    }

    [Fact]
    public void Consume_claim_liquidates_exactly_the_claimed_amount()
    {
        var e = Open();
        var claim = e.Claim(InvoiceA, 1, Money.Of(32_000m));
        e.ConsumeClaim(claim.Id).Should().Be(Money.Of(32_000m));
        e.Liquidated.Should().Be(Money.Of(32_000m));
        e.Remaining.Should().Be(Money.Of(128_000m));
        e.HeldClaims.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Open);
        FluentActions.Invoking(() => e.ConsumeClaim(claim.Id)).Should().Throw<LedgerException>();
    }

    [Fact]
    public void Full_consumption_closes()
    {
        var e = Open();
        e.ConsumeClaim(e.Claim(InvoiceA, 1, Money.Of(160_000m)).Id);
        e.Remaining.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void ReleaseAllFor_frees_claims_of_the_invoice()
    {
        var e = Open();
        e.Claim(InvoiceA, 1, Money.Of(50_000m));
        e.Claim(InvoiceB, 1, Money.Of(10_000m));
        e.ReleaseAllFor(InvoiceA).Should().Be(1);
        e.HeldClaims.Should().Be(Money.Of(10_000m));
        e.Claims.Single(c => c.InvoiceId == InvoiceA).Status.Should().Be(ClaimStatus.Released);
    }

    [Fact]
    public void Release_remainder_requires_no_held_claims_and_closes()
    {
        var e = Open();
        e.ConsumeClaim(e.Claim(InvoiceA, 1, Money.Of(90_000m)).Id);
        e.Claim(InvoiceB, 1, Money.Of(1_000m));
        FluentActions.Invoking(() => e.ReleaseRemainder("final invoice")).Should().Throw<LedgerException>();
        e.ReleaseAllFor(InvoiceB);
        e.ReleaseRemainder("final invoice");
        e.Released.Should().Be(Money.Of(70_000m));
        e.Remaining.Should().Be(Money.Zero);
        e.Status.Should().Be(EncumbranceStatus.Closed);
    }

    [Fact]
    public void Closed_encumbrance_rejects_claims()
    {
        var e = Open();
        e.ReleaseRemainder("cancelled");
        FluentActions.Invoking(() => e.Claim(InvoiceA, 1, Money.Of(1m))).Should().Throw<LedgerException>();
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
    private static readonly DateTimeOffset At = new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Jun15 = new(2026, 6, 15);
    private static readonly FiscalPeriod OpenJune = new(2026, 6);

    private static JournalLine Dr(string account, decimal amount, LedgerFamily family = LedgerFamily.Financial) =>
        new(AccountCode.Parse(account), family, Money.Of(amount), Money.Zero, "test");

    private static JournalLine Cr(string account, decimal amount, LedgerFamily family = LedgerFamily.Financial) =>
        new(AccountCode.Parse(account), family, Money.Zero, Money.Of(amount), "test");

    private static JournalEntry Create(IReadOnlyList<JournalLine> lines, FiscalPeriod? period = null, DateOnly? date = null) =>
        JournalEntry.Create("INV-1", PostingKind.InvoicePosting, lines, period ?? OpenJune, date ?? Jun15, Poster, At);

    [Fact]
    public void Balanced_single_fund_entry_is_created()
    {
        var entry = Create([Dr("701-6000-53100-G-COPS-26", 160_000m), Cr("701-0000-2100", 160_000m)]);
        entry.Lines.Should().HaveCount(2);
        entry.SourceRef.Should().Be("INV-1");
        entry.Kind.Should().Be(PostingKind.InvoicePosting);
        entry.PostingDate.Should().Be(Jun15);
    }

    [Fact]
    public void Multi_fund_entry_must_balance_per_fund()
    {
        // Итог сходится (30k/30k), но фонд 101 — Дт 12k / Кт 8k, фонд 202 — Дт 8k / Кт 12k.
        FluentActions.Invoking(() => Create(
            [Dr("101-6000-53100", 12_000m), Cr("101-0000-2100", 8_000m),
             Dr("202-4000-53100", 8_000m),  Cr("202-0000-2100", 12_000m),
             Dr("501-5000-53100", 10_000m), Cr("501-0000-2100", 10_000m)]))
            .Should().Throw<LedgerException>().WithMessage("*101*");
    }

    [Fact]
    public void Multi_fund_balanced_entry_is_created() =>
        Create([Dr("101-6000-53100", 12_000m), Cr("101-0000-2100", 12_000m),
                Dr("202-4000-53100", 8_000m),  Cr("202-0000-2100", 8_000m),
                Dr("501-5000-53100", 10_000m), Cr("501-0000-2100", 10_000m)]).Lines.Should().HaveCount(6);

    [Fact]
    public void Budgetary_and_financial_families_balance_separately()
    {
        Create([Dr("701-0000-2900-G-COPS-26", 160_000m, LedgerFamily.Budgetary), Cr("701-3000-5900-G-COPS-26", 160_000m, LedgerFamily.Budgetary),
                Dr("701-3000-53100-G-COPS-26", 160_000m), Cr("701-0000-2100", 160_000m)]).Lines.Should().HaveCount(4);

        FluentActions.Invoking(() => Create(
            [Dr("701-0000-2900-G-COPS-26", 160_000m, LedgerFamily.Budgetary), Cr("701-0000-2100", 160_000m)]))
            .Should().Throw<LedgerException>();
    }

    [Fact]
    public void Closed_period_is_rejected()
    {
        var closedMay = new FiscalPeriod(2026, 5);
        closedMay.Close();
        FluentActions.Invoking(() => Create([Dr("701-6000-53100-G-COPS-26", 1m), Cr("701-0000-2100", 1m)], closedMay, new DateOnly(2026, 5, 20)))
            .Should().Throw<LedgerException>().WithMessage("*closed*");
    }

    [Fact]
    public void Posting_date_outside_period_is_rejected() =>
        FluentActions.Invoking(() => Create([Dr("701-6000-53100-G-COPS-26", 1m), Cr("701-0000-2100", 1m)], OpenJune, new DateOnly(2026, 7, 1)))
            .Should().Throw<LedgerException>();

    [Fact]
    public void Fewer_than_two_lines_is_rejected() =>
        FluentActions.Invoking(() => Create([Dr("701-6000-53100-G-COPS-26", 1m)])).Should().Throw<LedgerException>();

    [Fact]
    public void Line_with_both_debit_and_credit_is_rejected() =>
        FluentActions.Invoking(() => new JournalLine(AccountCode.Parse("101-6000-53100"), LedgerFamily.Financial, Money.Of(1), Money.Of(1), "bad"))
            .Should().Throw<LedgerException>();

    [Fact]
    public void FiscalPeriod_key_and_contains()
    {
        FiscalPeriod.KeyFor(Jun15).Should().Be((2026, 6));
        OpenJune.Contains(Jun15).Should().BeTrue();
        OpenJune.Contains(new DateOnly(2026, 5, 31)).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`

- [ ] **Step 3: Реализация**

Перечисления, `namespace GovErp.Domain.Ledger.Entities;`, по файлу на тип:
```csharp
public enum EncumbranceStatus { Open, Closed }
public enum ClaimStatus { Held, Consumed, Released }
public enum LedgerFamily { Financial, Budgetary }
public enum PostingKind { InvoicePosting }
public enum PeriodStatus { Open, Closed }
```

`Entities/EncumbranceClaim.cs`:
```csharp
namespace GovErp.Domain.Ledger.Entities;

/// <summary>Вложенная сущность Encumbrance: захват части остатка инвойсом (InvoiceId + ContentVersion) на Submit.</summary>
public sealed class EncumbranceClaim
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public Money Amount { get; private set; }
    public ClaimStatus Status { get; private set; }

    internal EncumbranceClaim(Guid id, Guid invoiceId, int contentVersion, Money amount)
    {
        Id = id;
        InvoiceId = invoiceId;
        ContentVersion = contentVersion;
        Amount = amount;
        Status = ClaimStatus.Held;
    }

    private EncumbranceClaim() { }

    internal void Consume() => Status = ClaimStatus.Consumed;
    internal void Release() => Status = ClaimStatus.Released;
}
```

`Entities/Encumbrance.cs`:
```csharp
using GovErp.Domain.Ledger.Exceptions;

namespace GovErp.Domain.Ledger.Entities;

/// <summary>Резерв бюджета под строку PO. Remaining = Original − Liquidated − Released; Σ Held claims ≤ Remaining.</summary>
public sealed class Encumbrance
{
    private readonly List<EncumbranceClaim> _claims = [];

    public Guid Id { get; private set; }
    public string PoLineRef { get; private set; }
    public AccountCode Account { get; private set; }
    public FiscalYear FiscalYear { get; private set; }
    public Money Original { get; private set; }
    public Money Liquidated { get; private set; }
    public Money Released { get; private set; }
    public EncumbranceStatus Status { get; private set; }
    public int ChangeStamp { get; private set; }
    public IReadOnlyList<EncumbranceClaim> Claims => _claims;

    public Money Remaining => Original - Liquidated - Released;
    public Money HeldClaims => _claims.Where(c => c.Status == ClaimStatus.Held).Aggregate(Money.Zero, (s, c) => s + c.Amount);

    public Encumbrance(string poLineRef, AccountCode account, FiscalYear fiscalYear, Money original)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(poLineRef);
        if (original <= Money.Zero)
        {
            throw new LedgerException($"Encumbrance amount must be positive, got {original}.");
        }

        Id = Guid.NewGuid();
        PoLineRef = poLineRef;
        Account = account;
        FiscalYear = fiscalYear;
        Original = original;
        Liquidated = Money.Zero;
        Released = Money.Zero;
        Status = EncumbranceStatus.Open;
    }

    private Encumbrance() { PoLineRef = null!; Account = null!; }

    /// <summary>Остаток, доступный этому инвойсу: Remaining минус чужие удерживаемые claims.</summary>
    public Money ClaimableFor(Guid invoiceId) =>
        Remaining - _claims.Where(c => c.Status == ClaimStatus.Held && c.InvoiceId != invoiceId).Aggregate(Money.Zero, (s, c) => s + c.Amount);

    public EncumbranceClaim? ClaimOf(Guid invoiceId, int contentVersion) =>
        _claims.SingleOrDefault(c => c.Status == ClaimStatus.Held && c.InvoiceId == invoiceId && c.ContentVersion == contentVersion);

    public EncumbranceClaim Claim(Guid invoiceId, int contentVersion, Money amount)
    {
        RequireOpen();
        if (amount <= Money.Zero)
        {
            throw new LedgerException($"Claim amount must be positive, got {amount}.");
        }

        if (ClaimOf(invoiceId, contentVersion) is not null)
        {
            throw new LedgerException($"Invoice {invoiceId} v{contentVersion} already holds a claim on {PoLineRef}.");
        }

        var claimable = ClaimableFor(invoiceId);
        if (amount > claimable)
        {
            throw new LedgerException($"Cannot claim {amount} on {PoLineRef}: only {claimable} is claimable.");
        }

        var claim = new EncumbranceClaim(Guid.NewGuid(), invoiceId, contentVersion, amount);
        _claims.Add(claim);
        ChangeStamp++;
        return claim;
    }

    /// <summary>Post: погашает закреплённый claim, увеличивая Liquidated ровно на его сумму. Возвращает погашенную сумму.</summary>
    public Money ConsumeClaim(Guid claimId)
    {
        var claim = FindHeld(claimId);
        claim.Consume();
        Liquidated += claim.Amount;
        if (Remaining.IsZero && HeldClaims.IsZero)
        {
            Status = EncumbranceStatus.Closed;
        }

        ChangeStamp++;
        return claim.Amount;
    }

    public void ReleaseClaim(Guid claimId)
    {
        FindHeld(claimId).Release();
        ChangeStamp++;
    }

    public int ReleaseAllFor(Guid invoiceId)
    {
        var held = _claims.Where(c => c.Status == ClaimStatus.Held && c.InvoiceId == invoiceId).ToList();
        held.ForEach(c => c.Release());
        if (held.Count > 0)
        {
            ChangeStamp++;
        }

        return held.Count;
    }

    public void ReleaseRemainder(string reason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        RequireOpen();
        if (!HeldClaims.IsZero)
        {
            throw new LedgerException($"Encumbrance {PoLineRef} has held claims {HeldClaims}; release them first.");
        }

        Released += Remaining;
        Status = EncumbranceStatus.Closed;
        ChangeStamp++;
    }

    private void RequireOpen()
    {
        if (Status == EncumbranceStatus.Closed)
        {
            throw new LedgerException($"Encumbrance {PoLineRef} is closed.");
        }
    }

    private EncumbranceClaim FindHeld(Guid claimId)
    {
        var claim = _claims.SingleOrDefault(c => c.Id == claimId)
            ?? throw new LedgerException($"Claim {claimId} not found on {PoLineRef}.");
        if (claim.Status != ClaimStatus.Held)
        {
            throw new LedgerException($"Claim {claimId} is {claim.Status}, expected Held.");
        }

        return claim;
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

    public bool Contains(DateOnly date) => date.Year == Year && date.Month == Month;

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

/// <summary>Проведённая запись журнала. Неизменяема. Баланс — по каждой паре (фонд, семейство счетов). (SourceRef, Kind) уникален (индекс в плане 3).</summary>
public sealed class JournalEntry
{
    private readonly List<JournalLine> _lines = [];

    public Guid Id { get; private set; }
    public string SourceRef { get; private set; }
    public PostingKind Kind { get; private set; }
    public DateOnly PostingDate { get; private set; }
    public int PeriodYear { get; private set; }
    public int PeriodMonth { get; private set; }
    public UserId PostedBy { get; private set; }
    public DateTimeOffset PostedAt { get; private set; }
    public IReadOnlyList<JournalLine> Lines => _lines;

    private JournalEntry(string sourceRef, PostingKind kind, IEnumerable<JournalLine> lines, FiscalPeriod period,
        DateOnly postingDate, UserId postedBy, DateTimeOffset postedAt)
    {
        Id = Guid.NewGuid();
        SourceRef = sourceRef;
        Kind = kind;
        PostingDate = postingDate;
        PeriodYear = period.Year;
        PeriodMonth = period.Month;
        PostedBy = postedBy;
        PostedAt = postedAt;
        _lines.AddRange(lines);
    }

    private JournalEntry() { SourceRef = null!; }

    public static JournalEntry Create(string sourceRef, PostingKind kind, IReadOnlyList<JournalLine> lines, FiscalPeriod period,
        DateOnly postingDate, UserId postedBy, DateTimeOffset postedAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceRef);
        if (!period.Contains(postingDate))
        {
            throw new LedgerException($"Posting date {postingDate} is outside period {period.Year}-{period.Month:00}.");
        }

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

        return new JournalEntry(sourceRef, kind, lines, period, postingDate, postedBy, postedAt);
    }
}
```

Репозитории, `namespace GovErp.Domain.Ledger.Repositories;`, по файлу на интерфейс:
```csharp
using GovErp.Domain.Ledger.Entities;

public interface IBudgetLineRepository
{
    Task<BudgetLine?> FindAsync(AccountCode account, FiscalYear fiscalYear, CancellationToken ct = default);
    Task<IReadOnlyList<BudgetLine>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default);
    /// <summary>Строки, на которых у инвойса есть удерживаемые резервы (Reject / Withdraw / Post).</summary>
    Task<IReadOnlyList<BudgetLine>> ListHeldForInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task AddAsync(BudgetLine line, CancellationToken ct = default);
}

public interface IEncumbranceRepository
{
    Task<Encumbrance?> FindByPoLineAsync(string poLineRef, CancellationToken ct = default);
    Task<IReadOnlyList<Encumbrance>> ListClaimedByInvoiceAsync(Guid invoiceId, CancellationToken ct = default);
    Task AddAsync(Encumbrance encumbrance, CancellationToken ct = default);
}

public interface IOpeningBalanceRepository
{
    Task<IReadOnlyList<OpeningBalance>> ListAsync(FiscalYear fiscalYear, CancellationToken ct = default);
    Task AddAsync(OpeningBalance balance, CancellationToken ct = default);
}

public interface IJournalRepository
{
    Task AddAsync(JournalEntry entry, CancellationToken ct = default);
    Task<bool> ExistsAsync(string sourceRef, PostingKind kind, CancellationToken ct = default);
    Task<IReadOnlyList<JournalEntry>> ListBySourceAsync(string sourceRef, CancellationToken ct = default);
}

public interface IFiscalPeriodRepository
{
    Task<FiscalPeriod?> FindAsync(int year, int month, CancellationToken ct = default);
}
```

- [ ] **Step 4: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Ledger.Tests`
Expected: 35 passed (17 из задачи 6 + 9 + 9).

- [ ] **Step 5: Commit**

```bash
git add src/GovErp.Domain.Ledger tests/GovErp.Domain.Ledger.Tests
git commit -m "Ledger: encumbrance claims, journal entry with posting kind, fiscal period, ports

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
```

---

### Task 8: Payables — Vendor, PurchaseOrder с billing claims, VendorInvoice

**Files:**
- Create: `src/GovErp.Domain.Payables/Entities/Vendor.cs`, `VendorStatus.cs`, `PurchaseOrder.cs`, `PurchaseOrderLine.cs`, `PoBillingClaim.cs`, `ClaimStatus.cs`, `PurchaseOrderStatus.cs`, `VendorInvoice.cs`, `InvoiceDates.cs`, `DistributionInput.cs`, `InvoiceDistribution.cs`, `InvoiceStatus.cs`, `InvoiceApproval.cs`, `ApprovalDecision.cs`, `InvoiceOverride.cs`, `ApproverRole.cs`
- Create: `src/GovErp.Domain.Payables/Repositories/IVendorRepository.cs`, `IPurchaseOrderRepository.cs`, `IVendorInvoiceRepository.cs`
- Create: `src/GovErp.Domain.Payables/Exceptions/PayablesException.cs`
- Test: `tests/GovErp.Domain.Payables.Tests/PurchaseOrderTests.cs`, `VendorInvoiceTests.cs`, `VendorInvoiceApprovalTests.cs`

**Interfaces:**
- Produces:
  - `Vendor` (`Id`, `Code`, `Name`, `Status`, `SamRegistered`, `IsActive`).
  - `PurchaseOrder(string number, Guid vendorId, IReadOnlyList<(int LineNo, AccountCode Account, Money AuthorizedAmount)>)` с `Lines`, `Line(int)`, `LineRef(int) → "<Number>/<LineNo>"`, `OtherActiveBillingClaims(int lineNo, Guid invoiceId) → Money`, `BillingClaimsOf(Guid invoiceId, int contentVersion)`, `ClaimBilling(int lineNo, Guid invoiceId, int contentVersion, Money) → PoBillingClaim`, `ConsumeBillingClaim(Guid claimId) → Money`, `ReleaseBillingClaimsFor(Guid invoiceId) → int`, `ChangeStamp`; `PurchaseOrderLine` (`LineNo`, `Account`, `AuthorizedAmount`, `PostedAmount`, `BillingClaims`).
  - `InvoiceDates(DateOnly Invoice, DateOnly Service, DateOnly Posting, DateOnly Due)`; `DistributionInput(AccountCode Account, Money Amount, int? PoLineNo)`.
  - `VendorInvoice(string number, Guid vendorId, InvoiceDates, Money total, string? poNumber, IReadOnlyList<DistributionInput>, UserId createdBy, DateTimeOffset createdAt)` со свойствами `Number`, `NormalizedNumber`, `Reference` (`INV-<Number>`), `VendorId`, `Dates`, `Total`, `PoNumber`, `IsPoBacked`, `Status`, `ContentVersion`, `ApprovalCycleId`, `PaymentHold`, `LastEvaluationId`, `CreatedBy`, `PostedAt`, `Distributions`, `Approvals`, `Overrides`, `ActiveApprovals`, `ActiveOverrides`, `DistributedTotal`; методами `ReplaceContent(Guid vendorId, InvoiceDates, Money total, string? poNumber, IReadOnlyList<DistributionInput>) → bool`, `Submit(Guid evaluationId)`, `RecordApproval(ApproverRole, string? department, UserId, Guid evaluationId, DateTimeOffset)`, `MarkApproved()`, `Override(Guid evaluationId, string ruleId, int ruleVersion, int? distributionLine, UserId, string reason, DateTimeOffset)`, `OpenNewApprovalCycle()`, `Reject(ApproverRole, UserId, string reason, DateTimeOffset)`, `Withdraw(UserId, string reason, DateTimeOffset)`, `Post(Guid evaluationId, DateTimeOffset)`, `SetPaymentHold(bool)`, `IsReadyForPaymentHandoff(bool vendorActive, DateOnly businessDate)`, `RecordEvaluation(Guid evaluationId)`.
  - `ApproverRole { DepartmentHead, GrantsManager, BudgetOfficer, FinanceDirector }`; `InvoiceStatus { Draft, Submitted, Approved, Posted }`.
- Правила spec §3.4 и §5 (правила 1, 2, 5, 8, 9): `ContentVersion` растёт только при реальном изменении содержания; Reject/Withdraw возвращают в Draft, открывают новый цикл и **не удаляют** историю согласований и overrides; `ActiveApprovals`/`ActiveOverrides` — только текущего цикла (и для overrides — текущей версии содержания); статусов `Rejected` и `Payable` нет; готовность к оплате вычисляется.

- [ ] **Step 1: Тесты PurchaseOrder**

`PurchaseOrderTests.cs`:
```csharp
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Tests;

public class PurchaseOrderTests
{
    private static readonly Guid InvoiceA = Guid.Parse("aaaaaaaa-0000-0000-0000-000000000001");
    private static readonly Guid InvoiceB = Guid.Parse("bbbbbbbb-0000-0000-0000-000000000002");

    private static PurchaseOrder Po() => new("PO-2026-0451", Guid.NewGuid(),
        [(1, AccountCode.Parse("701-3000-53100-G-COPS-26"), Money.Of(160_000m))]);

    [Fact]
    public void Line_ref_and_authorized_amount()
    {
        var po = Po();
        po.LineRef(1).Should().Be("PO-2026-0451/1");
        po.Line(1).AuthorizedAmount.Should().Be(Money.Of(160_000m));
        po.Line(1).PostedAmount.Should().Be(Money.Zero);
        po.Status.Should().Be(PurchaseOrderStatus.Open);
    }

    [Fact]
    public void Unknown_line_is_rejected() =>
        FluentActions.Invoking(() => Po().LineRef(2)).Should().Throw<PayablesException>();

    [Fact]
    public void Zero_authorized_amount_is_rejected() =>
        FluentActions.Invoking(() => new PurchaseOrder("PO-1", Guid.NewGuid(), [(1, AccountCode.Parse("101-6000-53100"), Money.Zero)]))
            .Should().Throw<PayablesException>();

    [Fact]
    public void Billing_claims_of_other_invoices_are_visible_own_are_not()
    {
        var po = Po();
        po.ClaimBilling(1, InvoiceA, 1, Money.Of(100_000m));
        po.OtherActiveBillingClaims(1, InvoiceB).Should().Be(Money.Of(100_000m));
        po.OtherActiveBillingClaims(1, InvoiceA).Should().Be(Money.Zero);
        po.BillingClaimsOf(InvoiceA, 1).Should().ContainSingle();
        po.ChangeStamp.Should().Be(1);
    }

    [Fact]
    public void Claim_may_exceed_authorized_amount_limit_is_the_rules_job()
    {
        // Допуск (5%) проверяет правило PO_LIQUIDATION в той же транзакции; агрегат хранит факт.
        var po = Po();
        po.ClaimBilling(1, InvoiceA, 1, Money.Of(164_000m)).Amount.Should().Be(Money.Of(164_000m));
    }

    [Fact]
    public void Second_claim_for_same_invoice_version_and_line_is_rejected()
    {
        var po = Po();
        po.ClaimBilling(1, InvoiceA, 1, Money.Of(1m));
        FluentActions.Invoking(() => po.ClaimBilling(1, InvoiceA, 1, Money.Of(1m))).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Consume_moves_claim_to_posted_amount()
    {
        var po = Po();
        var claim = po.ClaimBilling(1, InvoiceA, 1, Money.Of(160_000m));
        po.ConsumeBillingClaim(claim.Id).Should().Be(Money.Of(160_000m));
        po.Line(1).PostedAmount.Should().Be(Money.Of(160_000m));
        po.OtherActiveBillingClaims(1, InvoiceB).Should().Be(Money.Zero);
        FluentActions.Invoking(() => po.ConsumeBillingClaim(claim.Id)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Release_all_for_invoice()
    {
        var po = Po();
        po.ClaimBilling(1, InvoiceA, 1, Money.Of(1m));
        po.ClaimBilling(1, InvoiceB, 1, Money.Of(2m));
        po.ReleaseBillingClaimsFor(InvoiceA).Should().Be(1);
        po.OtherActiveBillingClaims(1, InvoiceA).Should().Be(Money.Of(2m));
    }
}
```

- [ ] **Step 2: Тесты VendorInvoice — содержание и жизненный цикл**

`VendorInvoiceTests.cs`:
```csharp
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Tests;

public class VendorInvoiceTests
{
    internal static readonly UserId Clerk = UserId.New();
    internal static readonly UserId Chief = UserId.New();
    internal static readonly Guid VendorId = Guid.NewGuid();
    internal static readonly DateTimeOffset At = new(2026, 6, 15, 9, 0, 0, TimeSpan.Zero);
    internal static readonly DateOnly Jun15 = new(2026, 6, 15);
    internal static readonly InvoiceDates Dates = new(Jun15, Jun15, Jun15, new DateOnly(2026, 7, 15));
    internal static readonly AccountCode Cops = AccountCode.Parse("701-6000-53100-G-COPS-26");

    internal static VendorInvoice Draft(decimal total = 160_000m, params DistributionInput[] lines) =>
        new(" v-7781 ", VendorId, Dates, Money.Of(total), poNumber: null,
            lines.Length == 0 ? [new DistributionInput(Cops, Money.Of(total), null)] : lines, Clerk, At);

    internal static VendorInvoice Submitted()
    {
        var inv = Draft();
        inv.Submit(Guid.NewGuid());
        return inv;
    }

    [Fact]
    public void New_invoice_is_draft_with_content_version_one_and_normalized_number()
    {
        var inv = Draft();
        inv.Status.Should().Be(InvoiceStatus.Draft);
        inv.ContentVersion.Should().Be(1);
        inv.Number.Should().Be("v-7781");
        inv.NormalizedNumber.Should().Be("V-7781");
        inv.Reference.Should().Be("INV-v-7781");
        inv.ApprovalCycleId.Should().NotBe(Guid.Empty);
        inv.IsPoBacked.Should().BeFalse();
        inv.Distributions.Single().LineNo.Should().Be(1);
    }

    [Fact]
    public void Due_date_before_invoice_date_is_rejected() =>
        FluentActions.Invoking(() => new InvoiceDates(Jun15, Jun15, Jun15, new DateOnly(2026, 6, 14)))
            .Should().Throw<PayablesException>();

    [Fact]
    public void Po_line_without_po_number_is_rejected() =>
        FluentActions.Invoking(() => Draft(10m, new DistributionInput(Cops, Money.Of(10m), PoLineNo: 1)))
            .Should().Throw<PayablesException>();

    [Fact]
    public void Replace_content_bumps_version_only_when_something_changes()
    {
        var inv = Draft();
        inv.ReplaceContent(VendorId, Dates, Money.Of(160_000m), null, [new DistributionInput(Cops, Money.Of(160_000m), null)])
            .Should().BeFalse();
        inv.ContentVersion.Should().Be(1);

        inv.ReplaceContent(VendorId, Dates, Money.Of(30_000m), null,
            [new DistributionInput(AccountCode.Parse("101-6000-53100"), Money.Of(12_000m), null),
             new DistributionInput(AccountCode.Parse("202-4000-53100"), Money.Of(18_000m), null)]).Should().BeTrue();
        inv.ContentVersion.Should().Be(2);
        inv.Distributions.Select(d => d.LineNo).Should().Equal(1, 2);
    }

    [Fact]
    public void Submit_requires_distributions_to_equal_total()
    {
        var inv = Draft(160_000m, new DistributionInput(Cops, Money.Of(150_000m), null));
        FluentActions.Invoking(() => inv.Submit(Guid.NewGuid())).Should().Throw<PayablesException>().WithMessage("*160,000*");
    }

    [Fact]
    public void Submit_moves_to_submitted_and_records_evaluation()
    {
        var eval = Guid.NewGuid();
        var inv = Draft();
        inv.Submit(eval);
        inv.Status.Should().Be(InvoiceStatus.Submitted);
        inv.LastEvaluationId.Should().Be(eval);
    }

    [Fact]
    public void Submitted_invoice_content_cannot_change() =>
        FluentActions.Invoking(() => Submitted().ReplaceContent(VendorId, Dates, Money.Of(1m), null, [new DistributionInput(Cops, Money.Of(1m), null)]))
            .Should().Throw<PayablesException>();

    [Fact]
    public void Post_requires_approved()
    {
        var inv = Submitted();
        FluentActions.Invoking(() => inv.Post(Guid.NewGuid(), At)).Should().Throw<PayablesException>();
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        inv.MarkApproved();
        var eval = Guid.NewGuid();
        inv.Post(eval, At);
        inv.Status.Should().Be(InvoiceStatus.Posted);
        inv.LastEvaluationId.Should().Be(eval);
        inv.PostedAt.Should().Be(At);
    }

    [Fact]
    public void Withdraw_by_author_returns_to_draft_and_opens_new_cycle()
    {
        var inv = Submitted();
        var cycle = inv.ApprovalCycleId;
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        inv.Withdraw(Clerk, "wrong vendor", At);
        inv.Status.Should().Be(InvoiceStatus.Draft);
        inv.ApprovalCycleId.Should().NotBe(cycle);
        inv.ContentVersion.Should().Be(1);                 // отзыв сам по себе содержание не меняет
        inv.Approvals.Should().ContainSingle();            // история сохранена
        inv.ActiveApprovals.Should().BeEmpty();            // но в новом цикле не действует
    }

    [Fact]
    public void Withdraw_by_non_author_or_after_post_is_rejected()
    {
        FluentActions.Invoking(() => Submitted().Withdraw(Chief, "x", At)).Should().Throw<PayablesException>();
        var posted = Submitted();
        posted.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        posted.MarkApproved();
        posted.Post(Guid.NewGuid(), At);
        FluentActions.Invoking(() => posted.Withdraw(Clerk, "late", At)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Reject_records_decision_returns_to_draft_and_keeps_history()
    {
        var inv = Submitted();
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        inv.Reject(ApproverRole.GrantsManager, UserId.New(), "not an allowable cost", At);
        inv.Status.Should().Be(InvoiceStatus.Draft);
        inv.Approvals.Should().HaveCount(2);
        inv.Approvals.Last().Decision.Should().Be(ApprovalDecision.Rejected);
        inv.Approvals.Last().Reason.Should().Be("not an allowable cost");
        inv.ActiveApprovals.Should().BeEmpty();
    }

    [Fact]
    public void Posted_invoice_cannot_be_rejected()
    {
        var inv = Submitted();
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        inv.MarkApproved();
        inv.Post(Guid.NewGuid(), At);
        FluentActions.Invoking(() => inv.Reject(ApproverRole.FinanceDirector, Chief, "late", At)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Payment_handoff_readiness_is_computed()
    {
        var inv = Submitted();
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        inv.MarkApproved();
        inv.IsReadyForPaymentHandoff(vendorActive: true, businessDate: new DateOnly(2026, 7, 15)).Should().BeFalse(); // ещё не Posted
        inv.Post(Guid.NewGuid(), At);
        inv.IsReadyForPaymentHandoff(true, Jun15).Should().BeFalse();                        // DueDate 2026-07-15 ещё не наступил
        inv.IsReadyForPaymentHandoff(true, new DateOnly(2026, 7, 15)).Should().BeTrue();
        inv.IsReadyForPaymentHandoff(false, new DateOnly(2026, 7, 15)).Should().BeFalse();
        inv.SetPaymentHold(true);
        inv.IsReadyForPaymentHandoff(true, new DateOnly(2026, 7, 15)).Should().BeFalse();
    }

    [Fact]
    public void More_than_two_decimals_never_reaches_the_invoice() =>
        FluentActions.Invoking(() => Draft(1.005m)).Should().Throw<ArgumentException>();
}
```

- [ ] **Step 3: Тесты VendorInvoice — согласования и overrides**

`VendorInvoiceApprovalTests.cs`:
```csharp
using GovErp.Domain.Payables.Entities;
using GovErp.Domain.Payables.Exceptions;
using static GovErp.Domain.Payables.Tests.VendorInvoiceTests;

namespace GovErp.Domain.Payables.Tests;

public class VendorInvoiceApprovalTests
{
    [Fact]
    public void Author_cannot_approve() =>
        FluentActions.Invoking(() => Submitted().RecordApproval(ApproverRole.DepartmentHead, "6000", Clerk, Guid.NewGuid(), At))
            .Should().Throw<PayablesException>().WithMessage("*separation of duties*");

    [Fact]
    public void Same_role_and_department_cannot_approve_twice_in_a_cycle()
    {
        var inv = Submitted();
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        FluentActions.Invoking(() => inv.RecordApproval(ApproverRole.DepartmentHead, "6000", UserId.New(), Guid.NewGuid(), At))
            .Should().Throw<PayablesException>();
        inv.RecordApproval(ApproverRole.DepartmentHead, "4000", UserId.New(), Guid.NewGuid(), At);   // другой департамент — отдельный шаг
        inv.ActiveApprovals.Should().HaveCount(2);
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
    public void Override_is_bound_to_evaluation_rule_version_line_and_content_version()
    {
        var inv = Submitted();
        var eval = Guid.NewGuid();
        inv.Override(eval, "PROCUREMENT_THRESHOLD", 1, null, Chief, "Sole-source justification on file", At);
        inv.ActiveOverrides.Should().ContainSingle(o => o.EvaluationId == eval && o.RuleId == "PROCUREMENT_THRESHOLD"
            && o.RuleVersion == 1 && o.DistributionLine == null && o.ContentVersion == 1 && o.CycleId == inv.ApprovalCycleId);
    }

    [Fact]
    public void Override_requires_reason_and_submitted_status()
    {
        FluentActions.Invoking(() => Draft().Override(Guid.NewGuid(), "X", 1, null, Chief, "r", At)).Should().Throw<PayablesException>();
        FluentActions.Invoking(() => Submitted().Override(Guid.NewGuid(), "X", 1, null, Chief, " ", At)).Should().Throw<PayablesException>();
    }

    [Fact]
    public void Duplicate_override_for_same_rule_and_line_in_cycle_is_rejected()
    {
        var inv = Submitted();
        inv.Override(Guid.NewGuid(), "BUDGET_AVAILABILITY", 1, 1, Chief, "ok", At);
        FluentActions.Invoking(() => inv.Override(Guid.NewGuid(), "BUDGET_AVAILABILITY", 1, 1, Chief, "again", At))
            .Should().Throw<PayablesException>();
        inv.Override(Guid.NewGuid(), "BUDGET_AVAILABILITY", 1, 2, Chief, "other line", At);
        inv.ActiveOverrides.Should().HaveCount(2);
    }

    [Fact]
    public void New_cycle_deactivates_old_approvals_and_overrides_and_reopens_approved()
    {
        var inv = Submitted();
        inv.RecordApproval(ApproverRole.DepartmentHead, "6000", Chief, Guid.NewGuid(), At);
        inv.Override(Guid.NewGuid(), "PROCUREMENT_THRESHOLD", 1, null, Chief, "ok", At);
        inv.MarkApproved();
        inv.OpenNewApprovalCycle();                      // fingerprint изменился
        inv.Status.Should().Be(InvoiceStatus.Submitted);
        inv.ActiveApprovals.Should().BeEmpty();
        inv.ActiveOverrides.Should().BeEmpty();
        inv.Approvals.Should().HaveCount(1);
        inv.Overrides.Should().HaveCount(1);
    }

    [Fact]
    public void Content_change_after_withdraw_invalidates_old_overrides_even_in_same_cycle_id_scope()
    {
        var inv = Submitted();
        inv.Override(Guid.NewGuid(), "PROCUREMENT_THRESHOLD", 1, null, Chief, "ok", At);
        inv.Withdraw(Clerk, "fix amount", At);
        inv.ReplaceContent(VendorId, Dates, Money.Of(150_000m), null, [new DistributionInput(Cops, Money.Of(150_000m), null)]);
        inv.ContentVersion.Should().Be(2);
        inv.ActiveOverrides.Should().BeEmpty();
    }
}
```

- [ ] **Step 4: Убедиться, что не компилируется**

Run: `dotnet test tests/GovErp.Domain.Payables.Tests`

- [ ] **Step 5: Реализация — общие типы**

`Exceptions/PayablesException.cs`:
```csharp
namespace GovErp.Domain.Payables.Exceptions;

public sealed class PayablesException(string message) : Exception(message);
```

Перечисления, `namespace GovErp.Domain.Payables.Entities;`, по файлу на тип:
```csharp
public enum VendorStatus { Active, Inactive, Debarred }
public enum PurchaseOrderStatus { Open, Closed, Cancelled }
public enum ClaimStatus { Held, Consumed, Released }
public enum InvoiceStatus { Draft, Submitted, Approved, Posted }
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

    public bool IsActive => Status == VendorStatus.Active;

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

`Entities/InvoiceDates.cs`:
```csharp
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

/// <summary>Invoice — effective-правила; Service — допустимость гранта; Posting — бюджетный год и период; Due — срок оплаты (spec §5, правило 6).</summary>
public sealed record InvoiceDates
{
    public DateOnly Invoice { get; }
    public DateOnly Service { get; }
    public DateOnly Posting { get; }
    public DateOnly Due { get; }

    public InvoiceDates(DateOnly invoice, DateOnly service, DateOnly posting, DateOnly due)
    {
        if (due < invoice)
        {
            throw new PayablesException($"Due date {due} is before invoice date {invoice}.");
        }

        Invoice = invoice;
        Service = service;
        Posting = posting;
        Due = due;
    }
}
```

`Entities/DistributionInput.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record DistributionInput(AccountCode Account, Money Amount, int? PoLineNo);
```

`Entities/InvoiceDistribution.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceDistribution(int LineNo, AccountCode Account, Money Amount, int? PoLineNo);
```

`Entities/InvoiceApproval.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceApproval(Guid CycleId, ApproverRole Role, string? Department, UserId UserId,
    ApprovalDecision Decision, Guid? EvaluationId, string? Reason, DateTimeOffset At);
```

`Entities/InvoiceOverride.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

public sealed record InvoiceOverride(Guid CycleId, Guid EvaluationId, string RuleId, int RuleVersion, int? DistributionLine,
    int ContentVersion, UserId UserId, string Reason, DateTimeOffset At);
```

- [ ] **Step 6: Реализация — PurchaseOrder**

`Entities/PoBillingClaim.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

/// <summary>Захват полной суммы PO-backed инвойса по PO-строке; защищает накопительный допуск (spec §5, правило 4).</summary>
public sealed class PoBillingClaim
{
    public Guid Id { get; private set; }
    public Guid InvoiceId { get; private set; }
    public int ContentVersion { get; private set; }
    public Money Amount { get; private set; }
    public ClaimStatus Status { get; private set; }

    internal PoBillingClaim(Guid id, Guid invoiceId, int contentVersion, Money amount)
    {
        Id = id;
        InvoiceId = invoiceId;
        ContentVersion = contentVersion;
        Amount = amount;
        Status = ClaimStatus.Held;
    }

    private PoBillingClaim() { }

    internal void Consume() => Status = ClaimStatus.Consumed;
    internal void Release() => Status = ClaimStatus.Released;
}
```

`Entities/PurchaseOrderLine.cs`:
```csharp
namespace GovErp.Domain.Payables.Entities;

/// <summary>Вложенная сущность PurchaseOrder. Меняется только через корень.</summary>
public sealed class PurchaseOrderLine
{
    private readonly List<PoBillingClaim> _billingClaims = [];

    public int LineNo { get; private set; }
    public AccountCode Account { get; private set; }
    public Money AuthorizedAmount { get; private set; }
    public Money PostedAmount { get; private set; }
    public IReadOnlyList<PoBillingClaim> BillingClaims => _billingClaims;

    internal PurchaseOrderLine(int lineNo, AccountCode account, Money authorizedAmount)
    {
        LineNo = lineNo;
        Account = account;
        AuthorizedAmount = authorizedAmount;
        PostedAmount = Money.Zero;
    }

    private PurchaseOrderLine() { Account = null!; }

    internal void AddClaim(PoBillingClaim claim) => _billingClaims.Add(claim);
    internal void AddPosted(Money amount) => PostedAmount += amount;
}
```

`Entities/PurchaseOrder.cs`:
```csharp
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

/// <summary>Заказ с утверждёнными суммами строк и billing claims. Допуск проверяет конвейер под RowVersion этого агрегата.</summary>
public sealed class PurchaseOrder
{
    private readonly List<PurchaseOrderLine> _lines = [];

    public Guid Id { get; private set; }
    public string Number { get; private set; }
    public Guid VendorId { get; private set; }
    public PurchaseOrderStatus Status { get; private set; }
    public int ChangeStamp { get; private set; }
    public IReadOnlyList<PurchaseOrderLine> Lines => _lines;

    public PurchaseOrder(string number, Guid vendorId, IReadOnlyList<(int LineNo, AccountCode Account, Money AuthorizedAmount)> lines)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        if (lines.Count == 0)
        {
            throw new PayablesException("Purchase order needs at least one line.");
        }

        if (lines.Any(l => l.AuthorizedAmount <= Money.Zero))
        {
            throw new PayablesException($"PO {number}: authorized amounts must be positive.");
        }

        if (lines.Select(l => l.LineNo).Distinct().Count() != lines.Count)
        {
            throw new PayablesException($"PO {number}: duplicate line numbers.");
        }

        Id = Guid.NewGuid();
        Number = number;
        VendorId = vendorId;
        Status = PurchaseOrderStatus.Open;
        _lines.AddRange(lines.Select(l => new PurchaseOrderLine(l.LineNo, l.Account, l.AuthorizedAmount)));
    }

    private PurchaseOrder() { Number = null!; }

    public PurchaseOrderLine Line(int lineNo) =>
        _lines.SingleOrDefault(l => l.LineNo == lineNo) ?? throw new PayablesException($"PO {Number} has no line {lineNo}.");

    public string LineRef(int lineNo) => $"{Number}/{Line(lineNo).LineNo}";

    public Money OtherActiveBillingClaims(int lineNo, Guid invoiceId) =>
        Line(lineNo).BillingClaims.Where(c => c.Status == ClaimStatus.Held && c.InvoiceId != invoiceId)
            .Aggregate(Money.Zero, (s, c) => s + c.Amount);

    public IReadOnlyList<PoBillingClaim> BillingClaimsOf(Guid invoiceId, int contentVersion) =>
        _lines.SelectMany(l => l.BillingClaims)
            .Where(c => c.Status == ClaimStatus.Held && c.InvoiceId == invoiceId && c.ContentVersion == contentVersion).ToList();

    public PoBillingClaim ClaimBilling(int lineNo, Guid invoiceId, int contentVersion, Money amount)
    {
        if (Status != PurchaseOrderStatus.Open)
        {
            throw new PayablesException($"PO {Number} is {Status}.");
        }

        if (amount <= Money.Zero)
        {
            throw new PayablesException($"Billing claim must be positive, got {amount}.");
        }

        var line = Line(lineNo);
        if (line.BillingClaims.Any(c => c.Status == ClaimStatus.Held && c.InvoiceId == invoiceId && c.ContentVersion == contentVersion))
        {
            throw new PayablesException($"Invoice {invoiceId} v{contentVersion} already holds a billing claim on {LineRef(lineNo)}.");
        }

        var claim = new PoBillingClaim(Guid.NewGuid(), invoiceId, contentVersion, amount);
        line.AddClaim(claim);
        ChangeStamp++;
        return claim;
    }

    public Money ConsumeBillingClaim(Guid claimId)
    {
        var (line, claim) = FindHeld(claimId);
        claim.Consume();
        line.AddPosted(claim.Amount);
        ChangeStamp++;
        return claim.Amount;
    }

    public int ReleaseBillingClaimsFor(Guid invoiceId)
    {
        var held = _lines.SelectMany(l => l.BillingClaims).Where(c => c.Status == ClaimStatus.Held && c.InvoiceId == invoiceId).ToList();
        held.ForEach(c => c.Release());
        if (held.Count > 0)
        {
            ChangeStamp++;
        }

        return held.Count;
    }

    private (PurchaseOrderLine Line, PoBillingClaim Claim) FindHeld(Guid claimId)
    {
        foreach (var line in _lines)
        {
            var claim = line.BillingClaims.SingleOrDefault(c => c.Id == claimId);
            if (claim is null)
            {
                continue;
            }

            if (claim.Status != ClaimStatus.Held)
            {
                throw new PayablesException($"Billing claim {claimId} is {claim.Status}, expected Held.");
            }

            return (line, claim);
        }

        throw new PayablesException($"Billing claim {claimId} not found on PO {Number}.");
    }
}
```

- [ ] **Step 7: Реализация — VendorInvoice**

`Entities/VendorInvoice.cs`:
```csharp
using GovErp.Domain.Payables.Exceptions;

namespace GovErp.Domain.Payables.Entities;

/// <summary>
/// AP-инвойс. Draft → Submitted → Approved → Posted; Reject/Withdraw из Submitted/Approved → Draft с новым циклом.
/// ContentVersion растёт только при реальном изменении содержания; история согласований не удаляется.
/// </summary>
public sealed class VendorInvoice
{
    private readonly List<InvoiceDistribution> _distributions = [];
    private readonly List<InvoiceApproval> _approvals = [];
    private readonly List<InvoiceOverride> _overrides = [];

    public Guid Id { get; private set; }
    public string Number { get; private set; }
    public string NormalizedNumber { get; private set; }
    public Guid VendorId { get; private set; }
    public InvoiceDates Dates { get; private set; }
    public Money Total { get; private set; }
    public string? PoNumber { get; private set; }
    public InvoiceStatus Status { get; private set; }
    public int ContentVersion { get; private set; }
    public Guid ApprovalCycleId { get; private set; }
    public bool PaymentHold { get; private set; }
    public Guid? LastEvaluationId { get; private set; }
    public UserId CreatedBy { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? PostedAt { get; private set; }

    public IReadOnlyList<InvoiceDistribution> Distributions => _distributions;
    public IReadOnlyList<InvoiceApproval> Approvals => _approvals;
    public IReadOnlyList<InvoiceOverride> Overrides => _overrides;

    public IReadOnlyList<InvoiceApproval> ActiveApprovals =>
        _approvals.Where(a => a.CycleId == ApprovalCycleId && a.Decision == ApprovalDecision.Approved).ToList();

    public IReadOnlyList<InvoiceOverride> ActiveOverrides =>
        _overrides.Where(o => o.CycleId == ApprovalCycleId && o.ContentVersion == ContentVersion).ToList();

    public string Reference => $"INV-{Number}";
    public bool IsPoBacked => PoNumber is not null;
    public Money DistributedTotal => _distributions.Aggregate(Money.Zero, (s, d) => s + d.Amount);

    public VendorInvoice(string number, Guid vendorId, InvoiceDates dates, Money total, string? poNumber,
        IReadOnlyList<DistributionInput> distributions, UserId createdBy, DateTimeOffset createdAt)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(number);
        Id = Guid.NewGuid();
        Number = number.Trim();
        NormalizedNumber = Normalize(number);
        CreatedBy = createdBy;
        CreatedAt = createdAt;
        Status = InvoiceStatus.Draft;
        ContentVersion = 1;
        ApprovalCycleId = Guid.NewGuid();
        Dates = null!;
        ApplyContent(vendorId, dates, total, poNumber, distributions);
    }

    private VendorInvoice() { Number = null!; NormalizedNumber = null!; Dates = null!; }

    public static string Normalize(string number) => number.Trim().ToUpperInvariant();

    /// <summary>Возвращает true, если содержание изменилось (и ContentVersion увеличен).</summary>
    public bool ReplaceContent(Guid vendorId, InvoiceDates dates, Money total, string? poNumber, IReadOnlyList<DistributionInput> distributions)
    {
        RequireStatus(InvoiceStatus.Draft, "change content");
        var before = (VendorId, Dates, Total, PoNumber, Lines: _distributions.ToList());
        ApplyContent(vendorId, dates, total, poNumber, distributions);
        var changed = before.VendorId != VendorId || before.Dates != Dates || before.Total != Total
            || before.PoNumber != PoNumber || !before.Lines.SequenceEqual(_distributions);
        if (changed)
        {
            ContentVersion++;
        }

        return changed;
    }

    public void Submit(Guid evaluationId)
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
        LastEvaluationId = evaluationId;
    }

    public void RecordEvaluation(Guid evaluationId) => LastEvaluationId = evaluationId;

    public void RecordApproval(ApproverRole role, string? department, UserId userId, Guid evaluationId, DateTimeOffset at)
    {
        RequireStatus(InvoiceStatus.Submitted, "approve");
        if (userId == CreatedBy)
        {
            throw new PayablesException($"User {userId} created {Reference} and cannot approve it (separation of duties).");
        }

        if (ActiveApprovals.Any(a => a.Role == role && a.Department == department))
        {
            throw new PayablesException($"{role}{(department is null ? "" : $" ({department})")} has already approved {Reference} in this cycle.");
        }

        _approvals.Add(new InvoiceApproval(ApprovalCycleId, role, department, userId, ApprovalDecision.Approved, evaluationId, null, at));
        LastEvaluationId = evaluationId;
    }

    public void MarkApproved()
    {
        RequireStatus(InvoiceStatus.Submitted, "mark approved");
        Status = InvoiceStatus.Approved;
    }

    public void Override(Guid evaluationId, string ruleId, int ruleVersion, int? distributionLine, UserId userId, string reason, DateTimeOffset at)
    {
        RequireStatus(InvoiceStatus.Submitted, "override");
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleId);
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw new PayablesException("An override requires a reason.");
        }

        if (ActiveOverrides.Any(o => o.RuleId == ruleId && o.DistributionLine == distributionLine))
        {
            throw new PayablesException($"{ruleId} on line {distributionLine?.ToString() ?? "document"} is already overridden in this cycle.");
        }

        _overrides.Add(new InvoiceOverride(ApprovalCycleId, evaluationId, ruleId, ruleVersion, distributionLine, ContentVersion, userId, reason.Trim(), at));
    }

    /// <summary>Fingerprint правил изменился после согласований: прежние решения становятся историей.</summary>
    public void OpenNewApprovalCycle()
    {
        if (Status is not (InvoiceStatus.Submitted or InvoiceStatus.Approved))
        {
            throw new PayablesException($"Cannot restart approvals: invoice {Reference} is {Status}.");
        }

        ApprovalCycleId = Guid.NewGuid();
        Status = InvoiceStatus.Submitted;
    }

    public void Reject(ApproverRole role, UserId userId, string reason, DateTimeOffset at)
    {
        RequireInFlight("reject");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        _approvals.Add(new InvoiceApproval(ApprovalCycleId, role, null, userId, ApprovalDecision.Rejected, LastEvaluationId, reason.Trim(), at));
        ReturnToDraft();
    }

    public void Withdraw(UserId userId, string reason, DateTimeOffset at)
    {
        RequireInFlight("withdraw");
        ArgumentException.ThrowIfNullOrWhiteSpace(reason);
        if (userId != CreatedBy)
        {
            throw new PayablesException($"Only the author can withdraw {Reference}.");
        }

        ReturnToDraft();
    }

    public void Post(Guid evaluationId, DateTimeOffset at)
    {
        RequireStatus(InvoiceStatus.Approved, "post");
        Status = InvoiceStatus.Posted;
        LastEvaluationId = evaluationId;
        PostedAt = at;
    }

    public void SetPaymentHold(bool hold) => PaymentHold = hold;

    /// <summary>Готовность к передаче в платёжный модуль, не разрешение платить (spec §5, правило 9).</summary>
    public bool IsReadyForPaymentHandoff(bool vendorActive, DateOnly businessDate) =>
        Status == InvoiceStatus.Posted && vendorActive && !PaymentHold && Dates.Due <= businessDate;

    private void ApplyContent(Guid vendorId, InvoiceDates dates, Money total, string? poNumber, IReadOnlyList<DistributionInput> distributions)
    {
        if (total <= Money.Zero)
        {
            throw new PayablesException($"Invoice total must be positive, got {total}.");
        }

        var po = string.IsNullOrWhiteSpace(poNumber) ? null : poNumber.Trim();
        if (distributions.Any(d => d.Amount <= Money.Zero))
        {
            throw new PayablesException("Distribution amounts must be positive.");
        }

        if (po is null && distributions.Any(d => d.PoLineNo is not null))
        {
            throw new PayablesException("A distribution references a PO line, but the invoice has no PO.");
        }

        VendorId = vendorId;
        Dates = dates;
        Total = total;
        PoNumber = po;
        _distributions.Clear();
        _distributions.AddRange(distributions.Select((d, i) => new InvoiceDistribution(i + 1, d.Account, d.Amount, d.PoLineNo)));
    }

    private void ReturnToDraft()
    {
        Status = InvoiceStatus.Draft;
        ApprovalCycleId = Guid.NewGuid();
    }

    private void RequireInFlight(string action)
    {
        if (Status is not (InvoiceStatus.Submitted or InvoiceStatus.Approved))
        {
            throw new PayablesException($"Cannot {action}: invoice {Reference} is {Status}.");
        }
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

Замечание к тесту `Content_change_after_withdraw_…`: overrides неактивны уже после Withdraw (новый цикл); тест дополнительно фиксирует, что и `ContentVersion` отсекает их.

Репозитории, `namespace GovErp.Domain.Payables.Repositories;`, по файлу на интерфейс:
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
    Task<IReadOnlyList<PurchaseOrder>> ListAsync(CancellationToken ct = default);
}

public interface IVendorInvoiceRepository
{
    Task<VendorInvoice?> FindAsync(Guid id, CancellationToken ct = default);
    Task<IReadOnlyList<VendorInvoice>> ListAsync(CancellationToken ct = default);
    /// <summary>Есть ли другой инвойс (любой статус) того же поставщика с тем же нормализованным номером. Гонку закрывает уникальный индекс (план 3).</summary>
    Task<bool> ExistsDuplicateAsync(Guid vendorId, string normalizedNumber, Guid excludingInvoiceId, CancellationToken ct = default);
    Task AddAsync(VendorInvoice invoice, CancellationToken ct = default);
}
```

- [ ] **Step 8: Прогнать**

Run: `dotnet test tests/GovErp.Domain.Payables.Tests`
Expected: 30 passed (8 + 14 + 8).

- [ ] **Step 9: Commit**

```bash
git add src/GovErp.Domain.Payables tests/GovErp.Domain.Payables.Tests
git commit -m "Payables: vendor, purchase order billing claims, invoice lifecycle with approval cycles

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
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

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>"
git push
```

---

## Self-review

**Покрытие спеки (разделы 3.1–3.4):** Shared kernel — задачи 2–3 (все VO из 3.1). ChartOfAccounts — задачи 4–5 (Fund, Department, ObjectCodeDefinition, Grant, AccountCombination; CombinationRule — сознательно исключён, спека правится в задаче 1). Ledger — задачи 6–7 (BudgetLine с Reserve/Commit/Release/Amend, Encumbrance, JournalEntry с балансом по фонду, FiscalPeriod, четыре порта). Payables — задача 8 (Vendor, PurchaseOrder, VendorInvoice с конечным автоматом, три порта). Раздел 6.1 (структура решения, `Directory.Build.props`) — задача 1. Раздел 7 (архитектурные тесты, кроме `CA-10`) — задача 9.

**Не покрыто этим планом (намеренно):** Validation (план 2), `IAuditTrail`, `ITenantContext`, EF, seed, UI.

**Согласованность имён между задачами:** `BudgetControlMode` существует в двух контекстах (ChartOfAccounts и Ledger) — намеренно, маппинг в Application. `ReservationResult.ReservationId` — `Guid?`; `VendorInvoice.Submit` принимает `IReadOnlyList<Guid>`. `JournalLine` — positional-подобный record с проверкой в конструкторе; `JournalEntry.Create` принимает `IReadOnlyList<JournalLine>`. `FiscalPeriod` — сущность (не VO), у `JournalEntry` — `PeriodYear`/`PeriodMonth`. Тестовые счета AP `701-0000-2100` требуют `DepartmentCode("0000")` — формат `^[0-9]{4}$` допускает.
