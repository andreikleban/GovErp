using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;

namespace GovErp.Infrastructure.Startup;

/// <summary>The tenant runtime user: read/write of its own database, UPDATE/DELETE denied on append-only tables (GE-12). Migrations use a separate user.</summary>
public static partial class DatabaseSecurity
{
    private static readonly string[] AppendOnlyTables =
        ["validation.EvaluationRecords", "validation.Explanations", "ledger.JournalEntries", "ledger.JournalLines", "audit.Events", "ap.ProcessedCommands"];

    public static async Task EnsureRuntimeUserAsync(string migrationConnection, string database, string login, string password, CancellationToken ct)
    {
        RequireIdentifier(database);
        RequireIdentifier(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);   // an empty password from incomplete configuration is a startup error, not an open login
        var pwd = password.Replace("'", "''", StringComparison.Ordinal);
        var sql = $"""
            {LoginSql(login, pwd)}
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

    /// <summary>The Master runtime user: read-only access to the tenant catalog and user accounts (no db_datawriter, no DENY).</summary>
    public static async Task EnsureReadOnlyUserAsync(string migrationConnection, string database, string login, string password, CancellationToken ct)
    {
        RequireIdentifier(database);
        RequireIdentifier(login);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);   // an empty password from incomplete configuration is a startup error, not an open login
        var pwd = password.Replace("'", "''", StringComparison.Ordinal);
        var sql = $"""
            {LoginSql(login, pwd)}
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

    /// <summary>
    /// The login is created on first start and gets the password from the current configuration on later starts:
    /// a password change in .env / AppHost applies to an existing database without a manual ALTER LOGIN.
    /// </summary>
    private static string LoginSql(string login, string escapedPassword) => $"""
        IF NOT EXISTS (SELECT 1 FROM sys.server_principals WHERE name = N'{login}')
            CREATE LOGIN [{login}] WITH PASSWORD = N'{escapedPassword}', CHECK_POLICY = OFF;
        ELSE
            ALTER LOGIN [{login}] WITH PASSWORD = N'{escapedPassword}';
        """;

    /// <summary>Identifiers cannot be passed as parameters, so only validated names are used.</summary>
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
