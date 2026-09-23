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
/// Атомарная команда: scope и DbContext тенанта актора, транзакция, receipt по CommandId, один SaveChanges + commit.
/// Конфликт rowversion или deadlock — откат и один повтор в новом scope, затем retryable Conflict.
/// </summary>
public sealed class EfTenantOperationRunner(IServiceScopeFactory scopes) : ITenantOperationRunner
{
    public async Task<CommandResult<T>> ExecuteAsync<T>(ActorContext actor, CommandEnvelope envelope, string commandType, object request,
        Func<IServiceProvider, CancellationToken, Task<CommandResult<T>>> body, IsolationLevel isolation = IsolationLevel.ReadCommitted,
        CancellationToken ct = default)
    {
        var requestHash = Hash(commandType, request);
        for (var attempt = 1; ; attempt++)
        {
            await using var scope = scopes.CreateAsyncScope();
            var sp = scope.ServiceProvider;
            if (!await InitializeTenantAsync(sp, actor, ct))
            {
                return CommandResult<T>.NotFound($"Tenant {actor.TenantId} is not registered.");
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
                        return CommandResult<T>.Forbidden("This command id belongs to another user.");
                    }

                    return existing.CommandType == commandType && existing.RequestHash == requestHash
                        ? JsonSerializer.Deserialize<CommandResult<T>>(existing.ResultJson, JsonColumn.Options)!
                        : new CommandResult<T>(CommandStatus.Conflict, default, "This command id was already used for a different request.", false);
                }

                var result = await body(sp, ct);
                receipts.Add(new CommandReceipt(envelope.CommandId, actor.UserId, commandType, requestHash,
                    JsonSerializer.Serialize(result, JsonColumn.Options), clock.Now));
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return result;
            }
            // Оба фильтра IsTransient стоят раньше DbUpdateException: DbUpdateConcurrencyException — её наследник.
            catch (Exception ex) when (IsTransient(ex) && attempt == 1)
            {
                // Откат — при dispose транзакции; повтор в новом scope на свежих данных.
            }
            catch (Exception ex) when (IsTransient(ex))
            {
                return CommandResult<T>.Conflict("The data was changed by another user. Reload and try again.");
            }
            catch (DbUpdateException ex) when (UniqueIndexOf(ex) is { } index)
            {
                if (index.Contains("ProcessedCommands", StringComparison.OrdinalIgnoreCase) && attempt == 1)
                {
                    continue;   // параллельный дубль той же команды: следующая попытка вернёт сохранённый receipt
                }

                return index.Contains("UX_VendorInvoices_Vendor_Number", StringComparison.OrdinalIgnoreCase)
                    ? CommandResult<T>.Refused(default, "An invoice with this number already exists for this vendor.")
                    : new CommandResult<T>(CommandStatus.Conflict, default, $"The operation was already applied ({index}).", false);
            }
            catch (AuthorizationException ex) { return CommandResult<T>.Forbidden(ex.Message); }
            catch (NotFoundException ex) { return CommandResult<T>.NotFound(ex.Message); }
            catch (Exception ex) when (ex is PayablesException or LedgerException or ValidationException or ChartOfAccountsException)
            {
                return CommandResult<T>.Refused(default, ex.Message);
            }
            catch (ArgumentException ex)
            {
                // Значения домена (коды счетов, Money, обязательные строки) проверяют ввод конструктором.
                // Неверный ввод формы — отказ с откатом, а не необработанное исключение в UI.
                return CommandResult<T>.Refused(default, InputProblem(ex));
            }
        }
    }

    public async Task<T> QueryAsync<T>(ActorContext actor, Func<IServiceProvider, CancellationToken, Task<T>> body, CancellationToken ct = default)
    {
        await using var scope = scopes.CreateAsyncScope();
        if (!await InitializeTenantAsync(scope.ServiceProvider, actor, ct))
        {
            throw new NotFoundException($"Tenant {actor.TenantId} is not registered.");
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

    /// <summary>Сообщение без технического хвоста « (Parameter 'x')».</summary>
    private static string InputProblem(ArgumentException ex) =>
        ex.ParamName is { } name ? ex.Message.Replace($" (Parameter '{name}')", "", StringComparison.Ordinal) : ex.Message;

    private static string? UniqueIndexOf(DbUpdateException ex) =>
        ex.GetBaseException() is SqlException { Number: 2601 or 2627 } sql ? sql.Message : null;

    private static string Hash(string commandType, object request) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(commandType + "|" + JsonSerializer.Serialize(request, request.GetType(), JsonColumn.Options))))
            .ToLowerInvariant();
}
