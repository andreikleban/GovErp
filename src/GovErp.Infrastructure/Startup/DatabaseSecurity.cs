using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace GovErp.Infrastructure.Startup;

/// <summary>Runtime-пользователь тенанта: чтение/запись своей БД, запрет UPDATE/DELETE append-only таблиц (GE-12). Миграции — отдельным пользователем.</summary>
public static partial class DatabaseSecurity
{
    private static readonly string[] AppendOnlyTables =
        ["validation.EvaluationRecords", "validation.Explanations", "ledger.JournalEntries", "ledger.JournalLines", "audit.Events", "ap.ProcessedCommands"];

    public static async Task EnsureRuntimeUserAsync(string migrationConnection, string database, string login, string password, CancellationToken ct)
    {
        RequireIdentifier(database);
        RequireIdentifier(login);
        var pwd = password.Replace("'", "''", StringComparison.Ordinal);
        var sql = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{login}')
                CREATE LOGIN [{login}] WITH PASSWORD = N'{pwd}', CHECK_POLICY = OFF;
            USE [{database}];
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{login}')
                CREATE USER [{login}] FOR LOGIN [{login}];
            ALTER ROLE db_datareader ADD MEMBER [{login}];
            ALTER ROLE db_datawriter ADD MEMBER [{login}];
            {string.Join('\n', AppendOnlyTables.Select(t => $"DENY UPDATE, DELETE ON {t} TO [{login}];"))}
            """;
        await using var connection = new SqlConnection(migrationConnection);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Master runtime-пользователь: только чтение каталога тенантов и учётных записей (без db_datawriter, без DENY).</summary>
    public static async Task EnsureReadOnlyUserAsync(string migrationConnection, string database, string login, string password, CancellationToken ct)
    {
        RequireIdentifier(database);
        RequireIdentifier(login);
        var pwd = password.Replace("'", "''", StringComparison.Ordinal);
        var sql = $"""
            IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{login}')
                CREATE LOGIN [{login}] WITH PASSWORD = N'{pwd}', CHECK_POLICY = OFF;
            USE [{database}];
            IF NOT EXISTS (SELECT 1 FROM sys.database_principals WHERE name = N'{login}')
                CREATE USER [{login}] FOR LOGIN [{login}];
            ALTER ROLE db_datareader ADD MEMBER [{login}];
            """;
        await using var connection = new SqlConnection(migrationConnection);
        await connection.OpenAsync(ct);
        await using var command = new SqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Идентификаторы нельзя передать параметром — только проверенные имена.</summary>
    private static void RequireIdentifier(string value)
    {
        if (!Identifier().IsMatch(value))
        {
            throw new ArgumentException($"'{value}' is not a safe SQL identifier.", nameof(value));
        }
    }

    [GeneratedRegex("^[A-Za-z][A-Za-z0-9_]{0,63}$")]
    private static partial Regex Identifier();
}
