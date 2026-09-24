using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using GovErp.Application.Web.Commands;
using GovErp.Application.Web.Common;
using GovErp.Application.Web.Tenancy;
using GovErp.Domain.ChartOfAccounts.Exceptions;
using GovErp.Domain.Ledger.Exceptions;
using GovErp.Domain.Payables.Exceptions;
using GovErp.Domain.Validation.Exceptions;
using GovErp.Infrastructure.Persistence;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GovErp.Infrastructure.Operations;

/// <summary>
/// An atomic command: scope and DbContext of the actor's tenant, a transaction, a receipt by CommandId, one SaveChanges + commit.
/// A rowversion conflict or deadlock means rollback and one retry in a new scope, then a retryable Conflict.
/// </summary>
public sealed class EfTenantOperationRunner(IServiceScopeFactory scopes) : ITenantOperationRunner
{
    public async Task<CommandResult<T>> ExecuteAsync<T>(ActorContext actor, CommandEnvelope envelope, string commandType, object request,
        Func<IServiceProvider, CancellationToken, Task<CommandResult<T>>> body, IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        var requestHash = Hash(commandType, request);
        CommandResult<T> Done(CommandResult<T> result)
        {
            CommandMetrics.Record(commandType, result.Status.ToString());
            return result;
        }

        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            if (!await InitializeTenantAsync(sp, actor, ct))
            {
                return Done(CommandResult<T>.NotFound(AppErrors.TenantNotRegistered, ("tenant", actor.TenantId)));
            }

            var db = sp.GetRequiredService<GovErpDbContext>();
            var clock = sp.GetRequiredService<IClock>();
            await using var tx = await db.Database.BeginTransactionAsync(isolation, ct);
            try
            {
                var receipts = sp.GetRequiredService<ICommandReceipts>();
                if (await receipts.FindAsync(envelope.CommandId, ct) is { } existing)
                {
                    if (existing.ActorId != actor.UserId)
                    {
                        return Done(CommandResult<T>.Forbidden(AppErrors.CommandOfAnotherUser));
                    }

                    return Done(existing.CommandType == commandType && existing.RequestHash == requestHash
                        ? JsonSerializer.Deserialize<CommandResult<T>>(existing.ResultJson, JsonColumn.Options)!
                        : CommandResult<T>.Conflict(Problem.Of(AppErrors.CommandIdReused)) with { Retryable = false });
                }

                var result = await body(sp, ct);
                receipts.Add(new CommandReceipt(envelope.CommandId, actor.UserId, commandType, requestHash,
                    JsonSerializer.Serialize(result, JsonColumn.Options), clock.Now));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return Done(result);
            }
            // Both IsTransient filters come before DbUpdateException: DbUpdateConcurrencyException derives from it.
            catch (Exception ex) when (IsTransient(ex) && attempt == 1)
            {
                // Rollback happens when the transaction is disposed; the retry runs in a new scope on fresh data.
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                return Done(CommandResult<T>.Conflict(Problem.Of(AppErrors.DataChanged)));
            }
            catch (DbUpdateException ex) when (UniqueIndexOf(ex) is { } index)
            {
                if (index.Contains("ProcessedCommands", StringComparison.OrdinalIgnoreCase) && attempt == 1)
                {
                    continue;   // a concurrent duplicate of the same command: the next attempt returns the saved receipt
                }

                return Done(index.Contains("UX_VendorInvoices_Vendor_Number", StringComparison.OrdinalIgnoreCase)
                    ? CommandResult<T>.Refused(default, PayablesErrors.DuplicateNumber)
                    : CommandResult<T>.Conflict(Problem.Of(AppErrors.AlreadyApplied)) with { Retryable = false });
            }
            catch (AuthorizationException ex) { return Done(CommandResult<T>.Forbidden(ex.Problem)); }
            catch (NotFoundException ex) { return Done(CommandResult<T>.NotFound(ex.Problem)); }
            catch (PayablesException ex) { return Done(CommandResult<T>.Refused(default, ex.Problem)); }
            catch (LedgerException ex) { return Done(CommandResult<T>.Refused(default, ex.Problem)); }
            catch (ValidationException ex) { return Done(CommandResult<T>.Refused(default, ex.Problem)); }
            catch (ChartOfAccountsException ex) { return Done(CommandResult<T>.Refused(default, ex.Problem)); }
            catch (ArgumentException ex)
            {
                // Domain values (account codes, Money, required strings) validate input in their constructors.
                // Invalid form input is a refusal with rollback, not an unhandled exception in the UI.
                return Done(CommandResult<T>.Refused(default, ex is InvalidValueException invalid ? invalid.Problem : Problem.Of(AppErrors.InvalidInput, ("parameter", ex.ParamName))));
            }
        }
    }

    public async Task<T> QueryAsync<T>(ActorContext actor, Func<IServiceProvider, CancellationToken, Task<T>> body, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (!await InitializeTenantAsync(scope.ServiceProvider, actor, ct))
        {
            throw new NotFoundException(AppErrors.TenantNotRegistered, ("tenant", actor.TenantId));
        }

        return await body(scope.ServiceProvider, ct);
    }

    private static async Task<bool> InitializeTenantAsync(IServiceProvider sp, ActorContext actor, CancellationToken ct)
    {
        var catalog = sp.GetRequiredService<ITenantCatalog>();
        var tenant = await catalog.FindAsync(actor.TenantId, ct);
        if (tenant is null)
        {
            return false;
        }

        sp.GetRequiredService<ITenantContextInitializer>().Initialize(tenant.Id, catalog.RuntimeConnectionString(tenant));
        return true;
    }

    private static bool IsTransient(Exception ex) =>
        ex is DbUpdateConcurrencyException
        || ex.GetBaseException() is SqlException { Number: 1205 or 3960 };   // deadlock victim, snapshot/serializable conflict

    private static string? UniqueIndexOf(DbUpdateException ex) =>
        ex.GetBaseException() is SqlException { Number: 2601 or 2627 } sql ? sql.Message : null;

    private static string Hash(string commandType, object request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commandType + "|" + JsonSerializer.Serialize(request, request.GetType(), JsonColumn.Options))))
            .ToLowerInvariant();
}
