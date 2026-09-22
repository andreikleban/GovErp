# GovErp — манифест проекта

Проект следует общим правилам разработки из скилла `ca-ddd-rules` — чистая архитектура
и прагматичный DDD. Здесь описано только то, что специфично для этого продукта.

Правила оттуда цитируются по номеру (`CA-7`, `DDD-12`, `NM-3`). Если реализация расходится
с общим правилом — расхождение оформляется исключением в разделе 6, а не молчанием.

---

## 1. Продукт

Движок валидации транзакций государственного ERP: определяет, разрешён ли AP-инвойс по плану счетов,
ограничениям фондов и грантов, бюджету и encumbrances; ведёт инвойс по жизненному циклу
Save → Submit → Approve → Post с перевалидацией; пишет проводки и неизменяемый аудит.
Прототип для интервью, мультитенантный (тенант — одна государственная единица).

---

## 2. Решения проекта

| ID | Решение | Пояснение |
|---|---|---|
| **GE-1** | Источник истины для бюджетных остатков — агрегат `BudgetLine` (`Amended`, `Actuals`, `Encumbered`, `Held`), а не запрос по журналу | Проверка доступности и резервирование — одна строка под `rowversion`; это даёт конкурентность без блокировок. Пересмотреть, если появится требование сверки остатков с журналом |
| **GE-2** | Операционное состояние — SQL Server, EF Core, один `DbContext`, схема на контекст (`coa`, `ledger`, `ap`, `validation`, `audit`) | FK только внутри схемы; между схемами — коды и идентификаторы (`DDD-8.4`) |
| **GE-3** | Ограниченных контекстов четыре — раздел 3 | Конкретизирует `DDD-6`. Audit — не контекст, а порт слоя сценариев |
| **GE-4** | Канал сценариев один: `GovErp.Application.Web` | REST API отсутствует. Пересмотреть при появлении второго потребителя сценариев |
| **GE-5** | Контекст из окружения (пользователь, тенант) допустим только в `GovErp.Web`; сценарии принимают `ActorContext` явно | Конкретизирует `NM-16` |
| **GE-6** | Мультитенантность — база данных на тенанта; реестр тенантов и пользователи — в `GovErp_Master`; `ITenantContext` резолвится при логине | Физическая изоляция, которую можно показать аудитору. Пересмотреть при переходе на dedicated-развёртывание (тот же код, другая инфраструктура) |
| **GE-7** | Доменные события не используются | Единственная межконтекстная реакция (Post → Ledger) — прямой вызов внутри исключения GE-E1 (`DDD-14.4`, `DDD-15`). Пересмотреть при выносе Ledger в отдельный сервис — тогда outbox |
| **GE-8** | Маркерные интерфейсы `IEntity` / `IAggregateRoot` не вводятся | Нет механизма, который бы на них опирался (`DDD-11`). Пересмотреть при введении обобщённых ограничений на репозитории |
| **GE-9** | Конвейер валидации — чистый доменный сервис: вход `ValidationSubject` (снимки), выход `EvaluationRecord`; репозитории других контекстов читает `ValidationSubjectAssembler` в слое сценариев | Конкретизирует `DDD-13.1`, `DDD-14.3`. Делает решение детерминированным и тестируемым без базы |
| **GE-10** | Правила — типизированный код с параметрами из `RuleDefinition`; DSL и интерпретатор правил не вводятся | Объяснимость и тестируемость важнее гибкости. Пересмотреть, если тенанты потребуют собственные правила без релиза |
| **GE-11** | Порядок шагов конвейера фиксирован (1..8); конфигурируются включённость, severity, параметры и scope (фонд, грант) правил по слоям `Core / Federal / State / Tenant` | Побеждает самый специфичный слой, но только если не ослабляет вышестоящие (severity, роли override, параметры по их смыслу); ослабление — ошибка конфигурации, проверяется при seed и при разрешении набора. Допущение демо, не правовая иерархия |
| **GE-12** | `EvaluationRecord`, `Explanations`, `JournalEntries`, `audit.Events` — только добавление | Interceptor защищает EF-путь; права runtime-пользователя БД запрещают UPDATE/DELETE этих таблиц. Interceptor не объявляется полной защитой |
| **GE-13** | Объяснения генерируются только из сохранённого `EvaluationRecord` через порт `IExplanationGenerator`; LLM-провайдер — через `Microsoft.Extensions.AI.IChatClient`, выбирается конфигурацией | Текст объяснения архитектурно не может повлиять на решение. Свой интерфейс над `IChatClient` не вводится (`DDD-15`) |
| **GE-14** | Submit атомарно создаёт бюджетные резервы, encumbrance claims и PO billing claims, принадлежащие `InvoiceId + ContentVersion`, и записывает их id на инвойс; Post погашает именно их; Reject/Withdraw возвращают `InvoiceRelease` и освобождают их в той же транзакции | Собственный резерв не вычитается повторно при Approve/Post (`AvailableForInvoice = Available + OwnHeld`). Конфликт `rowversion` — rollback и один повтор в новом scope, затем retryable Conflict |
| **GE-15** | Версия набора правил — fingerprint (SHA-256 бинарной сериализации всех применённых определений, scope и версии engine); `RuleSetVersions` с максимумами по слоям остаётся только для отображения | Инвойс хранит fingerprint согласования; `Reevaluate` открывает новый цикл при смене fingerprint или маршрута; `Post` требует совпадения оценки, цикла, версии содержания и fingerprint |
| **GE-16** | Mutating-команды идемпотентны по `CommandId` + `RequestHash`; receipt в `ap.ProcessedCommands` сохраняется в той же транзакции | Тот же payload — сохранённый результат; другой payload под тем же ключом — Conflict. Проверки статуса недостаточно |
| **GE-17** | PO billing claim (полная сумма PO-backed инвойса) живёт в Ledger на `Encumbrance` рядом с encumbrance claim; агрегат сам проверяет накопительный допуск от `AuthorizedPoAmount` | Одна строка с rowversion закрывает гонку и по ликвидации, и по допуску; инвариант нельзя обойти мимо агрегата. Цена — утверждённая сумма PO хранится и в Payables, и в Ledger; допуск передаётся из параметра правила `PO_LIQUIDATION`. Пересмотреть при выпуске и изменении PO в самой системе (change orders) |
| **GE-18** | `DbContext` и tenant scope создаются на каждую операцию, не на Blazor circuit; тенант берётся только из аутентифицированного principal и серверного каталога | БД на тенанта — логическая изоляция на одном сервере; runtime-пользователь БД видит только свою БД, миграции — отдельным пользователем |
| **GE-19** | Маршрут согласования и fingerprint хранятся на `VendorInvoice`; согласование помечено циклом, версией содержания и fingerprint | Разделение обязанностей, согласование только своим шагом (роль + департамент) и повторное согласование — инварианты агрегата (`DDD-3`), а не проверки сценария |

---

## 3. Ограниченные контексты

| Контекст | Предметная область | Хранилище |
|---|---|---|
| `ChartOfAccounts` | Сегменты плана счетов, гранты, допустимые комбинации (whitelist); ограничения сочетаний — атрибуты фондов и грантов | схема `coa` |
| `Ledger` | Бюджетные строки и резервы, encumbrances с claims ликвидации и billing claims PO, начальные остатки, журнал проводок, финансовые периоды | схема `ledger` |
| `Payables` | Поставщики, PO, инвойсы с распределениями, маршрутом и циклами согласований, статусом; receipts команд | схема `ap` |
| `Validation` | Определения правил, конвейер, записи оценок | схема `validation` |

Ссылок между контекстами нет (`DDD-6`). Обмен — оркестрацией в слое сценариев (`DDD-14.3`).
Общее ядро значений — `GovErp.Domain.Shared` (исключение GE-E2).

---

## 4. Состав проектов

| Слой | Проекты |
|---|---|
| Общее ядро | `GovErp.Domain.Shared` |
| Правила предметной области | `GovErp.Domain.ChartOfAccounts`, `GovErp.Domain.Ledger`, `GovErp.Domain.Payables`, `GovErp.Domain.Validation` |
| Сценарии | `GovErp.Application.Web` |
| Работа с внешним миром | `GovErp.Infrastructure` |
| Интерфейс и точка сборки | `GovErp.Web` |

```
Web ────────────► Application.Web, Infrastructure (только Program.cs и Extensions/)
Application.Web ► Domain.* , Domain.Shared
Infrastructure ─► Domain.* , Domain.Shared, Application.Web (реализация портов слоя сценариев)
Domain.* ───────► Domain.Shared, BCL
Domain.Shared ──► BCL
```

Тесты: `tests/GovErp.<Проект>.Tests/`, плюс `tests/GovErp.Architecture.Tests`. Тесты Domain
не ссылаются на Infrastructure (`NM-7`).

---

## 5. Внешние зависимости

| Что | Докуда доходит |
|---|---|
| BCL, `System.Text.Json` (атрибуты сериализации снимков) | До слоя правил включительно |
| EF Core, SQL Server provider, Testcontainers | Не дальше Infrastructure и Application.Web.Tests |
| ASP.NET Core, Blazor, cookie auth | Только `GovErp.Web` |
| `Microsoft.Extensions.AI` и провайдеры (Anthropic, OpenAI, Ollama) | Только Infrastructure; порт `IExplanationGenerator` — в Application.Web |
| Bootstrap | Только `GovErp.Web/wwwroot` |

Защитный слой не требуется: внешних систем с собственной моделью нет.

---

## 6. Исключения из общих правил

| Исключение | Правило | Причина | Пересмотреть |
|---|---|---|---|
| **GE-E1** Submit, Reject/Withdraw и Post изменяют несколько агрегатов одной tenant-БД атомарно | `DDD-8.3` | Документ, бюджет, PO claims, журнал и audit должны фиксировать один согласованный финансовый переход; промежуточных коммитов нет | При физическом разделении сервисов спроектировать outbox/saga и состояния ожидания |
| **GE-E2** Проект `GovErp.Domain.Shared` разделяется всеми контекстами | `DDD-6` | Shared kernel по Эвансу: `Money`, `AccountCode`, `FiscalYear`, идентификаторы — значения без поведения, одинаковые во всех контекстах | Если в Shared появится сущность или порт — это ошибка, а не расширение исключения |
| **GE-E3** `TenantId`, `UserId` — обёртки над одним примитивом без собственных правил | `DDD-7.4` | Перепутать два идентификатора между собой слишком легко, компилятор — единственная надёжная защита | Не пересматривается |
| **GE-E4** `Infrastructure` ссылается на `Application.Web` | `CA-7` (граф из общих правил) | Порты `IAuditTrail`, `IExplanationGenerator`, `ITenantContext` объявлены в слое сценариев, потому что не относятся ни к одному контексту; реализации живут снаружи | Если порт станет нужен слою правил — перенести в соответствующий Domain |

---

## 7. Стек

| Что | Выбор |
|---|---|
| Платформа | .NET (LTS), C# |
| Интерфейс | Blazor Server, Bootstrap |
| Хранилище | SQL Server 2022 в Docker; EF Core; БД на тенанта + `GovErp_Master` |
| Обмен сообщениями | нет |
| Аутентификация | cookie auth, пользователи и роли в `GovErp_Master`, `PasswordHasher<T>` |
| LLM | `Microsoft.Extensions.AI`; провайдер из конфигурации (`Template` / `Anthropic` / `OpenAI` / `Ollama`) |
| Тесты | xUnit, FluentAssertions, Testcontainers, NetArchTest |
| Развёртывание | `docker compose`: `sqlserver` + `web` |

---

## 8. Чем проверяется

| Правило | Чем |
|---|---|
| Граф ссылок (`CA-7`, `CA-11`, `DDD-6`) | `Directory.Build.props`: `DisableTransitiveProjectReferences=true`; `GovErp.Architecture.Tests` |
| Domain без фреймворков (`CA-2`) | `Architecture.Tests`: Domain не ссылается на `Microsoft.EntityFrameworkCore*`, `Microsoft.AspNetCore*` |
| Неизменяемость значений (`DDD-7.2`) | `Architecture.Tests`: типы в `ValueObjects/` — records без изменяемых свойств |
| Доменные сервисы без состояния (`DDD-13.1`) | `Architecture.Tests`: типы в `DomainServices/` без изменяемых полей экземпляра |
| DTO наружу (`CA-10`) | `Architecture.Tests`: методы UI-facing `I*AppService` не принимают и не возвращают типы из `Entities/` |
| Инварианты агрегатов (`DDD-8`) | `Domain.*.Tests` |
| Append-only (`GE-12`) | `Application.Web.Tests`: `AppendOnlyViaEf`; скрипт прав runtime-пользователя БД |
| Конкурентность и атомарность (`GE-1`, `GE-14`, `GE-E1`) | `Application.Web.Tests`: `ParallelBudgetSubmits`, `ParallelPoClaims`, `AtomicSubmitFailure`, `AtomicPostFailure`, `OwnReservationIsNotChargedTwice` |
| Идемпотентность (`GE-16`) | `Application.Web.Tests`: `SameCommandRepeated`, `SameCommandDifferentPayload` |
| Повторное согласование (`GE-15`) | `Application.Web.Tests`: `ReapprovalAfterRuleChange` |
| Изоляция тенантов (`GE-6`, `GE-18`) | `Application.Web.Tests`: `TenantIsolation` |
| Форматирование | `.editorconfig`, `dotnet format --verify-no-changes` в сборке |

---

## 9. Где что лежит

| Тема | Где |
|---|---|
| Общие правила разработки | Скилл `ca-ddd-rules` |
| Термины предметной области | `docs/GLOSSARY.md` |
| Дизайн и допущения | `docs/superpowers/specs/` |
| Планы реализации | `docs/superpowers/plans/` |
| Технический долг | `docs/DEBT.md` (заводится при первом пункте) |
| Стиль кода | `.editorconfig` |

**Дизайн-документ не является источником правил.** Решение становится правилом, только когда
его перенесли в этот манифест или в общие правила. Не перенесли — осталось историей.

## 10. Завершённые решения жизненного цикла

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