using GovErp.Application.Web.Common;

namespace GovErp.Application.Web.Commands;

public interface ITenantOperationRunner
{
    /// <summary>
    /// Mutating command: a new scope and DbContext of the actor's tenant, a transaction with the given isolation, a receipt by CommandId,
    /// one SaveChanges + commit; a rowversion conflict means rollback and one retry in a new scope, then a retryable Conflict.
    /// The body does not call SaveChanges. A Refused result is persisted too (evaluation, audit, receipt); the body makes no financial changes in that case.
    /// </summary>
    Task<CommandResult<T>> ExecuteAsync<T>(ActorContext actor, CommandEnvelope envelope, string commandType, object request,
        Func<IServiceProvider, CancellationToken, Task<CommandResult<T>>> body,
        System.Data.IsolationLevel isolation = System.Data.IsolationLevel.ReadCommitted, CancellationToken ct = default);

    /// <summary>Read: a new scope of the actor's tenant, no transaction and no save.</summary>
    Task<T> QueryAsync<T>(ActorContext actor, Func<IServiceProvider, CancellationToken, Task<T>> body, CancellationToken ct = default);
}
