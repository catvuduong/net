using Npgsql;

namespace Api;

/// <summary>
/// PostgreSQL access (Npgsql; use Dapper on the returned connection for queries).
/// Add every table as a CREATE TABLE IF NOT EXISTS statement to <see cref="Schema"/>; it runs once per process,
/// serialized by an advisory lock so concurrent instances cannot race.
/// </summary>
public sealed class Db
{
    private static readonly string[] Schema = [
        // "CREATE TABLE IF NOT EXISTS items (id serial PRIMARY KEY, name text NOT NULL)",
    ];

    private readonly NpgsqlDataSource? _source;
    private Task? _ready;

    public Db()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");
        _source = string.IsNullOrWhiteSpace(url) ? null : NpgsqlDataSource.Create(ToConnectionString(url));
    }

    public bool Enabled => _source is not null;

    public async Task<NpgsqlConnection> OpenAsync()
    {
        if (_source is null) throw new InvalidOperationException("DATABASE_URL is not set");
        await EnsureSchemaAsync();
        return await _source.OpenConnectionAsync();
    }

    public async Task<string> StatusAsync()
    {
        if (_source is null) return "disabled";
        try
        {
            await using var conn = await OpenAsync();
            await using var cmd = new NpgsqlCommand("SELECT 1", conn);
            await cmd.ExecuteScalarAsync();
            return "up";
        }
        catch
        {
            return "down";
        }
    }

    private Task EnsureSchemaAsync() => _ready ??= RunSchemaAsync();

    private async Task RunSchemaAsync()
    {
        try
        {
            await using var conn = await _source!.OpenConnectionAsync();
            await using var tx = await conn.BeginTransactionAsync();
            await new NpgsqlCommand("SELECT pg_advisory_xact_lock(727274)", conn, tx).ExecuteNonQueryAsync();
            foreach (var statement in Schema)
                await new NpgsqlCommand(statement, conn, tx).ExecuteNonQueryAsync();
            await tx.CommitAsync();
        }
        catch
        {
            _ready = null; // retry on the next call
            throw;
        }
    }

    /// <summary>Converts postgres://user:pass@host:5432/db?sslmode=require into an Npgsql connection string.</summary>
    public static string ToConnectionString(string url)
    {
        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var query = System.Web.HttpUtility.ParseQueryString(uri.Query);
        var local = uri.Host is "localhost" or "127.0.0.1";
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort || uri.Port < 0 ? 5432 : uri.Port,
            Username = Uri.UnescapeDataString(userInfo[0]),
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : null,
            Database = uri.AbsolutePath.TrimStart('/'),
            SslMode = query["sslmode"] is "disable" || (local && query["sslmode"] is null) ? SslMode.Disable : SslMode.Require,
        };
        return builder.ConnectionString;
    }
}
