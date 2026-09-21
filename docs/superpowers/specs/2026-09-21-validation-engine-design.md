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
2. Тот же после budget amendment +$13,000 → **Soft Stop** (procurement threshold) → override → Submit → Approve → Post
3. PO-backed инвойс $160,000 → **Allowed**, encumbrance ликвидирована, available не изменился
4. Мультифондовый инвойс $30,000 на три фонда (101 / 202 / 501) → баланс по фондам, Soft Stop на 101, Expense vs Expenditure
5. Concurrency-тест: два параллельных Submit на одну бюджетную строку — проходит ровно один

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
- PO-backed: ликвидация в пределах остатка PO-строки; излишек ≤ 5% — Warning, излишек проверяется
  против available; > 5% — Hard Stop.
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
- Payment — только флаг eligibility; проводок оплаты нет.
- Fiscal periods: 2026-09 открыт; 2026-08 закрыт.

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
| `Money` | `decimal(18,2)`; арифметика возвращает новые значения; для сумм ≥ 0, для дельт — любой знак |
| `FundCode`, `DepartmentCode`, `ObjectCode`, `GrantCode` | непустая строка фиксированного формата |
| `AccountCode` | `Fund`, `Department`, `Object`, `Grant?`; `ToString()` → `701-6000-53100-G-COPS-26` |
| `FiscalYear` | `Start`, `End`, `Contains(date)`; из даты по правилу «1 июля» |
| `TenantId`, `UserId` | обёртки с проверкой на пустоту |

### 3.2 ChartOfAccounts

| Агрегат | Состояние | Методы | Инварианты |
|---|---|---|---|
| `Fund` | `Code`, `Name`, `FundType`, `AccountingBasis`, `BudgetControlMode`, `GrantPolicy`, `AllowedDepartments`, `AllowedObjects`, `IsActive` | анемичный | — |
| `Department`, `ObjectCode` | код, имя, `IsActive`; `ObjectCode.Category` (Expenditure / Asset / Liability / Budgetary) | анемичные | — |
| `Grant` | `Code`, `Sponsor`, `IsFederal`, `Period`, `AllowedDepartments`, `AllowableObjects`, `Status` | `IsEligible(date, dept, object)` | — |
| `AccountCombination` | `Code: AccountCode`, `Status` (Pending / Active / Inactive), `EffectiveFrom/To`, `ApprovedBy` | `Approve(user)`, `Deactivate(date)` | нельзя `Approve` активную; `EffectiveTo ≥ EffectiveFrom` |
| `CombinationRule` | `Version`, `Effective*`, `Condition`, `Constraint` | анемичный | — |

Репозитории: `IFundRepository`, `IGrantRepository`, `IAccountCombinationRepository`,
`ICombinationRuleRepository`, `IReferenceDataRepository`.

### 3.3 Ledger

| Агрегат | Состояние | Методы | Инварианты |
|---|---|---|---|
| `BudgetLine` | `Account`, `FiscalYear`, `ControlMode`, `Adopted`, `Amendments[]`, `Actuals`, `Encumbered`, `Reservations[]` (`Id`, `SourceRef`, `Amount`, `Status: Held / Committed / Released`), `RowVersion` | `Amend`, `Reserve → ReservationResult`, `Commit(reservationId)`, `Release(reservationId)`, `Snapshot()` | `Amended = Adopted + ΣAmendments`; `Available = Amended − Actuals − Encumbered − ΣHeld`; при Hard `Reserve` отказывает, если `Available < amount`; при Soft — резервирует с флагом `Overage`; `Commit`: Held → Committed, `Actuals += amount`; повторный `Commit` — ошибка |
| `Encumbrance` | `PoLineRef`, `Account`, `Original`, `Liquidated`, `Released`, `Status`, `RowVersion` | `Liquidate(amount, sourceRef)`, `ReleaseRemainder(reason)` | `Remaining = Original − Liquidated − Released ≥ 0`; `Liquidate > Remaining` — ошибка |
| `JournalEntry` | `SourceRef`, `PostedAt`, `PostedBy`, `Lines[]` (`Account`, `LedgerFamily: Financial / Budgetary`, `Debit`, `Credit`, `Description`) | фабрика `Create(lines, period, actor)`; неизменяем | по каждому фонду `ΣDebit = ΣCredit`; ≥ 2 строк; период открыт |
| `FiscalPeriod` | `Year`, `Month`, `Status` | `Close()` | — |

`BudgetLine.Encumbered` — денормализованная сумма, обновляется тем же сценарием, что ликвидирует
`Encumbrance`.

Репозитории: `IBudgetLineRepository`, `IEncumbranceRepository`, `IJournalRepository`, `IFiscalPeriodRepository`.

### 3.4 Payables

| Агрегат | Состояние | Методы | Инварианты |
|---|---|---|---|
| `Vendor` | `Code`, `Name`, `Status` (Active / Debarred), `SamRegistered` | анемичный | — |
| `PurchaseOrder` | `Number`, `VendorId`, `Status`, `Lines[]` (`LineNo`, `Account`, `Amount`) | анемичный (seed) | — |
| `VendorInvoice` | `Number`, `VendorId`, `InvoiceDate`, `Total`, `PoRef?`, `Distributions[]` (`LineNo`, `Account`, `Amount`, `PoLineRef?`), `Status`, `Approvals[]` (`Role`, `UserId`, `Decision`, `At`, `EvaluationRef`), `Overrides[]` (`RuleId`, `UserId`, `Reason`, `At`), `LastEvaluationRef`, `ReservationRefs[]`, `CreatedBy`, `RowVersion` | `AddDistribution`, `RemoveDistribution`, `Submit(evalRef, reservations)`, `Approve(role, user, evalRef)`, `Override(ruleId, user, reason)`, `Reject`, `Post(evalRef)`, `MarkPayable()` | `ΣDistributions = Total` при `Submit`; редактируем только `Draft`; `Approve` — только ожидаемой ролью и не автором; `Post` — только из `Approved` |

Статусы: `Draft → Submitted → Approved → Posted → Payable`; `Rejected` из `Submitted` / `Approved`; `Rejected → Draft`.

Репозитории: `IVendorInvoiceRepository`, `IPurchaseOrderRepository`, `IVendorRepository`.

### 3.5 Validation

| Тип | Что это | Состояние |
|---|---|---|
| `RuleDefinition` (агрегат, анемичный) | Запись о правиле: включённость, версия, параметры | `RuleId`, `Version`, `Step (1..8)`, `Layer (Core / Federal / State / Tenant)`, `Scope`, `Severity`, `Parameters` (json), `OverridableBy[]`, `Effective*`, `Message`, `Resolution` |
| `ValidationSubject` (value object) | Все входы конвейера — снимки | `TransactionSnapshot` (тип, дата, сумма, vendor status, PO?, distributions); по distribution — `CombinationSnapshot`, `FundSnapshot`, `GrantSnapshot?`, `BudgetSnapshot`, `EncumbranceSnapshot?` (с `AmountToCheck`); `ApprovalsSoFar`, `OverridesSoFar`, `PeriodStatus`, `ExistingInvoiceNumbers` |
| `ValidationPipeline` (доменный сервис) | `Evaluate(subject, rules, trigger) → EvaluationRecord` | без состояния; шаги — `IValidationStep` в `DomainServices/Steps/`; правила — классы в `DomainServices/Rules/` |
| `ApprovalRouting`, `PostingEligibility` (доменные сервисы) | шаги 7 и 8 | без состояния |
| `EvaluationRecord` (агрегат, неизменяемый) | Результат оценки | `Id`, `TransactionRef`, `TransactionVersion`, `Trigger`, `EvaluatedAt`, `EvaluatedBy`, `RuleSetVersions` (по слоям), `Outcomes[]`, `Overall`, `Capabilities`, `ApprovalRoute[]`, `PostingPreview[]`, `InputSnapshot` |

Инварианты `EvaluationRecord`: `Overall = max(Outcomes.Severity)` с учётом overrides; `Capabilities` —
функция от `Overall`; создаётся один раз, не изменяется.

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
Evaluate(subject, rules, trigger):
  for step in 1..6:
      outcomes += step.Run(subject, rules.ForStep(step))
      if outcomes.Any(HardStop): break
  route    = ApprovalRouting.Build(subject, outcomes)              // шаг 7, всегда
  overall  = Aggregate(outcomes, subject.OverridesSoFar)
  preview  = overall ≤ SoftStop ? PostingPreview.Build(subject) : []
  eligible = PostingEligibility.Check(...)                          // шаг 8, при trigger = Post
  return EvaluationRecord(...)
```

Разрешение конфликтов:
1. Внутри шага — все правила шага выполняются, результат = строжайший.
2. Между шагами — Hard Stop на шагах 1–6 прерывает; Soft / Warning накапливаются.
3. Одинаковый `RuleId` в разных слоях — побеждает более специфичный: `Tenant > State > Federal > Core`.
   Ослаблять severity правил `Federal` / `Core` нельзя.

Override меняет агрегацию, не outcome: outcome остаётся `SoftStop`, `Overall` считается без него,
override фиксируется в записи.

Шаг 6 (encumbrance impact) вычислительно предшествует шагу 5: `AmountToCheck = amount − min(amount, remaining)`
считается в `ValidationSubjectAssembler` и лежит в `EncumbranceSnapshot`; правило шага 6 отчитывается
о ликвидации и излишке, правило шага 5 использует `AmountToCheck`.

### 4.2 Правила демо

| # | RuleId | Шаг | Layer | Severity | Проверяет | Параметры |
|---|---|---|---|---|---|---|
| 1 | `SEG_REQUIRED` | 1 | Core | Hard | Fund, Dept, Object заполнены; Grant — по `GrantPolicy` | — |
| 2 | `SEG_GRANT_FORBIDDEN` | 1 | Core | Hard | Grant при `GrantPolicy = Forbidden` | — |
| 3 | `COA_COMBINATION_ACTIVE` | 2 | Core | Hard | комбинация в whitelist, Active, в effective-окне | — |
| 4 | `FUND_DEPT_OBJECT_ALLOWED` | 3 | Tenant | Hard | dept ∈ `Fund.AllowedDepartments`, object ∈ `Fund.AllowedObjects` | — |
| 5 | `GRANT_ELIGIBLE` | 3 | Federal | Hard | грант активен, дата в периоде, dept и object разрешены | — |
| 6 | `VENDOR_ELIGIBLE` | 4 | Federal | Hard | vendor не Debarred; для федерального гранта — `SamRegistered` | — |
| 7 | `PROCUREMENT_THRESHOLD` | 4 | State | Soft | non-PO и total ≥ threshold | `threshold: 25000`; override `FinanceDirector` |
| 8 | `INVOICE_DUPLICATE` | 4 | Core | Hard | vendor + номер уже есть (не Rejected) | — |
| 9 | `BUDGET_AVAILABILITY` | 5 | Core | по фонду | `available < AmountToCheck` → Hard при `ControlMode = Hard`, Soft при Soft | override (Soft): `BudgetOfficer`, `FinanceDirector` |
| 10 | `BUDGET_LOW_REMAINING` | 5 | Tenant | Warning | `(available − amount) / amended < pct` | `pct: 0.10` |
| 11 | `PO_LIQUIDATION` | 6 | Core | Warning / Hard | излишек ≤ tolerance → Warning; > tolerance → Hard | `tolerance_pct: 0.05` |

Синтетический outcome `BUDGET_CONCURRENCY` (Hard) — при конфликте резервирования на Submit.
Синтетический outcome `REVALIDATION_REQUIRED` (Hard) — при смене версий правил после Approve.

### 4.3 Result model

```
Severity: Allowed < Warning < SoftStop < HardStop

RuleOutcome
  RuleId, RuleVersion, Step, Layer, DistributionLine?
  Severity
  Inputs   (json)     { amended, actuals, encumbered, held, amount }
  Computed (json)     { available, overage }
  Message, Resolution
  FinancialImpact     { available_after }
  OverridableBy[], OverriddenBy? { user, reason, at }
```

| Overall | Save | Submit | Approve | Post | Pay |
|---|---|---|---|---|---|
| Allowed | ✓ | ✓ | ✓ | ✓ при eligibility | ✓ при Posted ∧ vendor payable |
| Warning | ✓ | ✓ | ✓ | ✓ при eligibility | ✓ при Posted ∧ vendor payable |
| SoftStop (не снят) | ✓ | ✓ | ✗ | ✗ | ✗ |
| SoftStop (снят) | ✓ | ✓ | ✓ | ✓ при eligibility | ✓ при Posted ∧ vendor payable |
| HardStop | ✓ | ✗ | ✗ | ✗ | ✗ |

`PostingEligibility`: `Overall ≤ Warning` (с overrides) ∧ все роли маршрута дали Approved ∧ период
открыт ∧ preview сбалансирован по фондам ∧ `RuleSetVersions` совпадают с оценкой при последнем Approve.

`ApprovalRouting`: по правилам 2.4; для каждого согласующего — `WhatTheySee` (distributions, бюджетный
результат, сработавшие правила).

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
| `IInvoiceAppService` | `CreateDraft`, `CreateFromPreset`, `UpdateDraft`, `Validate`, `Submit`, `GetInvoice`, `ListInvoices` |
| `IApprovalAppService` | `GetQueue`, `Approve`, `Reject`, `Override` |
| `IPostingAppService` | `Post`, `GetPostingPreview` |
| `IExplanationAppService` | `Explain(evaluationId, audience)`, `GetAuditTrail(invoiceId)` |
| `IBudgetAppService` | `Amend(account, amount, reference)`, `ListBudgetLines`, `GetBudgetLine` |
| `IReferenceAppService` | `GetSegments`, `GetCombinations`, `GetRules` |

`ValidationSubjectAssembler` — вспомогательный компонент Application, собирает снимки из четырёх контекстов.

### 5.2 Транзакции по шагам

| Шаг | Транзакции | Агрегаты | Оценка |
|---|---|---|---|
| CreateDraft / UpdateDraft | 1 | `VendorInvoice` | нет |
| Validate | 1 | `EvaluationRecord` + audit | `Manual` |
| Submit | N + 1, компенсация | ① `BudgetLine.Reserve` по одной транзакции на строку; ② `VendorInvoice.Submit` + `EvaluationRecord` | `Submit`; при Hard Stop ① не выполняется |
| Approve | 1 | `VendorInvoice.Approve` + `EvaluationRecord` | `Approve`; если оценка хуже предыдущей — отказ, инвойс остаётся Submitted |
| Override | 1 | `VendorInvoice.Override` + `EvaluationRecord` | перевалидация с override |
| Reject | 1 + компенсация | `VendorInvoice.Reject`; затем `BudgetLine.Release` ×N | — |
| Post | **1 (исключение)** | `VendorInvoice.Post` + `BudgetLine.Commit` ×N + `Encumbrance.Liquidate` ×M + `JournalEntry` + audit | `Post`; `PostingEligibility` обязан пройти |

### 5.3 Submit — резервирование и concurrency

```
Submit(invoiceId, actor):
  invoice = load; subject = assembler.Build(invoice)
  eval = pipeline.Evaluate(subject, rules, Submit)
  if eval.Overall == HardStop: save eval; audit; return eval

  reservations = []
  foreach dist in invoice.Distributions:
      try:
          line = budgetLines.Load(dist.Account, fy)            // читает RowVersion
          r = line.Reserve(dist.AmountToCheck, invoice.Ref)
          budgetLines.Save(line)                                // UPDATE ... WHERE RowVersion = @v
      catch ConcurrencyConflict:
          retry once; if again → release all; return eval + BUDGET_CONCURRENCY (Hard)
      if r.Refused: release all; return eval + BUDGET_AVAILABILITY recomputed (Hard)
      reservations += r

  invoice.Submit(eval.Id, reservations); save invoice + eval; audit
  (на любом сбое после резервирования — Release всех резервов в finally)
```

`Reserve` — метод одного агрегата: проверка `Available ≥ amount` и запись `Held` внутри одной строки
под rowversion. Два параллельных Submit → второй получает конфликт, перечитывает, видит `Held`, отказывает.

Висящие резервы при сбое компенсации — на слайде: фоновый reconciler по `Reservation.SourceRef`.

### 5.4 Post — одна транзакция

```
Post(invoiceId, actor):
  invoice = load (Approved)
  eval = pipeline.Evaluate(assembler.Build(invoice), rules, Post)
  if !PostingEligibility.Passes(eval, invoice, period): save eval; return

  using tx = unitOfWork.Begin():
      invoice.Post(eval.Id)
      foreach res in invoice.ReservationRefs: budgetLine.Commit(res)
      foreach dist with PoLineRef: encumbrance.Liquidate(...); budgetLine.Encumbered -= ...
      journal = JournalEntry.Create(eval.PostingPreview, period, actor)
      audit.Record(InvoicePosted, ...)
      tx.Commit()
```

Исключение из `DDD-8.3` — записано в манифесте. Резерв, взятый на Submit, делает Post безопасным для
eventual consistency в production (outbox) — транзакция здесь удобство, не необходимость.

### 5.5 Идемпотентность и события

`Submit` / `Approve` / `Post` — проверка «инвойс уже в целевом статусе → вернуть текущее состояние».
`IdempotencyKey` и таблица `ap.ProcessedCommands` — на слайде.

Доменные события в демо не используются.
На слайде production: `InvoiceSubmitted`, `InvoicePosted`, `BudgetAmended`, `EvaluationRecorded` через outbox.

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
| `coa` | Funds, Departments, ObjectCodes, Grants, AccountCombinations, CombinationRules |
| `ledger` | BudgetLines, BudgetAmendments, BudgetReservations, Encumbrances, JournalEntries, JournalLines, FiscalPeriods |
| `ap` | Vendors, PurchaseOrders, PurchaseOrderLines, VendorInvoices, InvoiceDistributions, InvoiceApprovals, InvoiceOverrides |
| `validation` | RuleDefinitions, EvaluationRecords (json-колонки: Outcomes, InputSnapshot, PostingPreview, ApprovalRoute), Explanations |
| `audit` | Events |

FK — только внутри схемы. `rowversion` на BudgetLines, Encumbrances, VendorInvoices. `EvaluationRecords`
и `audit.Events` — только INSERT.
Value objects — `OwnsOne` / `HasConversion`; `AccountCode` — четыре колонки + вычисляемая `FullCode`.

**Tenancy:** БД `GovErp_Master` (тенанты, пользователи, роли, connection strings); БД `GovErp_Springfield`,
`GovErp_Shelbyville`. `ITenantContext { TenantId, ConnectionString }` — scoped, из claims после логина;
`GovErpDbContext` конфигурируется из него. Миграции при старте — Master, затем цикл по тенантам; seed.

**Auth:** cookie auth, `IUserRepository` в Master, `PasswordHasher<T>`, роли → claims, `AuthorizeView`.
Без регистрации и восстановления.

**Explanation:** см. 4.4. Ключи из env; провайдер из конфигурации.

**Docker:** `docker-compose.yml` — `sqlserver` (mssql/server:2022, healthcheck) + `web` (multi-stage
Dockerfile). Web ждёт healthcheck, при старте выполняет миграции и seed. Порт 8080. `.env`:
`SA_PASSWORD`, опционально `ANTHROPIC_API_KEY`, `Explanation__Provider`.

### 6.3 UI (Blazor Server, Bootstrap из шаблона)

| Страница | Роли | Содержание |
|---|---|---|
| `/login` | все | форма; список seed-пользователей |
| `/invoices` | все | список; кнопки пресетов «Non-PO», «PO-backed», «Multi-fund» → Draft |
| `/invoices/{id}` | ApClerk — правка; остальные — просмотр | header, distributions с живым итогом, действия по статусу; вкладки **Results**, **Posting Preview**, **Approval Route**, **Audit**, **Explain** |
| `/approvals` | согласующие роли | очередь; Approve / Reject / Override с причиной; предупреждение с diff, если оценка изменилась |
| `/budget` | BudgetOfficer, FinanceDirector | строки с amended / actuals / encumbered / held / available; «Amend» |
| `/rules` | все | RuleDefinition по шагам и слоям — только чтение |

### 6.4 Демо-путь (5 минут)

1. ApClerk → пресет Non-PO → Validate → Hard Stop −13,000 → Explain
2. BudgetOfficer → `/budget` → Amend +13,000 → Validate → Soft Stop (procurement) → Submit
3. FinanceDirector → Override + Approve; DepartmentHead (Fire), GrantsManager → Approve → Post → журнал, `/budget`: actuals 292k / available 0
4. Пресет PO-backed → Validate → Allowed, две пары проводок, available не изменился
5. Пресет Multi-fund → три баланса, Expense vs Expenditure, Soft Stop на 101
6. Audit по инвойсу из 1–3: четыре оценки, версии, override, кто и когда

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
`Scenario_NonPo_701_AfterAmendment_WithOverride_IsAllowed`,
`Scenario_PoBacked_WithinRemaining_IsAllowed_AvailableUnchanged`,
`Scenario_PoBacked_Excess3Pct_IsWarning`, `Scenario_PoBacked_Excess8Pct_IsHardStop`,
`Scenario_MultiFund_101_Overage_IsSoftStop_202_501_Allowed`, `Scenario_MultiFund_501_UsesExpenseNotExpenditure`,
по одному `Rule_*` на каждое правило, `Pipeline_HardStopAtStep2_SkipsSteps3To6_StillBuildsRoute`,
`Pipeline_SameRuleId_TenantLayer_OverridesStateLayer`, `Pipeline_Override_ChangesOverall_NotOutcome`,
`Capabilities_*`, `PostingEligibility_RuleSetVersionChanged_RequiresRevalidation`,
`PostingPreview_BalancedPerFund`, `PostingPreview_PoBacked_HasBudgetaryReversalLines`.

Ключевые Application-тесты: `Submit_TwoParallelInvoices_OneBudgetLine_ExactlyOneSucceeds` (10 повторов),
`Submit_HardStop_CreatesNoReservation`, `Submit_Then_Reject_ReleasesReservations`,
`Post_Commits_Liquidates_WritesJournal_InOneTransaction`, `Post_JournalUnbalanced_RollsBackEverything`,
`Post_WhenRuleVersionChangedAfterApprove_IsRefused`, `Approve_ByAuthor_IsRefused`,
`Approve_WhenEvaluationWorsened_IsRefused`, `Tenant_Shelbyville_CannotSeeSpringfieldInvoices`,
`EvaluationRecord_Update_IsRejected`, `Explanation_LlmFails_FallsBackToTemplate_AndRecordsReason`,
`Explanation_ProviderTemplate_NeverCallsChatClient`.

Архитектурные: Domain не ссылается на EF / ASP.NET и на другие контексты; `ValueObjects/` — неизменяемые
records; `DomainServices/` — без изменяемых полей; Application не отдаёт наружу `Entities/`;
Infrastructure ссылается только из Web и тестов.

Blazor-компоненты не тестируются; демо-путь проверяется вручную по чеклисту.
