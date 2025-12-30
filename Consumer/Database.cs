// Database helper for MSSQL: centralizes connection and query execution.
// Reads settings from env vars: DB_HOST, DB_PORT, DB_USER, DB_PASS, DB_NAME,
// DB_ENCRYPT (true/false), DB_TRUST_SERVER_CERT (true/false).
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Data.SqlClient;

internal static class Database
{
    private static string GetConnectionString()
    {
        // Default to localhost for native Windows execution
        var host = Environment.GetEnvironmentVariable("DB_HOST") ?? "beldb12.belsis.local";
        var port = Environment.GetEnvironmentVariable("DB_PORT") ?? "1433";
        var instance = Environment.GetEnvironmentVariable("DB_INSTANCE") ?? "sql2012v2";
        var user = Environment.GetEnvironmentVariable("DB_USER") ?? "raportest";
        var pass = Environment.GetEnvironmentVariable("DB_PASS") ?? "raportest";
        var name = Environment.GetEnvironmentVariable("DB_NAME") ?? "raportestedremit";

        // Build connection string with encryption for SQL Server 2012
        var dataSource = string.IsNullOrWhiteSpace(instance) ? $"{host},{port}" : $"{host}\\{instance}";
        
        // Use encrypted connection (works natively on Windows with SSMS settings)
        return $"Data Source={dataSource};Initial Catalog={name};User ID={user};Password={pass};TrustServerCertificate=True;Encrypt=True;";
    }

    public static async Task<int> ExecuteNonQueryAsync(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);
        using var cmd = new SqlCommand(sql, conn);
        if (parameters != null)
        {
            foreach (var p in parameters) cmd.Parameters.Add(p);
        }
        return await cmd.ExecuteNonQueryAsync(cancellationToken);
    }

    public static async Task<object?> ExecuteScalarAsync(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);
        using var cmd = new SqlCommand(sql, conn);
        if (parameters != null)
        {
            foreach (var p in parameters) cmd.Parameters.Add(p);
        }
        return await cmd.ExecuteScalarAsync(cancellationToken);
    }

    public static async Task<List<Dictionary<string, object?>>> ExecuteQueryAsync(string sql, IEnumerable<SqlParameter>? parameters = null, CancellationToken cancellationToken = default)
    {
        using var conn = new SqlConnection(GetConnectionString());
        await conn.OpenAsync(cancellationToken);
        using var cmd = new SqlCommand(sql, conn);
        if (parameters != null)
        {
            foreach (var p in parameters) cmd.Parameters.Add(p);
        }
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        var results = new List<Dictionary<string, object?>>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>(reader.FieldCount);
            for (int i = 0; i < reader.FieldCount; i++)
            {
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            }
            results.Add(row);
        }
        return results;
    }
}