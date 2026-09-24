# UI GovErp в стиле ERP — дизайн

Дата: 2026-09-23. Статус: согласован в чате, реализуется сразу.

## Цель

Демонстрация трека Solution Architecture: комиссия за 15 минут видит настройку, бюджет, транзакцию, решение движка, учёт и аудит, а также связи между ними. Сейчас всё собрано в карточке инвойса стопкой панелей, и часть данных (план счетов, encumbrance, журнал, оценки, пользователи) не видна совсем.

## Навигация

Меню слева по модулям в порядке потока данных. Каждый пункт — список с фильтрами и карточка по общему шаблону.

| Модуль | Экраны |
|---|---|
| Setup | Funds, Departments, Objects, Grants, Account Combinations, Vendors, Rules, Users, Roles |
| Budget | Budget Lines (карточка строки), Amendments, Encumbrances |
| Purchasing | Purchase Orders |
| Payables | Invoices, Approvals |
| General Ledger | Journal, Periods |
| Audit | Evaluations, Events |

Главная после входа — «Ожидают меня» (очередь Approvals) и ссылки на модули роли.

Всё, кроме документа инвойса и поправки бюджета, только для чтения (данные задаёт seed).

## Общий шаблон

- Хлебные крошки: `Payables › Invoices › AP-2026-000001`.
- Список: фильтры над таблицей, суммы вправо в формате `160,000.00`, статусы бейджами, первая колонка — ссылка в карточку.
- Карточка: панель действий сверху → шапка (поля сеткой) → статусная строка → таблица строк → вкладки.
- Переходы по ссылкам:

| Откуда | Ссылка | Куда |
|---|---|---|
| строка инвойса | счёт | карточка строки бюджета |
| строка инвойса | PO line | заказ и его encumbrance |
| строка бюджета | — | резервы (с номерами инвойсов), encumbrance по счёту, поправки |
| hold | код правила | Setup › Rules |
| encumbrance / PO | claim | инвойс, который держит или погасил claim |
| проводка журнала | SourceRef | инвойс |
| оценка (Audit) | документ | карточка инвойса, открытая на этой оценке (`?evaluation=`) |
| фонд, грант | код | их карточка: комбинации и строки бюджета |

## Карточка инвойса

**Панель действий** — всегда один набор кнопок; недоступные серые, причина в подсказке (`title`):

| Кнопка | Кому и когда |
|---|---|
| Save, Submit | автор-клерк, Draft |
| Check Funds (Validate) | Draft, Submitted, Approved |
| Approve, Reject | согласующий, если его шаг ждёт в маршруте |
| Release Hold (override) | роль, которой правило разрешает снять Soft Stop |
| Post | Budget Officer / Finance Director, Approved, нет открытых стопов |
| Withdraw | автор, Submitted / Approved |
| Return to Draft | автор, Rejected |
| Hold / Release Payment | Budget Officer / Finance Director, Posted |
| Audit | всем, окно истории |

Причины (Reject, Withdraw, Release Hold) вводятся в маленьком диалоге по нажатию кнопки.

**Статусная строка:**

| Поле | Значения |
|---|---|
| Document | Draft · Submitted · Approved · Rejected · Posted |
| Validation | Not checked · Passed · N warnings · N holds (soft/hard) |
| Approval | — · k of n · Approved · Rejected |
| Funds | Not reserved · Reserved X · Liquidating PO X · Consumed |
| Accounting | Not posted · Posted {дата} |
| Payment | Not ready: {причина} · Ready · On hold |

**Строки:** Fund, Dept, Object, Grant, Amount, PO line (колонка только при выбранном PO, выбор из строк заказа), Available after (из расчёта движка), Status (худший итог по строке). Счёт — ссылка на бюджет.

**Вкладки:** Holds (открытые и снятые блокировки: правило, причина, цифры, кто может снять) · Validation Log (8 шагов, входы и расчёт) · Approvals (маршрут и решения по всем циклам) · Accounting (preview до Post, журнал после). Объяснение оценки — кнопка «Explain…» в Validation Log, открывает отдельное окно. Открывается первая содержательная: Holds, если есть; иначе Approvals для Submitted; иначе Accounting для Approved/Posted; иначе Validation Log.

**Режим правки:** шапка и строки редактируются прямо в форме — только автором в Draft; New открывает ту же форму пустой.

## Setup › Users и Roles

- Users: пользователи текущего тенанта из Master — логин, имя, роль, отдел.
- Roles: матрица «роль → действие» (создание и отправка, согласование, снятие Soft Stop, поправка бюджета, проводка, payment hold) из тех же констант ролей, что проверяет сервер (`Roles.*`), плюс «кто может снять» из текущих правил (`RuleDefinition.OverridableBy`). Под матрицей — правило разделения обязанностей.

## Данные

Модель данных и движок не меняются; миграций нет. Новые методы только читают и возвращают view-модели (CA-10).

| Сервис | Методы |
|---|---|
| `IReferenceAppService` | `GetFundAsync`, `GetGrantAsync`, `GetUsersAsync`, `GetRoleMatrixAsync` |
| `IBudgetAppService` | `GetLineAsync(account, fy)` (поправки, резервы с номерами инвойсов, encumbrance по счёту), `ListAmendmentsAsync`, `ListEncumbrancesAsync` |
| `IPurchasingAppService` (новый) | `ListOrdersAsync`, `GetOrderAsync` (строки, encumbrance, claims с номерами инвойсов) |
| `ILedgerAppService` (новый) | `ListJournalAsync(fund, period, source)`, `ListPeriodsAsync` |
| `IAuditAppService` (новый) | `ListEvaluationsAsync(filter)`, `ListEventsAsync(filter)` — последние 200 |
| `IInvoiceAppService` | фильтры `ListAsync` (статус, фонд, есть блокировки); в `InvoiceVm` — суммы резервов, claims ликвидации и billing claims |

Новые методы чтения репозиториев: списки encumbrance, журнала, периодов, оценок по всем документам, событий аудита по фильтру; порт «пользователи тенанта» (Master).

## Структура UI

`Components/Pages/{Setup,Budget,Purchasing,Payables,Ledger,Audit}/*.razor`; общие компоненты в `Components/Shared`: Breadcrumbs, FilterBar, StatusBadge, AccountLink, DocumentLink, Money. Текущие Budget, Rules, Approvals переезжают в модули; компоненты карточки (EvaluationPanel, PostingPanel, ExplanationDialog, AuditDialog) переиспользуются.

## Тесты

- Интеграционные (SQL Server, существующая фикстура) на каждый новый сервис: агрегации корректны (резервы строки бюджета с номерами инвойсов, claims заказа ведут на инвойс, журнал по фонду сбалансирован).
- Изоляция: Audit и Users под Shelbyville не видят Springfield.
- Матрица ролей совпадает с реальными проверками сервисов (Forbidden или нет).
- Пустые данные — пустые списки; неизвестный код — NotFound.
- UI проверяется вживую по сценарию демо; компонентных тестов нет.

## Порядок работ

1. Каркас: меню, крошки, общие компоненты, главная.
2. Карточка инвойса и список с фильтрами.
3. Budget и Purchasing.
4. General Ledger.
5. Audit.
6. Setup (включая Users и Roles).

## Вне рамок

Редактирование справочников, отчёты, дашборд, модуль оплаты, постраничный вывод.
