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
| **GE-11** | Порядок шагов конвейера фиксирован (1..8); конфигурируется включённость, severity и параметры правил по слоям `Core / Federal / State / Tenant` | Конфликт одного `RuleId` в разных слоях — побеждает более специфичный; ослаблять `Core` / `Federal` нельзя |
| **GE-12** | `EvaluationRecord` и `audit.Events` — только добавление; UPDATE / DELETE отклоняются interceptor'ом | Воспроизводимость решения через годы — требование, не пожелание |
| **GE-13** | Объяснения генерируются только из сохранённого `EvaluationRecord` через порт `IExplanationGenerator`; LLM-провайдер — через `Microsoft.Extensions.AI.IChatClient`, выбирается конфигурацией | Текст объяснения архитектурно не может повлиять на решение. Свой интерфейс над `IChatClient` не вводится (`DDD-15`) |
| **GE-14** | Резервирование бюджета (`BudgetLine.Reserve`) происходит на Submit, фиксация (`Commit`) — на Post | Защита от двойного списания не зависит от транзакционности Post |

---

## 3. Ограниченные контексты

| Контекст | Предметная область | Хранилище |
|---|---|---|
| `ChartOfAccounts` | Сегменты плана счетов, гранты, допустимые комбинации, правила комбинаций | схема `coa` |
| `Ledger` | Бюджетные строки и резервы, encumbrances, журнал проводок, финансовые периоды | схема `ledger` |
| `Payables` | Поставщики, PO, инвойсы с распределениями, согласованиями и статусом | схема `ap` |
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
| **GE-E1** Post изменяет `VendorInvoice` (Payables) и `BudgetLine`, `Encumbrance`, `JournalEntry` (Ledger) в одной транзакции БД | `DDD-8.3` | Проводка в книгу и обновление бюджетных остатков — одно учётное событие; промежуточное состояние «Posted без actuals» нарушает hard budget control, который является законодательным требованием | При выносе Ledger в отдельный сервис — outbox с `InvoicePosted` и идемпотентными обработчиками; резерв (GE-14) защищает от overspend в окне eventual consistency |
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
| DTO наружу (`CA-10`) | `Architecture.Tests`: публичные методы Application не возвращают типы из `Entities/` |
| Инварианты агрегатов (`DDD-8`) | `Domain.*.Tests` |
| Append-only (`GE-12`) | `Application.Web.Tests`: `EvaluationRecord_Update_IsRejected` |
| Конкурентность (`GE-1`, `GE-14`) | `Application.Web.Tests`: `Submit_TwoParallelInvoices_OneBudgetLine_ExactlyOneSucceeds` |
| Изоляция тенантов (`GE-6`) | `Application.Web.Tests`: `Tenant_Shelbyville_CannotSeeSpringfieldInvoices` |
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
