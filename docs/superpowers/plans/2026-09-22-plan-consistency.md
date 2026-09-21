# Обязательные уточнения к планам 1–4

Это результат сверки планов 22 сентября 2026. При противоречии с ранними фрагментами кода выполнять решения ниже. Код в планах — проектные наброски, он ещё не собран и не протестирован. Финансовые переходы и завершённые правила жизненного цикла перенесены в спецификацию и манифест; новые функции сверх согласованного объёма не добавляются.

## 1. Даты, сценарии и честные допущения

- Демо-дата документа: **2026-06-15**; FY2026: **2025-07-01 … 2026-06-30**. Июнь 2026 открыт; май 2026 закрыт. Это заменяет сентябрь 2026 в набросках (он относится к FY2027). Время audit/evaluation — фактическое время операции, дата документа хранится отдельно.
- Поправка бюджета указывает FY2026 и effective date 2026-06-15 явно. `AmendBudgetCommand` расширить полями `int FiscalYear, DateOnly EffectiveDate`; не вычислять бюджетный год из текущего времени сервера.
- Грант G-COPS-26 — условный грант из задания. Название не доказывает агентство, срок и правовые условия. Все ограничения гранта, закупочный порог 25000, SAM-флаг и hard/soft по фондам — **допущения демо**, не юридические нормы США.
- После amendment +13000 и procurement override итог **Warning**, а не Allowed: остаётся правило low remaining (0 < 10%). Warning не блокирует Post после остальных проверок.
- В основной строке 96000 encumbrances не содержат PO на 160000. PO-backed сценарий использует отдельную Police-комбинацию с бюджетом 500000 / actuals 100000 / encumbrances 160000.

## 2. Атомарные Submit и Reject

В одной tenant-БД не нужен процесс резервирования каждой строки отдельной транзакцией с компенсациями. **Submit, Reject и Post выполняются атомарно**: документ, все затронутые бюджетные строки, заявки на остаток PO, evaluation, audit и receipt команды сохраняются вместе. Это заменяет `ReserveWithRetryAsync` и компенсации в плане 3 task 5; ранний псевдокод не копировать.

Алгоритм Submit:

1. Начать транзакцию в новом operation scope, загрузить актуальные invoice/бюджеты/PO; строки бюджета и PO обрабатывать в стабильном порядке идентификаторов.
2. Проверить receipt команды и ожидаемую версию инвойса; построить снимок и оценку. Сгруппировать distributions по budget key: две строки одного инвойса не могут независимо расходовать весь один остаток.
3. При HardStop сохранить отказ/evaluation без резервов. Иначе создать бюджетные резервы и PO claims, изменить статус на Submitted, сохранить audit/receipt.
4. Один SaveChanges и commit. Конфликт rowversion — rollback всего, dispose scope, повторить всю операцию на свежих данных не более одного раза. Исчерпание retry вернуть как Conflict/retryable, а не ложное утверждение об отсутствии бюджета.

Reject освобождает все Held и PO claims и возвращает документ в Draft в той же транзакции; решение Reject остаётся в истории. Не освобождать резервы в `finally` успешного Submit.

Обновить GE-E1: единая транзакция нужна для финансового перехода нескольких агрегатов в пределах текущего модульного монолита. Это осознанная архитектурная граница, не утверждение о требовании конкретного закона. Outbox/saga остаются вариантом при физическом разделении сервисов.

## 3. Собственный резерв и захват остатка PO

Доступный остаток для новых операций:

```text
Available = Amended - Actuals - Encumbered - Held
RequiredNewBudget = InvoiceAmount - EligibleLiquidation
AvailableForThisInvoice = Available + OwnHeld
ProjectedAvailable = AvailableForThisInvoice - RequiredNewBudget
```

OwnHeld — активные резервы **этого документа и его текущей версии** на данной бюджетной строке. При Approve/Post нельзя ещё раз вычитать собственный резерв. В снимке сохранять Available, OwnHeld, RequiredNewBudget и ProjectedAvailable раздельно.

На Submit также захватывается часть существующего encumbrance. Добавить в Ledger дочернюю сущность `EncumbranceClaim`: Id, InvoiceId, ContentVersion, Amount, Status (Held/Consumed/Released). Это не новый encumbrance и не дополнительное вычитание из бюджета.

```text
ClaimableForInvoice = RemainingEncumbrance - OtherInvoicesHeldClaims
EligibleLiquidation = min(InvoiceAmount, ClaimableForInvoice)
```

При нескольких distributions на одну PO line считать суммарно. Методы `Encumbrance.Claim(invoiceId, version, amount)`, `ConsumeClaim(claimId)`, `ReleaseClaim(claimId)` защищают remaining и ownership; изменение claims увеличивает ChangeStamp владельца, вызывая UPDATE с rowversion. При Post использовать закреплённый claim, не вычислять свободную liquidation заново из уже захваченного остатка.

## 4. Post и отсутствие двойного счёта

Загрузка, перевалидация и запись Post происходят внутри одной транзакции. Защитить прочитанные изменяемые правила/период от изменения до commit: для прототипа Serializable на Post с коротким временем транзакции. Все retries выполняются в новом DbContext. LLM-вызовов внутри транзакций нет.

Проверяемый инвариант по каждой бюджетной строке:

```text
Actuals_after = Actuals_before + Sum(invoice distributions)
Encumbered_after = Encumbered_before - Sum(consumed PO claims)
Held_after = Held_before - Sum(own budget reservations)
```

`BudgetLine.Commit(reservationId)` в плане 1 уже увеличивает Actuals на сумму нового бюджетного резерва; `RecordLiquidation(amount)` должен одновременно уменьшать Encumbered и увеличивать Actuals на ликвидированную часть. Проверить это в реализации. Не добавлять Actuals на весь invoice поверх этих двух действий. Альтернативное разбиение методов допустимо только при сохранении приведённого инварианта и тестов.

Уникальный ключ журнала `(SourceRef, PostingKind)` и receipt команды исключают второй Post. Финансовые и бюджетные строки балансируются **отдельно** внутри каждого фонда. Receipt, journal, actuals, claims, статус и audit коммитятся вместе.

## 5. Команды, разрешения и версии

- Mutating-команды получают `CommandId` и `ExpectedRowVersion`; формы удерживают CommandId при повторе того же запроса. Изменённые данные — новый CommandId.
- `ap.ProcessedCommands`: CommandId, ActorId, CommandType, RequestHash, ResultJson; unique CommandId внутри tenant-БД. Повтор того же запроса возвращает receipt, другой payload под тем же ID — Conflict. Одного сравнения целевого статуса недостаточно.
- Override привязан к evaluation/outcome, RuleId + RuleVersion, строке распределения, ContentVersion, actor, reason. Перенос на изменённый документ или другую строку запрещён. В application-контракт Override добавить evaluationId, distributionLine и expectedVersion.
- DepartmentHead удовлетворяет только шаг своего департамента. Проверки tenant/role/department и автор ≠ согласующий выполняются сервером.
- Хранить полный перечень применённых версий правил, включая прошедшие, а не только максимальный номер по слою. Итоговый fingerprint вычислять по упорядоченному набору ID/version/parameters и версии engine.
- Изменение fingerprint запускает новую оценку и при необходимости новое согласование. После повторного согласования по актуальному fingerprint Post должен стать возможным; не создавать вечный `REVALIDATION_REQUIRED`.
- «Tenant > Federal» не является универсальным правилом права. В демо специализированные параметры разрешены только для явно overridable правила; обязательные ограничения не исчезают при локальном переопределении. Ослабление параметров проверять по типу правила (например, повышение закупочного threshold), не только по Severity.
- Тест архитектуры ограничивает **UI-facing app-services**, а не все публичные типы Application: порты репозиториев и генератора закономерно принимают доменные типы.

## 6. Blazor, tenancy и хранение

- DbContext не живёт весь circuit; scope/DbContext создаётся на каждую операцию. TenantContext инициализируется один раз по authenticated principal и серверному каталогу; произвольный tenantId из формы/URL не определяет connection string.
- БД на тенанта — логическая изоляция данных, не физически отдельный сервер. Использовать разные DB users с доступом к своей БД; SA/migration credentials не использовать в runtime. Каталог хранит database name/ссылку на secret, не передаёт секреты в DTO.
- SQL volume переживает обычный restart. `docker compose down -v` исключён из штатной проверки: это удаление данных.
- Interceptor append-only защищает EF-путь. Для runtime DB user запретить UPDATE/DELETE таблиц evaluations/audit/journal; migration user отдельно. Не объявлять interceptor полной защитой от SQL-доступа.
- AccountCode хранится строкой через converter. `CombinationRule` как отдельный интерпретатор исключён: whitelist + типизированные ограничения фондов/грантов. Это согласовать с манифестом перед кодом.
- Entity.ChangeStamp для owned-коллекций необходим, иначе вставка reservation/claim может не обновить rowversion корня. Проверить реальными параллельными SQL-тестами.

## 7. Обязательные regression-тесты (план 3 task 8)

| Тест | Условия | Ожидаемый результат |
|---|---|---|
| OwnReservationIsNotChargedTwice | Hard available 147000; Submit 100000; Approve/Post | Нет повторного дефицита; после Post Available 47000 |
| SameBudgetAcrossDistributions | Две строки по 80000, один budget available 147000 | HardStop 13000; ни одного Held |
| ParallelBudgetSubmits | Два инвойса по 100000 при available 147000 | Один Submitted; Held 100000; другой отказ/Conflict |
| ParallelPoClaims | Два инвойса на 96000 при PO remaining 96000, свободный бюджет 0 | Только один захватывает PO; второй не проходит |
| PoPostingTotals | Budget 500000, Actuals 100000, Encumbered 160000; invoice 160000 | Actuals 260000; Encumbered 0; Available 240000 |
| MixedPostingTotals | PO remaining 96000; invoice 100000; excess разрешён | Actuals +100000; Encumbered -96000; Available -4000 |
| AtomicSubmitFailure | Ошибка на второй бюджетной строке | Нет частичных резервов/claims/Submitted |
| AtomicPostFailure | Ошибка при записи journal | Ни один бюджет/PO/статус не изменился |
| SameCommandRepeated | Одинаковый CommandId + payload дважды | Одна мутация, один receipt, один журнал |
| ReapprovalAfterRuleChange | Approve v1; новое правило; Approve v2; Post | Post возможен после актуального согласования |

Concurrency-тесты синхронизируют две операции барьером после чтения, используют независимые scope и настоящий SQL Server. Повторение Task.WhenAll без синхронизации не доказывает воспроизведение гонки. Unit-тесты и integration-тесты не отмечать завершёнными до реального запуска.

## Порядок применения

- [x] Обновить spec и ARCHITECTURE: атомарный Submit/Post, версии содержимого и цикл согласований; удалить старые алгоритмы Submit/Post из плана 3.
- [ ] План 1 task 6–7: ownership резервов/claims, ChangeStamp, арифметика Post.
- [ ] План 2 tasks 1, 5–7: снимки own holds/claims, группировка строк, fingerprint и корректный Warning.
- [ ] План 3 tasks 3–5, 8: схемы claims/receipts, атомарные операции, permissions, все regression-тесты.
- [ ] План 4: operation scopes, UI, provider adapters, окончательная проверка демо.
