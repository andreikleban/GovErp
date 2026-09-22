# Технический долг

Как есть, а не как должно быть (правила — в `ARCHITECTURE.md`). Пункт закрывается кодом или переносом в манифест как исключение.

| # | Где | Что | Почему не сейчас | Риск до исправления |
|---|---|---|---|---|
| D-1 | `Domain.Validation/ValueObjects/TransactionSnapshot`, `ValidationSubject` | Накопленные алиасы: `TransactionVersion` / `ContentVersion`, `Date` / `InvoiceDate`, `ApprovalsSoFar` + `DetailedApprovals`, `VersionsAtLastApproval` рядом с fingerprint на транзакции | Переименование затрагивает почти все 209 тестов Validation | Путаница при написании сборщика снимков (план 3): использовать `ContentVersion`, `InvoiceDate`, `DetailedApprovals`, `Transaction.RuleFingerprint` |
| D-2 | `Domain.Validation/ValueObjects/BudgetSnapshot` | `FiscalYear = 2026` по умолчанию в конструкторе | Тот же объём правки тестов | Низкий: `ValidationPipeline` отклоняет снимок, чей бюджетный год не совпадает с годом PostingDate (`VALIDATION_INPUT`); сборщик обязан передавать год явно |
| D-3 | `Domain.Validation/DomainServices/RuleResolution.EnsureSafeOverride` | Смысл параметров («threshold выше — слабее», «pct ниже — слабее») зашит в резолвер, а не в класс правила | Работает и покрыт тестами; перенос в `IValidationRule` — рефакторинг без изменения поведения | Новое правило с параметрами требует правки резолвера; неизвестный параметр безопасно запрещает любое изменение |
| D-4 | `Domain.Payables/Entities/VendorInvoice.Reference` | `INV-{Guid}` — стабильно, но нечитаемо в журнале и аудите | Номер инвойса может меняться в Draft, Guid — нет | Только удобство: UI и объяснения показывают номер отдельно |
| D-5 | `VendorInvoice.ReservationRefs` / `EncumbranceClaimRefs` / `PoBillingClaimRefs` | Ссылки дублируют владельца (`InvoiceId + ContentVersion`), записанного на резервах и claims в Ledger | Осознанный выбор: явные ссылки для Post / Reject / Withdraw | Расхождение ловится проверкой владельца в `Commit` / `ConsumeClaim` (исключение → откат транзакции) |
