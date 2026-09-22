using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Commands;

public interface ITenantOperationRunner
{
    /// <summary>
    /// Мутирующая команда: новый scope и DbContext тенанта актора, транзакция с заданной изоляцией, receipt по CommandId,
    /// один SaveChanges + commit; конфликт rowversion — rollback и один повтор в новом scope, затем retryable Conflict.
    /// Тело не вызывает SaveChanges. Результат Refused тоже фиксируется (оценка, аудит, receipt) — финансовые изменения тело в этом случае не делает.
    /// </summary>
    Task<CommandResult<T>> ExecuteAsync<T>(ActorContext actor, CommandEnvelope envelope, string commandType, object request,
        Func<IServiceProvider, CancellationToken, Task<CommandResult<T>>> body,
        System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default);

    /// <summary>Чтение: новый scope тенанта актора, без транзакции и без сохранения.</summary>
    Task<T> QueryAsync<T>(ActorContext actor, Func<IServiceProvider, CancellationToken, Task<T>> body, CancellationToken ct = default);
}
