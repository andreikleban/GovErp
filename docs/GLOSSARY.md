# Глоссарий — единый язык проекта

Термины предметной области государственного учёта (США) и их имена в коде. Пополняется вместе с кодом (`DDD-16`).

## Учёт и бюджет

| Термин | В коде | Значение |
|---|---|---|
| **Fund** (фонд) | `Fund` | Самостоятельная учётная единица со своим балансом и юридическими ограничениями на использование денег. Не путать с департаментом |
| **Governmental fund** | `FundType.Governmental` | Фонд, финансируемый налогами и грантами; учёт по modified accrual; расход — `Expenditure` |
| **Enterprise fund** | `FundType.Enterprise` | Фонд, работающий как бизнес (водоканал); учёт по full accrual; расход — `Expense` |
| **Modified accrual / Full accrual** | `AccountingBasis` | Метод признания: «использование финансовых ресурсов» vs «потребление экономической выгоды» |
| **Appropriation** | `BudgetLine.Adopted` | Бюджетное ассигнование, утверждённое советом; законная граница расходов |
| **Amended budget** | `BudgetLine.Amended` | `Adopted + ΣAmendments` — текущая законная граница |
| **Budget amendment** | `BudgetLine.Amend` | Изменение ассигнования, принятое уполномоченным органом |
| **Actual expenditures** | `BudgetLine.Actuals` | Проведённые расходы |
| **Encumbrance** | `Encumbrance` | Резерв бюджета под принятое обязательство (PO), по которому счёт ещё не пришёл |
| **Outstanding encumbrances** | `BudgetLine.Encumbered` | Сумма открытых encumbrances по бюджетной строке |
| **Liquidation** | `Encumbrance.Liquidate` | Снятие резерва при приходе инвойса по PO; резерв превращается в actuals |
| **Reservation** (Held) | `BudgetLine.Reservations`, `ReservationStatus.Held` | Внутренний резерв движка под инвойс, взятый на Submit до Post. Не бухгалтерский термин, а механизм защиты от двойного списания |
| **Available budget** | `BudgetLine.Available` | `Amended − Actuals − Encumbered − ΣHeld` |
| **Hard / Soft budget control** | `BudgetControlMode` | Hard — превышение блокирует; Soft — превышение даёт Soft Stop, снимаемый уполномоченным |
| **General Ledger (GL)** | `JournalEntry`, `JournalLine` | Главная книга; проводки по двойной записи |
| **Budgetary vs Financial ledger family** | `LedgerFamily` | Бюджетные счета (Appropriations, Encumbrances, Reserve for Encumbrances) и реальные (расходы, обязательства, активы) |
| **Balanced posting** | инвариант `JournalEntry` | `ΣDebit = ΣCredit` внутри каждого фонда |
| **Fiscal year / period** | `FiscalYear`, `FiscalPeriod` | Финансовый год с 1 июля; период — месяц, Open / Closed |
| **Сторно** | обратная проводка | Отмена записи записью с обратными знаками; ничего не удаляется |

## План счетов

| Термин | В коде | Значение |
|---|---|---|
| **Chart of Accounts (CoA)** | контекст `ChartOfAccounts` | Справочник счетов; в гос. ERP — составной ключ из сегментов |
| **Segment** | `FundCode`, `DepartmentCode`, `ObjectCode`, `GrantCode` | Часть составного адреса счёта |
| **Object (code)** | `ObjectCode` | Вид расхода / дохода / актива (53100 Professional Services) |
| **Account code / combination** | `AccountCode` | Полный адрес: `Fund-Department-Object-Grant` |
| **Valid combination** | `AccountCombination` | Заведённая и утверждённая комбинация сегментов с effective dates и статусом |
| **Combination rule** | `CombinationRule` | Правило допустимости сочетаний: «если Fund = 701, Grant обязателен» |
| **Grant** | `Grant` | Целевое финансирование с периодом, спонсором, allowable costs и разрешёнными департаментами |
| **Allowable cost** | `Grant.AllowableObjects` | Категория расходов, которую грант разрешает оплачивать |
| **Effective dating** | `EffectiveFrom / EffectiveTo` | Период, в течение которого запись (правило, комбинация, версия) действует |

## Транзакции

| Термин | В коде | Значение |
|---|---|---|
| **AP invoice** | `VendorInvoice` | Счёт поставщика к оплате |
| **Distribution** | `InvoiceDistribution` | Строка инвойса: сумма на конкретный `AccountCode` |
| **Purchase Order (PO)** | `PurchaseOrder` | Заказ / контракт, создающий encumbrance |
| **PO-backed / Non-PO invoice** | `VendorInvoice.PoRef` | Инвойс, ссылающийся на PO (ликвидирует encumbrance) / без PO (весь проверяется против available) |
| **Amount to check** | `EncumbranceSnapshot.AmountToCheck` | Часть суммы distribution, проверяемая против available: `amount − min(amount, PO remaining)` |
| **Debarred vendor** | `VendorStatus.Debarred` | Поставщик, которому запрещены государственные контракты |
| **SAM registration** | `Vendor.SamRegistered` | Регистрация в федеральной системе SAM.gov; обязательна для федеральных грантов |
| **Save / Submit / Approve / Post / Pay** | `InvoiceStatus`, `Capabilities` | Стадии жизненного цикла: черновик / на согласовании / согласован / проведён в GL / оплачен |
| **Override** | `VendorInvoice.Override` | Снятие Soft Stop уполномоченной ролью с указанием причины |
| **Separation of duties** | инвариант `VendorInvoice.Approve` | Автор не согласует; согласующий не оплачивает |

## Валидация

| Термин | В коде | Значение |
|---|---|---|
| **Validation pipeline** | `ValidationPipeline` | Фиксированная последовательность из 8 шагов |
| **Rule definition** | `RuleDefinition` | Запись о правиле: версия, слой, severity, параметры, effective dates |
| **Rule layer** | `RuleLayer` | `Core` (семантика учёта) / `Federal` (2 CFR 200) / `State` / `Tenant` |
| **Severity / Result state** | `Severity` | `Allowed` < `Warning` < `SoftStop` < `HardStop` |
| **Hard Stop** | `Severity.HardStop` | Блокировка без возможности override; требуется исправить данные или бюджет |
| **Soft Stop** | `Severity.SoftStop` | Блокировка, снимаемая override уполномоченной роли |
| **Rule outcome** | `RuleOutcome` | Результат одного правила: входы, расчёт, severity, сообщение, resolution |
| **Evaluation record** | `EvaluationRecord` | Неизменяемая запись одной оценки: снимок входов, версии правил, outcomes, overall, capabilities, маршрут, preview |
| **Validation subject** | `ValidationSubject` | Снимки всего, что нужно конвейеру; собирается в слое сценариев |
| **Capabilities** | `Capabilities` | Что можно сделать с транзакцией при данном результате: save / submit / approve / post / pay |
| **Approval route** | `ApprovalRouting` | Список согласующих ролей и что каждой показывается |
| **Posting eligibility** | `PostingEligibility` | Шаг 8: все согласования, период открыт, баланс, версии правил не изменились |
| **Posting preview** | `EvaluationRecord.PostingPreview` | Проводки, которые будут созданы при Post — до Post |
| **Explanation** | `IExplanationGenerator` | Текст для аудитории (finance user / manager / auditor / public), сгенерированный из `EvaluationRecord` |

## Инфраструктура

| Термин | В коде | Значение |
|---|---|---|
| **Tenant** | `TenantId`, `ITenantContext` | Одна государственная единица (город); своя БД |
| **Audit event** | `AuditEvent`, `IAuditTrail` | Append-only запись «кто, что, когда, по какой версии» |
| **Actor** | `ActorContext` | Пользователь или сервис, выполняющий сценарий; передаётся явно |
