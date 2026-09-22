# Chart of Accounts & Fund/Budget Validation Engine — дизайн

Дата: 2026-09-21. Статус: утверждён к реализации.

Прототип для архитектурного трека «Government ERP Candidate Exercise».
Формат: 15 минут демо + 15 минут вопросов. Оценивают reasoning, контроли, сквозную связность — не визуал.

Архитектурные правила проекта — `docs/ARCHITECTURE.md` (манифест) поверх скилла `ca-ddd-rules`.
Термины — `docs/GLOSSARY.md`.

---

## 1. Цель и границы

**Делаем:** рабочее приложение, которое принимает AP-инвойс, прогоняет его через восьмишаговый
конвейер валидации, выдаёт результат Allowed / Warning / Soft Stop / Hard Stop с объяснением,
проводит инвойс по жизненному циклу Save → Submit → Approve → Post с перевалидацией на каждом
шаге, пишет проводки в журнал и ведёт неизменяемый аудит.

**Сценарии демо** (все на seed-данных):

1. Non-PO инвойс $160,000 по `701-6000-53100-G-COPS-26` → **Hard Stop, превышение $13,000** (сценарий задания)
2. Тот же после budget amendment +$13,000 → **Soft Stop** (procurement threshold) → Submit → override → **Warning** (остаток 0 < 10%) → Approve → Post
3. PO-backed инвойс $160,000 → **Allowed**, encumbrance ликвидирована, available не изменился
4. Мультифондовый инвойс $30,000 на три фонда (101 / 202 / 501) → баланс по фондам, Soft Stop на 101, Expense vs Expenditure
5. Concurrency-тесты: два параллельных Submit на одну бюджетную строку и на один остаток PO — проходит ровно один

**Не делаем** (описывается на слайдах): платёж и банковская интеграция; payroll; receipts / three-way
match; UI управления справочниками и правилами; закрытие года (lapse / carryforward); внешние
сервисы (SAM.gov) — только атрибут vendor; outbox и доменные события; битемпоральность
(есть effective dating, нет recorded_at); pre-encumbrance.

---

## 2. Допущения предметной области

### 2.1 Тенанты и пользователи

- Два тенанта: **City of Springfield** (все сценарии) и **City of Shelbyville** (только справочники —
  демонстрация изоляции). Пользователь принадлежит одному тенанту.
- Роли: `ApClerk`, `DepartmentHead`, `GrantsManager`, `BudgetOfficer`, `FinanceDirector`.
  Seed — по одному пользователю на роль, фиксированные пароли.
- Разделение обязанностей: автор инвойса не может его согласовать; override Soft Stop — только
  `BudgetOfficer` или `FinanceDirector`.

### 2.2 План счетов (Springfield)

Сегменты: `Fund - Department - Object - Grant`. Grant опционален, кроме фондов с `GrantPolicy = Required`.

| Fund | Название | Тип | Basis | Budget control | Grant | Allowed depts | Allowed objects |
|---|---|---|---|---|---|---|---|
| 101 | General Fund | Governmental | Modified accrual | Soft | Forbidden | все | все расходные |
| 202 | Street Fund | Governmental | Modified accrual | Hard | Forbidden | 4000 | 53100, 54000, 55000 |
| 501 | Water Enterprise Fund | Enterprise | Full accrual | Soft | Forbidden | 5000 | все расходные |
| 701 | Grants Fund | Governmental | Modified accrual | Hard | **Required** | все | все расходные |

Департаменты: 3000 Police, 4000 Public Works, 5000 Water Utility, 6000 Fire.
Object-коды: 53100 Professional Services, 54000 Supplies, 55000 Capital Outlay (Expenditure);
2100 Accounts Payable.

Гранты: `G-COPS-26` — федеральный (DOJ), период 2025-07-01 … 2027-06-30, allowable objects 53100 и 54000,
allowed departments 3000 и 6000. `G-FEMA-24` — закрыт.

Допустимые комбинации — whitelist ~15 строк с effective dates; одна `Inactive` по `G-FEMA-24`.

### 2.3 Бюджет и encumbrances (FY2026 = 2025-07-01 … 2026-06-30)

| Account | Amended | Actuals | Encumbered | Available | Назначение |
|---|---|---|---|---|---|
| 701-6000-53100-G-COPS-26 | 375,000 | 132,000 | 96,000 (PO 60k + 36k, чужие) | **147,000** | сценарии 1–2 |
| 701-3000-53100-G-COPS-26 | 500,000 | 100,000 | 160,000 (PO-2026-0451, наш) | 240,000 | сценарий 3 |
| 101-6000-53100 | 50,000 | 40,000 | 0 | 10,000 | сценарий 4, Soft Stop на 12,000 |
| 202-4000-53100 | 25,000 | 5,000 | 0 | 20,000 | сценарий 4, 8,000 проходит |
| 501-5000-53100 | 60,000 | 30,000 | 0 | 30,000 | сценарий 4, 10,000 проходит |

- Уровень бюджетного контроля — полная комбинация из четырёх сегментов.
- Non-PO инвойс не ликвидирует encumbrance.
- PO-backed: ликвидация в пределах свободного (не захваченного другими инвойсами) остатка encumbrance;
  излишек над ликвидацией проверяется против available. Допуск считается накопительно от **утверждённой**
  суммы PO-строки (§5, правило 4): накопленное превышение ≤ 5% — Warning; > 5% — Hard Stop.
- Бюджетный ключ — `(AccountCode, FiscalYear)`; distributions одного инвойса с одним ключом проверяются суммарно.
- Year-end policy — вне scope.

### 2.4 Пороги и маршруты согласования

- Non-PO инвойс с total ≥ 25,000 → Soft Stop `PROCUREMENT_THRESHOLD`, override `FinanceDirector`.
- Маршрут: всегда `DepartmentHead` департамента каждой distribution; при любом гранте — `GrantsManager`;
  при total ≥ 50,000 — `FinanceDirector`; при каждом не снятом Soft Stop — роль, которая может его снять.
- Warning `BUDGET_LOW_REMAINING`: после операции остаток < 10% от amended.

### 2.5 Учёт

- Проводка инвойса по каждой distribution: Дт Expenditure (governmental) / Expense (enterprise) по
  комбинации; Кт AP `<fund>-2100`. Баланс проверяется по каждому фонду отдельно.
- PO-backed добавляет бюджетную пару: Дт Reserve for Encumbrances `<fund>-2900` / Кт Encumbrances по комбинации.
- Payment — только вычисляемый `ReadyForPaymentHandoff` (§5, правило 9); проводок оплаты нет.
- Демо-дата документа (InvoiceDate = ServiceDate = PostingDate) и BusinessDate — **2026-06-15**, FY2026.
  Fiscal periods: 2026-06 открыт; 2026-05 закрыт.
- Seed-остатки таблицы 2.3 загружаются как `OpeningBalance` (AsOfDate 2026-06-01), а не как вымышленные проводки.
- Суммы — только USD, `decimal(18,2)`; значение с более чем двумя знаками после запятой отклоняется, не округляется.

### 2.6 Вопросы к SME (на слайд)

Уровень бюджетного контроля (полная комбинация vs fund+dept); закрытие PO при частичном инвойсе;
year-end lapse vs carryforward для грантов; pooled cash и due to/from; нужны ли budgetary-проводки
для enterprise-фондов; StateRAMP / data residency у целевых клиентов; какие штаты предписывают план счетов.

---

## 3. Контексты, агрегаты, инварианты

### 3.1 Shared kernel — `GovErp.Domain.Shared`

Только значения. Ни сущностей, ни портов.

| Value object | Правила |
|---|---|
| `Money` | `decimal(18,2)`, USD; больше двух знаков после запятой — ошибка создания; арифметика возвращает новые значения; для сумм ≥ 0, для дельт — любой знак |
| `FundCode`, `DepartmentCode`, `ObjectCode`, `GrantCode` | непустая строка фиксированного формата |
| `AccountCode` | `Fund`, `Department`, `Object`, `Grant?`; `ToString()` → `701-6000-53100-G-COPS-26` |
| `FiscalYear` | `Start`, `End`, `Contains(date)`; из даты по правилу «1 июля» |
| `TenantId`, `UserId` | обёртки с проверкой на пустоту |

### 3.2 ChartOfAccounts

| Агрегат | Состояние | Методы | Инварианты |
|---|---|---|---|
| `Fund` | `Code`, `Name`, `FundType`, `AccountingBasis`, `BudgetControlMode`, `GrantPolicy`, `AllowedDepartments`, `AllowedObjects`, `IsActive` | анемичный | — |
| `Department`, `ObjectCode` | код, имя, `IsActive`; `ObjectCode.Category` (Expenditure / Asset / Liability / Budgetary) | анемичные | — |
| `Grant` | `Code`, `Sponsor`, `IsFederal`, `Period`, `AllowedDepartments`, `AllowableObjects`, `Status` | `CheckEligibility(serviceDate, dept, object)` | — |
| `AccountCombination` | `Code: AccountCode`, `Status` (Pending / Active / Inactive), `EffectiveFrom/To`, `ApprovedBy` | `Approve(user)`, `Deactivate(date)` | нельзя `Approve` активную; `EffectiveTo ≥ EffectiveFrom` |

Правила сочетаний выражены атрибутами `Fund` (`GrantPolicy`, `AllowedDepartments`, `AllowedObjects`) и
`Grant` (`AllowedDepartments`, `AllowableObjects`) плюс whitelist `AccountCombination`; отдельная сущность
`CombinationRule` и её интерпретатор не вводятся.

Репозитории: `IFundRepository`, `IGrantRepository`, `IAccountCombinationRepository`,
`IReferenceDataRepository`.

### 3.3 Ledger

| Агрегат | Состояние | Методы | Инварианты |
|---|---|---|---|
| `BudgetLine` | `Account`, `FiscalYear`, `ControlMode`, `Adopted`, `Amendments[]`, `Actuals`, `Encumbered`, `Reservations[]` (`Id`, `InvoiceId`, `ContentVersion`, `SourceRef`, `Amount`, `Status: Held / Committed / Released`), `OpeningBalanceId`, `ChangeStamp` | `ApplyOpeningBalance(opening)`, `Amend`, `Reserve(invoiceId, contentVersion, amount, sourceRef) → ReservationResult`, `Commit(reservationId, invoiceId, contentVersion)`, `Release(reservationId, invoiceId, contentVersion)`, `RecordLiquidation(amount)`, `OwnHeld(invoiceId, contentVersion)`, `AvailableForInvoice(invoiceId, contentVersion)` | `Amended = Adopted + ΣAmendments`; `Available = Amended − Actuals − Encumbered − ΣHeld`; `AvailableForInvoice = Available + OwnHeld`; при Hard `Reserve` отказывает, если `Available < amount`; при Soft резервирует с признаком превышения; `Commit` / `Release` — только владельцем (инвойс + версия содержания); `RecordLiquidation`: `Encumbered −= amount`, `Actuals += amount`; opening balance — один раз и только на нетронутую строку; любое изменение увеличивает `ChangeStamp` |
| `Encumbrance` | `PoLineRef`, `Account`, `Original`, `Liquidated`, `Released`, `AuthorizedPoAmount`, `AlreadyPostedAgainstPo`, `Claims[]` (ликвидируемая часть), `BillingClaims[]` (полная PO-сумма инвойса); у обеих коллекций `InvoiceId`, `ContentVersion`, `Amount`, `Status: Held / Consumed / Released`; `ChangeStamp` | `ClaimableForInvoice(invoiceId, contentVersion)`, `Claim`, `ConsumeClaim`, `ReleaseClaim`, `ClaimBilling(invoiceId, contentVersion, amount, tolerance)`, `ConsumeBillingClaim`, `ReleaseBillingClaim`, `ReleaseRemainder` | `Remaining = Original − Liquidated − Released`; `Σ Held claims ≤ Remaining`; **`AlreadyPostedAgainstPo + Σ Held billing claims + amount ≤ AuthorizedPoAmount × (1 + tolerance)`** — проверяет сам агрегат (§5, правило 4); `ConsumeClaim` увеличивает `Liquidated`, `ConsumeBillingClaim` — `AlreadyPostedAgainstPo`; claim меняет только его владелец |
| `OpeningBalance` | `Account`, `FiscalYear`, `AsOfDate`, `InitialActuals`, `InitialEncumbered`, `SourceReference` | неизменяемый | дата в пределах бюджетного года, суммы ≥ 0 |
| `JournalEntry` | `SourceRef`, `PeriodYear/Month`, `PostedAt`, `PostedBy`, `Lines[]` (`Account`, `LedgerFamily: Financial / Budgetary`, `Debit`, `Credit`, `Description`) | фабрика `Create(sourceRef, lines, period, actor, at)`; неизменяем | по каждой паре (фонд, семейство) `ΣDebit = ΣCredit`; ≥ 2 строк; период открыт; повторная проводка исключена уникальным `SourceRef` и receipt команды |
| `FiscalPeriod` | `Year`, `Month`, `Status` | `Close()` | — |

Billing claim живёт в Ledger рядом с encumbrance, а не на строке PO. Допуск передаётся из параметра правила `PO_LIQUIDATION` (`tolerance_pct`), значение по умолчанию в агрегате не используется.

Инвариант Post по каждой бюджетной строке: `Actuals` растёт ровно на сумму distributions инвойса (`Commit` собственного резерва + `RecordLiquidation` погашенного claim); `Encumbered` уменьшается на погашенные claims; `Held` — на собственные резервы.

Репозитории: `IBudgetLineRepository`, `IEncumbranceRepository`, `IJournalRepository`, `IFiscalPeriodRepository`.

### 3.4 Payables

| Агрегат | Состояние | Методы | Инварианты |
|---|---|---|---|
| `Vendor` | `Code`, `Name`, `Status` (Active / Inactive / Debarred), `SamRegistered` | анемичный | — |
| `PurchaseOrder` | `Number`, `VendorId`, `Status`, `Lines[]` (`LineNo`, `Account`, `Amount`) | анемичный (seed) | уникальные номера строк, положительные суммы |
| `VendorInvoice` | `Number`, `NormalizedInvoiceNumber`, `VendorId`, `InvoiceDate`, `ServiceDate`, `PostingDate`, `DueDate`, `Total`, `PoRef?`, `Distributions[]`, `Status`, `ContentVersion`, `ApprovalCycleId`, `LastEvaluationRef`, `RuleFingerprint`, маршрут (`ApprovalRequirement[]`: роль + департамент), `Approvals[]` (с циклом, версией содержания и fingerprint), `Overrides[]` (`OverrideTarget`: оценка, outcome, правило, версия, строка, версия содержания, цикл), `Withdrawals[]`, ссылки на свои резервы и claims (`ReservationRefs`, `EncumbranceClaimRefs`, `PoBillingClaimRefs`), `PaymentHold` | `UpdateHeader`, `AddDistribution`, `RemoveDistribution`, `Submit(evaluation, fingerprint, route, refs)`, `RecordApproval(role, department, user, evaluation)`, `MarkApproved`, `Override(target, user, reason)`, `Reevaluate(evaluation, contentVersion, fingerprint, route)`, `Reject → InvoiceRelease`, `ReturnToDraft`, `Withdraw → InvoiceRelease`, `Post(evaluation, contentVersion, cycle, fingerprint)`, `SetPaymentHold`, `ReadyForPaymentHandoff(vendorActive, businessDate)` | `ΣDistributions = Total` при `Submit`; содержание меняется только в `Draft` и увеличивает `ContentVersion`; согласует только шаг маршрута (роль + свой департамент) и не автор; `MarkApproved` — когда все шаги согласованы в текущем цикле, версии и fingerprint; `Reevaluate` открывает новый цикл при смене fingerprint **или маршрута**; override — не автором и только для текущей оценки, цикла и версии; `Post` — только согласованные оценка, цикл, версия и fingerprint; `Withdraw` — только автор, до `Post`; история согласований и отзывов не удаляется |

Статусы: `Draft → Submitted → Approved → Posted`; `Reject` переводит в `Rejected`.

Маршрут и fingerprint хранятся на инвойсе намеренно: повторное согласование. Ссылки на резервы и claims дублируют владельца, записанного в Ledger; расхождение ловит проверка владельца в `Commit` / `ConsumeClaim`.

Репозитории: `IVendorInvoiceRepository`, `IPurchaseOrderRepository`, `IVendorRepository`.

### 3.5 Validation

| Тип | Что это | Состояние |
|---|---|---|
| `RuleDefinition` (агрегат, анемичный) | Запись о правиле | `RuleId`, `Version`, `Step (1..8)`, `Layer (Core / Federal / State / Tenant)`, `ScopeFund?`, `ScopeGrant?`, `Severity?`, `Parameters` (json), `OverridableBy[]`, `Effective*`, `Message`, `Resolution`, `IsEnabled` |
| `ValidationSubject` (value object) | Все входы конвейера — снимки | `TransactionSnapshot` (ref, версия содержания, цикл, fingerprint, статус, даты, сумма, vendor, PO?, дубликат, PaymentHold); по distribution — `CombinationSnapshot`, `FundSnapshot`, `GrantSnapshot?`, `BudgetSnapshot` (с `OwnHeld`), `EncumbranceSnapshot?` (остаток, чужие и свои claims, утверждённая и выставленная сумма PO); согласования и overrides активного цикла; `PeriodIsOpen`; `BusinessDate` |
| `EffectiveRuleSet` | Набор правил на дату и scope (фонд, грант) | определения, `RuleSetVersions` (максимумы по слоям — для отображения, `AppliedRules`, `Fingerprint`) |
| `ValidationPipeline`, `BudgetAllocation`, `ApprovalRouteResolver`, `PostingPreviewBuilder`, `PostingEligibility` (доменные сервисы) | конвейер; распределение ликвидации по строкам; шаг 7; preview; шаг 8 | без состояния |
| `EvaluationRecord` (агрегат, неизменяемый) | Результат оценки | ref, версия содержания, цикл, trigger, время, актор, `RuleSetVersions` с fingerprint, outcomes (у каждого `OutcomeRef` для привязки override), `Overall`, `Capabilities`, маршрут, preview, `PostingCheck?`, снимок входов |

Инварианты `EvaluationRecord`: `Overall = max(Outcomes.Severity)` без применённых overrides; `Capabilities` — функция от `Overall` и `PostingCheck`; создаётся один раз.

Fingerprint — SHA-256 по бинарной сериализации (с префиксами длины) всех применённых определений: id, версия, слой, шаг, scope, severity, включённость, effective dates, сообщения, параметры, роли override, плюс версия engine и наборы по scope.

Репозитории: `IRuleDefinitionRepository`, `IEvaluationRecordRepository`.

Правила — типизированный код, не DSL. `RuleDefinition` в БД — включённость, версия, severity, параметры.

### 3.6 Audit — порт, не контекст

`IAuditTrail` объявлен в `GovErp.Application.Web/Audit/`, реализация `EfAuditTrail`. `AuditEvent` — DTO:
`TenantId`, `OccurredAt`, `Actor`, `Action`, `SubjectRef`, `CorrelationId`, `Payload`. Пишется в той же
транзакции, что и изменение агрегата. Таблица `audit.Events` — только INSERT.

### 3.7 Как контексты видят друг друга

Только через Application и только по идентификаторам / кодам. `ValidationSubjectAssembler`
(Application) — единственный класс, который читает все четыре контекста.

---

## 4. Конвейер, правила, result model

### 4.1 Алгоритм

```
Evaluate(subject, ruleSet, trigger, actor, at):
  for step in 1..6:
      outcomes += rules of step (subject)
      if outcomes.Any(HardStop): break
  (outcomes, overall) = Aggregate(outcomes, subject.ActiveOverrides)   // override снимает только свой SoftStop
  route    = ApprovalRouting.Build(subject, outcomes)                   // шаг 7, всегда
  preview  = overall ≤ SoftStop ? PostingPreview.Build(subject) : []
  check    = trigger == Post ? PostingEligibility.Check(...) : null      // шаг 8
  return EvaluationRecord(..., ruleSet.AppliedRules, ruleSet.Fingerprint, ...)
```

Разрешение конфликтов:
1. Внутри шага — все правила шага выполняются, результат = строжайший.
2. Между шагами — Hard Stop на шагах 1–6 прерывает; Soft / Warning накапливаются.
3. Правила разрешаются на дату и **scope** (фонд, грант distribution): определение со `ScopeFund` /
   `ScopeGrant` действует только для своих строк. Одинаковый `RuleId` в нескольких слоях — побеждает самый
   специфичный слой (`Tenant > State > Federal > Core`), внутри слоя — старшая версия, **но только если он не
   ослабляет ни один вышестоящий**: не ниже severity, не шире роли override, параметры — не мягче
   (`threshold`, `finance_director_threshold`, `tolerance_pct` — не выше; `pct` — не ниже; прочие параметры
   менять нельзя, а у правил без override нельзя менять никакие). Ослабление — ошибка конфигурации; та же
   проверка выполняется при seed и сохранении правила, чтобы ошибка не всплывала впервые при оценке.
   Так обязательное ограничение не исчезает при локальном переопределении. Это допущение демо, а не
   утверждение о правовой иерархии США.

Override меняет агрегацию, не outcome. Override привязан к `OutcomeRef` конкретной оценки, `RuleId` +
`RuleVersion`, строке distribution, `ContentVersion` и активному `ApprovalCycleId`; на другую строку,
версию содержания или цикл не переносится; автор инвойса override не делает.

Шаг 6 (encumbrance impact) вычислительно предшествует шагу 5: `BudgetAllocation` распределяет по строкам
каждой PO-строки ликвидацию `min(amount, ClaimableForInvoice)` (для уже поданного инвойса — его собственный
claim) и проверяет согласованность снимков; `BUDGET_AVAILABILITY` суммирует по бюджетному ключу
`RequiredNewBudget = Σ amount − Σ liquidation` и сравнивает с `AvailableForInvoice = Available + OwnHeld`.
Правило шага 6 отчитывается о ликвидации и накопительном допуске PO.

### 4.2 Правила демо

| # | RuleId | Шаг | Layer | Severity | Проверяет | Параметры |
|---|---|---|---|---|---|---|
| 1 | `SEG_REQUIRED` | 1 | Core | Hard | Grant — по `GrantPolicy` (Fund/Dept/Object гарантированы типом) | — |
| 2 | `SEG_GRANT_FORBIDDEN` | 1 | Core | Hard | Grant при `GrantPolicy = Forbidden` | — |
| 3 | `COA_COMBINATION_ACTIVE` | 2 | Core | Hard | комбинация в whitelist, Active, в effective-окне на InvoiceDate | — |
| 4 | `FUND_DEPT_OBJECT_ALLOWED` | 3 | Tenant | Hard | dept ∈ `Fund.AllowedDepartments`, object ∈ `Fund.AllowedObjects` | — |
| 5 | `GRANT_ELIGIBLE` | 3 | Federal | Hard | грант активен, ServiceDate в периоде, dept и object разрешены | — |
| 6 | `VENDOR_ELIGIBLE` | 4 | Federal | Hard | vendor Active (не Debarred / Inactive); для федерального гранта — `SamRegistered` | — |
| 7 | `PROCUREMENT_THRESHOLD` | 4 | State | Soft | non-PO и total ≥ threshold | `threshold: 25000`; override `FinanceDirector` |
| 8 | `INVOICE_DUPLICATE` | 4 | Core | Hard | тот же vendor + `NormalizedNumber` у другого инвойса (любой статус) | — |
| 9 | `BUDGET_AVAILABILITY` | 5 | Core | по фонду | `ProjectedAvailable < 0` по бюджетному ключу → Hard при `ControlMode = Hard`, Soft при Soft; нет бюджетной строки → Hard | override (Soft): `BudgetOfficer`, `FinanceDirector` |
| 10 | `BUDGET_LOW_REMAINING` | 5 | Tenant | Warning | `0 ≤ ProjectedAvailable / Amended < pct` | `pct: 0.10` |
| 11 | `PO_LIQUIDATION` | 6 | Core | Allowed / Warning / Hard | PO-строка открыта, `AuthorizedAmount > 0`; `CumulativeExcessPct` = 0 → Allowed (информационный), ≤ tolerance → Warning, > tolerance → Hard | `tolerance_pct: 0.05` |
| 12 | `APPROVAL_ROUTE` | 7 | Tenant | — | параметры маршрута | `finance_director_threshold: 50000` |

Синтетический outcome `REVALIDATION_REQUIRED` (Hard) выдаёт `PostingEligibility`, если fingerprint или
`ContentVersion` отличаются от активного цикла согласования. Он снимается новым согласованием
по актуальному fingerprint, а не является вечным блоком. Конфликт конкурентности не превращается в outcome:
команда возвращает retryable `Conflict`.

### 4.3 Result model

```
Severity: Allowed < Warning < SoftStop < HardStop

RuleOutcome
  OutcomeRef, RuleId, RuleVersion, Step, Layer, DistributionLine?
  Severity
  Inputs   (json)     { amended, actuals, encumbered, held, ownHeld, requiredNewBudget, ... }
  Computed (json)     { availableForInvoice, projectedAvailable, overage, ... }
  Message, Resolution
  OverridableBy[], OverriddenBy? { rule, user, reason, at }
```

| Overall | Save | Submit | Approve | Post | Payment handoff |
|---|---|---|---|---|---|
| Allowed | ✓ | ✓ | ✓ | ✓ при `PostingCheck` | вычисляется: Posted ∧ vendor Active ∧ ¬PaymentHold ∧ DueDate ≤ BusinessDate |
| Warning | ✓ | ✓ | ✓ | ✓ при `PostingCheck` | то же |
| SoftStop (не снят) | ✓ | ✓ (Soft-фонд удерживает резерв с дефицитом) | ✗ | ✗ | ✗ |
| SoftStop (снят) | ✓ | ✓ | ✓ | ✓ при `PostingCheck` | то же |
| HardStop | ✓ | ✗ | ✗ | ✗ | ✗ |

`PostingEligibility`: `Overall ≤ Warning` (с overrides) ∧ каждый шаг маршрута удовлетворён согласованием
**активного** цикла (DepartmentHead — только своего департамента) ∧ период PostingDate открыт ∧ preview
сбалансирован по каждой паре (фонд, семейство) ∧ `ContentVersion` и `RuleSetFingerprint` совпадают с теми,
по которым было последнее согласование цикла.

`ApprovalRouteResolver`: по правилам 2.4; шаг маршрута = (роль, департамент?); для каждого — причина и признак
удовлетворённости согласованием активного цикла.

### 4.4 Explanation — отдельно от решения

Порт `IExplanationGenerator.Explain(EvaluationRecord, audience) → ExplanationResult` в Application.
Вход — только сохранённая запись. Выход сохраняется в `validation.Explanations` с полями `Provider`,
`Model`, `GeneratedAt`, `PromptVersion`, `FallbackReason?`.

Реализации в Infrastructure:
- `TemplateExplanationGenerator` — детерминированный текст.
- `LlmExplanationGenerator` — строит промпт (system: «объясни для аудитории X только на основании данных
  ниже; не добавляй фактов; не оценивай правильность решения»; user: сериализованный `EvaluationRecord`),
  вызывает `Microsoft.Extensions.AI.IChatClient`, таймаут 10 с; guardrail — все денежные суммы из ответа
  должны присутствовать во входе; любая ошибка / отклонение → fallback на Template с `FallbackReason`.

Провайдер — конфигурация развёртывания (`Explanation:Provider = Template | Anthropic | OpenAI | Ollama`),
не выбор пользователя. Реализуются и проверяются Anthropic и Ollama; OpenAI — подключён пакетом и конфигом.
Текст объяснения не читается ни одним правилом: генератор живёт в Application / Infrastructure, конвейер в Domain.

---

## 5. Сценарии, жизненный цикл, консистентность

### 5.1 App-сервисы (`GovErp.Application.Web`)

Все принимают `ActorContext(tenantId, userId, roles)` явно.

| Сервис | Методы |
|---|---|
| `IInvoiceAppService` | `CreateDraft`, `CreateFromPreset`, `UpdateDraft`, `Validate`, `Submit`, `Withdraw`, `SetPaymentHold`, `GetInvoice`, `ListInvoices` |
| `IApprovalAppService` | `GetQueue`, `Approve`, `Reject`, `Override(evaluationId, ruleId, distributionLine?, expectedRowVersion, reason)` |
| `IPostingAppService` | `Post`, `GetJournal` |
| `IExplanationAppService` | `Explain(evaluationId, audience)`, `GetEvaluationHistory(invoiceId)`, `GetAuditTrail(invoiceId)` |
| `IBudgetAppService` | `Amend(account, fiscalYear, amount, reference, effectiveDate)`, `ListBudgetLines` |
| `IReferenceAppService` | `GetSegments`, `GetCombinations`, `GetRules`, `GetVendors`, `GetPurchaseOrders` |

Каждая mutating-команда несёт `CommandId` и `ExpectedRowVersion` и возвращает `CommandResult`:
`Accepted` / `Refused` (бизнес-отказ с оценкой) / `Conflict` (retryable) / `Forbidden`.

`ValidationSubjectAssembler` — вспомогательный компонент Application, собирает снимки из четырёх контекстов.

### 5.2 Атомарные команды

Create/Update, Submit, Approve, Override, Reject/Withdraw, Post — каждая команда выполняется в одной транзакции tenant-БД. Submit и Reject не имеют промежуточных коммитов и компенсаций. CommandId, actor, request hash и сохранённый результат обеспечивают идемпотентность через `ap.ProcessedCommands`.

### 5.3 Submit

Начать транзакцию, проверить receipt и RowVersion, загрузить данные и сгруппировать distributions по budget/PO key. Вычислить AvailableForInvoice = Amended - Actuals - Encumbered - Held + OwnHeld. При HardStop сохранить отказ без резервов. Иначе захватить budget reserves, encumbrance claims и PO billing claims, записать Submitted, evaluation, audit и receipt; один SaveChanges/commit. Конфликт — полный rollback; один повтор в новом scope со свежими данными, затем retryable Conflict. Reject/Withdraw атомарно освобождает claims и reserves.

### 5.4 Post

Короткая Serializable-транзакция включает загрузку и перевалидацию, проверку активного цикла согласований/правил/периода, погашение собственных резервов и PO claims, журнал, статус, audit и receipt. Actuals увеличивается ровно на сумму инвойса, Encumbered уменьшается на liquidation, Held уменьшается на собственный резерв. Journal уникален по SourceRef + PostingKind; повтор команды не создаёт второй журнал. Финансовые и бюджетные записи балансируются отдельно внутри фонда. LLM не вызывается внутри транзакции.

### 5.5 Идемпотентность и события

Все mutating-команды несут CommandId и ожидаемый RowVersion. Receipt сохраняется в той же транзакции; повтор того же payload возвращает сохранённый результат, другой payload под тем же ключом — Conflict. Авторизация проверяется до выдачи receipt. Domain events/outbox не реализуются; описываются как вариант при разделении сервисов.

### Завершённые правила жизненного цикла (22 сентября 2026)

1. **Версии.** `ContentVersion` увеличивается только при изменении суммы, поставщика, дат, PO или distributions. `RowVersion` SQL Server меняется при любой записи и служит optimistic concurrency. Evaluation.TransactionVersion означает ContentVersion. Approve/Override не изменяют ContentVersion. Резервы и PO claims принадлежат InvoiceId + ContentVersion. При Reject/Withdraw старые решения остаются историей, но больше не удовлетворяют новый цикл согласования.
2. **Повторное согласование.** Изменение содержания или fingerprint применимых правил открывает новый ApprovalCycleId и делает старые approvals/overrides неприменимыми. Изменение свободного бюджета вызывает новую оценку без автоматического сброса approvals; новый HardStop блокирует действие, новый SoftStop требует своего override. Сравнение только числового severity недостаточно. Post требует активный цикл с тем же ContentVersion и fingerprint.
3. **SoftStop на Submit.** В Soft-фонде Submitted может удерживать резерв сверх available до разрешения исключения. UI явно показывает дефицит и не позволяет Post без override. В Hard-фонде недостаток бюджета запрещает резерв. После Reject/Withdraw весь резерв освобождается атомарно.
4. **PO tolerance.** База допуска — утверждённая сумма PO-строки, не её текущий остаток. `ProjectedBilled = AlreadyPostedAgainstPo + OtherActiveInvoiceClaims + CurrentInvoicePoAmount`; `CumulativeExcess = max(0, ProjectedBilled - AuthorizedPoAmount)`. При CumulativeExcess > AuthorizedPoAmount × 0.05 — HardStop. Нужен отдельный claim полной суммы PO-backed инвойса, включая превышение: encumbrance claim захватывает только ликвидируемую часть, а PO billing claim защищает накопленный допуск. Обе суммы меняются атомарно. Change order в демо не редактируется, поддерживается как seed-допущение.
5. **Дубликаты.** `NormalizedInvoiceNumber = Number.Trim().ToUpperInvariant()`. Уникальный индекс `(VendorId, NormalizedInvoiceNumber)` внутри tenant-БД, включая Draft/Rejected/Posted. Предварительная проверка даёт удобную ошибку, индекс закрывает гонку. Reject не освобождает номер. Пресеты всегда создают уникальный номер.
6. **Даты.** InvoiceDate, ServiceDate и PostingDate отдельные поля; для демо все равны 2026-06-15. Бюджетный год и открытый период определяются PostingDate, период допустимости услуги — ServiceDate, effective-правила демо — InvoiceDate. EvaluatedAt/RecordedAt — реальные UTC timestamp. Другие варианты дат отклоняются с объяснением ограничения демо, не молча приводятся к одной дате.
7. **Начальное состояние.** `ledger.OpeningBalances` хранит Account, FiscalYear, AsOfDate, InitialActuals, InitialEncumbered, SourceReference. Seed-остатки — начальный снимок, а не вымышленные проводки. Сверка: opening actuals + проведённые в прототипе финансовые расходы = actuals; encumbered отдельно сверяется с opening PO и его движениями.
8. **Отзыв.** `Withdraw` доступен автору для Submitted/Approved до Post. Одна транзакция освобождает budget/encumbrance/billing claims, закрывает цикл согласований, возвращает Draft и сохраняет причину в audit. После Post редактирование/отзыв запрещены. Корректирующие проводки и credit notes вне объёма.
9. **Готовность к оплате.** Вычисляемый `ReadyForPaymentHandoff = Posted && VendorActive && !PaymentHold && DueDate <= BusinessDate`. Добавить DueDate и PaymentHold; BusinessDate передаётся явно. Это готовность передачи в платёжный модуль, не разрешение отправить деньги. Cash availability, банковские реквизиты и банковский платёж не реализуются. Статус Payable не хранить.
10. **Повтор демо.** Отдельная операторская команда `demo-reset --tenant springfield --confirm springfield` разрешена только при Environment=Demo и признаке IsDemo у тенанта. Проверить allowlist database names и закрыть активные операции на время сброса. Пересоздать только demo tenant-БД, повторить seed; Master и второй тенант не затрагивать. Никакого автоматического сброса при старте и удаления volumes штатной командой запуска. История Demo намеренно сбрасывается, что явно показывается оператору.

Вне объёма: мультивалютность (только USD, decimal(18,2), более двух дробных знаков — ошибка), налоги, credit notes, годовое закрытие, реальные закупочные проверки, изменение PO, банковские интеграции.

---

## 6. Проекты, инфраструктура, UI

### 6.1 Решение

```
src/
  GovErp.Domain.Shared
  GovErp.Domain.ChartOfAccounts       → Shared
  GovErp.Domain.Ledger                → Shared
  GovErp.Domain.Payables              → Shared
  GovErp.Domain.Validation            → Shared
  GovErp.Application.Web              → Shared, 4× Domain
  GovErp.Infrastructure               → Shared, 4× Domain, Application.Web
  GovErp.Web                          → Application.Web, Infrastructure (точка сборки)
tests/
  GovErp.Domain.ChartOfAccounts.Tests
  GovErp.Domain.Ledger.Tests
  GovErp.Domain.Payables.Tests
  GovErp.Domain.Validation.Tests
  GovErp.Application.Web.Tests        → Testcontainers (SQL Server)
  GovErp.Architecture.Tests           → NetArchTest
```

`Directory.Build.props`: `DisableTransitiveProjectReferences=true`, `Nullable=enable`, `TreatWarningsAsErrors=true`.

### 6.2 Infrastructure

Один `GovErpDbContext`, схемы:

| Схема | Таблицы |
|---|---|
| `coa` | Funds, Departments, ObjectCodes, Grants, AccountCombinations |
| `ledger` | BudgetLines, BudgetAmendments, BudgetReservations, Encumbrances, EncumbranceClaims, PoBillingClaims, OpeningBalances, JournalEntries, JournalLines, FiscalPeriods |
| `ap` | Vendors, PurchaseOrders, PurchaseOrderLines, VendorInvoices, InvoiceDistributions, InvoiceApprovals, InvoiceOverrides, InvoiceWithdrawals, ProcessedCommands |
| `validation` | RuleDefinitions, EvaluationRecords (json-колонки: AppliedRules, Outcomes, InputSnapshot, PostingPreview, ApprovalRoute, PostingCheck), Explanations |
| `audit` | Events |

FK — только внутри схемы. `rowversion` на BudgetLines, Encumbrances, VendorInvoices;
`ChangeStamp` на корнях с owned-коллекциями гарантирует UPDATE корня при добавлении резерва/claim.
Уникальные индексы: `ap.VendorInvoices (VendorId, NormalizedInvoiceNumber)`, `ledger.JournalEntries (SourceRef)`,
`ap.ProcessedCommands (CommandId)`. `EvaluationRecords`, `Explanations`, `audit.Events`, `JournalEntries` —
только INSERT: interceptor защищает EF-путь, права runtime-пользователя БД запрещают UPDATE/DELETE
(interceptor не объявляется полной защитой). `AccountCode` — одна колонка `nvarchar(64)` через конвертер.

**Tenancy:** БД `GovErp_Master` (тенанты, пользователи, роли, имя БД тенанта); БД `GovErp_Springfield`,
`GovErp_Shelbyville`. Это логическая изоляция на одном сервере, не физическая. `TenantContext`
инициализируется один раз на операцию из аутентифицированного principal и серверного каталога; tenantId из
формы/URL не определяет connection string. Runtime-пользователь БД на тенанта имеет доступ только к своей БД;
миграции выполняются отдельным migration-пользователем. Миграции и seed при старте — Master, затем цикл по
тенантам; seed только в пустую БД.

**Operation scope:** `DbContext` не живёт весь Blazor circuit — scope и `DbContext` создаются на каждую
команду/запрос; повтор после конфликта — в новом scope.

**Auth:** cookie auth, `IUserRepository` в Master, `PasswordHasher<T>`, роли → claims, `AuthorizeView`.
Без регистрации и восстановления.

**Explanation:** см. 4.4. Ключи из env; провайдер из конфигурации.

**Docker:** `docker-compose.yml` — `sqlserver` (mssql/server:2022, healthcheck, именованный volume) + `web`
(multi-stage Dockerfile). Web ждёт healthcheck, при старте применяет миграции и seed только в пустые БД.
Порт 8080. `.env`: `SA_PASSWORD` (только для migration-пользователя), пароли runtime-пользователей тенантов,
опционально `ANTHROPIC_API_KEY`, `Explanation__Provider`. Обычный restart сохраняет данные; `docker compose down -v`
удаляет их и в штатную проверку не входит. Сброс демо — только команда `demo-reset`.

### 6.3 UI (Blazor Server, Bootstrap из шаблона)

| Страница | Роли | Содержание |
|---|---|---|
| `/login` | все | форма; список seed-пользователей |
| `/invoices` | все | список; кнопки пресетов «Non-PO», «PO-backed», «Multi-fund» → Draft с уникальным номером |
| `/invoices/{id}` | ApClerk — правка и Withdraw; остальные — просмотр | header (три даты, DueDate, PaymentHold), distributions с живым итогом, действия по статусу и Capabilities; вкладки **Results**, **Posting Preview**, **Approval Route**, **Audit**, **Explain** |
| `/approvals` | согласующие роли | очередь по своей роли и департаменту; Approve / Reject / Override с причиной; если оценка изменилась — показ разницы outcome'ов, а не только severity |
| `/budget` | BudgetOfficer, FinanceDirector | строки с opening / amended / actuals / encumbered / held / available; «Amend» с FY и effective date |
| `/rules` | все | RuleDefinition по шагам и слоям, adjustable-флаг, текущий fingerprint — только чтение |

Все кнопки действий удерживают `CommandId` до получения ответа; повторное нажатие не создаёт вторую мутацию.

### 6.4 Демо-путь (5 минут)

1. `ap.clerk` → пресет Non-PO → Validate → Hard Stop, превышение 13,000 → Explain
2. `budget.officer` → `/budget` → Amend +13,000 (FY2026, 2026-06-15) → `ap.clerk`: Validate → Soft Stop (procurement) → Submit (резерв 160,000)
3. `finance.director` → Override `PROCUREMENT_THRESHOLD` → итог **Warning** (остаток 0 < 10%) → Approve; `fire.chief`, `grants.manager` → Approve → `finance.director` → Post → журнал, `/budget`: actuals 292,000 / held 0 / available 0
4. Пресет PO-backed → Validate → Allowed, две пары проводок (Budgetary + Financial), после Post: actuals 260,000, encumbered 0, available 240,000
5. Пресет Multi-fund → три баланса, Expense vs Expenditure, Soft Stop на 101
6. Audit по инвойсу из 1–3: оценки с fingerprint, override, согласования цикла, кто и когда; `ReadyForPaymentHandoff` на BusinessDate 2026-06-15

---

## 7. Тесты

| Проект | Что | Как |
|---|---|---|
| `Domain.Validation.Tests` | конвейер и каждое правило | чистые `ValidationSubject`, без БД и моков |
| `Domain.Ledger.Tests` | `BudgetLine`, `Encumbrance`, `JournalEntry` | юнит |
| `Domain.Payables.Tests` | конечный автомат `VendorInvoice`, `ΣDistributions = Total`, автор ≠ согласующий | юнит |
| `Domain.ChartOfAccounts.Tests` | `AccountCombination`, `Grant.IsEligible` | юнит |
| `Application.Web.Tests` | сценарии против SQL Server | Testcontainers, реальные миграции и seed |
| `Architecture.Tests` | границы | NetArchTest |

Ключевые тесты конвейера: `Scenario_NonPo_701_ExceedsAvailable_By13000_IsHardStop`,
`Scenario_NonPo_701_AfterAmendment13000_IsSoftStop_ProcurementThreshold`,
`Scenario_NonPo_701_AfterAmendment_WithOverride_IsWarning`,
`Scenario_PoBacked_WithinRemaining_IsAllowed_AvailableUnchanged`,
`Scenario_PoBacked_CumulativeExcess3Pct_IsWarning`, `Scenario_PoBacked_CumulativeExcess8Pct_IsHardStop`,
`Scenario_MultiFund_101_Overage_IsSoftStop_202_501_Allowed`, `Scenario_MultiFund_501_UsesExpenseNotExpenditure`,
`Scenario_SameBudgetAcrossDistributions_IsHardStop13000`, `Scenario_OwnReservationIsNotChargedTwice`,
по одному `Rule_*` на каждое правило, `Pipeline_HardStopAtStep2_SkipsSteps3To6_StillBuildsRoute`,
`Resolution_StricterTenantLayerReplacesState`, `Resolution_WeakeningThreshold_IsRejected`,
`Resolution_ScopedRuleAppliesOnlyToItsFundOrGrant`, `Fingerprint_ChangesWithParameters_NotWithOrder`,
`Pipeline_Override_ChangesOverall_NotOutcome`, `Override_OtherLineOrContentVersion_DoesNotApply`,
`Capabilities_*`, `PostingEligibility_FingerprintChanged_RequiresReapproval`,
`PostingEligibility_DepartmentHeadOfOtherDepartment_DoesNotSatisfyStep`,
`PostingPreview_BalancedPerFundAndFamily`, `PostingPreview_PoBacked_HasBudgetaryReversalLines`.

Обязательные интеграционные тесты (настоящий SQL Server; гонки синхронизируются барьером после чтения,
операции — в независимых scope):

| Тест | Условия | Ожидаемый результат |
|---|---|---|
| `OwnReservationIsNotChargedTwice` | Hard, available 147,000; Submit 100,000; Approve; Post | нет повторного дефицита; после Post available 47,000 |
| `SameBudgetAcrossDistributions` | две строки по 80,000 на один бюджетный ключ, available 147,000 | HardStop 13,000; ни одного Held |
| `ParallelBudgetSubmits` | два инвойса по 100,000 при available 147,000 | один Submitted; Held 100,000; второй — отказ или Conflict |
| `ParallelPoClaims` | два инвойса на 96,000 при остатке PO 96,000 и свободном бюджете 0 | только один захватывает PO |
| `PoPostingTotals` | 500,000 / 100,000 / 160,000; инвойс 160,000 | actuals 260,000; encumbered 0; available 240,000 |
| `MixedPostingTotals` | остаток PO 96,000; инвойс 100,000; излишек в допуске | actuals +100,000; encumbered −96,000; available −4,000 |
| `AtomicSubmitFailure` | ошибка на второй бюджетной строке | нет частичных резервов/claims/Submitted |
| `AtomicPostFailure` | ошибка при записи журнала | бюджет, PO, статус не изменились |
| `SameCommandRepeated` | тот же CommandId + payload дважды | одна мутация, один receipt, один журнал |
| `SameCommandDifferentPayload` | тот же CommandId, другой payload | Conflict |
| `ReapprovalAfterRuleChange` | Approve v1; новое правило; Approve v2; Post | Post возможен после актуального согласования |
| `WithdrawReleasesEverything` | Submitted PO-backed инвойс; Withdraw автором | Held, encumbrance и billing claims освобождены; статус Draft; история согласований сохранена |
| `DuplicateNumberRace` | два Create с одним vendor + номером (разный регистр/пробелы) | один успешен, второй — отказ по уникальному индексу |
| `TenantIsolation` | `shelby.clerk` читает инвойсы / Id из Springfield | пусто / NotFound |
| `AppendOnlyViaEf` | изменение EvaluationRecord через DbContext | исключение interceptor'а |
| `ExplanationTemplateNeverCallsChatClient`, `ExplanationLlmFailureFallsBack` | Template / сбой LLM | текст Template, `FallbackReason` записан |

Архитектурные: Domain не ссылается на EF / ASP.NET и на другие контексты; `ValueObjects/` — неизменяемые
records; `DomainServices/` — без изменяемых полей; UI-facing app-сервисы не отдают наружу `Entities/` (порты репозиториев и генератора доменные типы принимают закономерно);
Infrastructure ссылается только из Web и тестов.

Blazor-компоненты не тестируются; демо-путь проверяется вручную по чеклисту.
