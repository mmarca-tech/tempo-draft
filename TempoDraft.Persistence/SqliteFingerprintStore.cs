using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using TempoDraft.Core;

namespace TempoDraft.Persistence;

public sealed class SqliteFingerprintStore : IFingerprintStore
{
    private readonly string _dbPath;

    public SqliteFingerprintStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task EnsureCreatedAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? ".");
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS fingerprints (
    hash TEXT PRIMARY KEY,
    date TEXT NOT NULL,
    createdUtc TEXT NOT NULL
);";
        await command.ExecuteNonQueryAsync();
    }

    public async Task<bool> ExistsAsync(string fingerprint)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "SELECT 1 FROM fingerprints WHERE hash = $hash LIMIT 1";
        command.Parameters.AddWithValue("$hash", fingerprint);
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }

    public async Task AddAsync(string fingerprint, string date)
    {
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = "INSERT OR IGNORE INTO fingerprints (hash, date, createdUtc) VALUES ($hash, $date, $createdUtc)";
        command.Parameters.AddWithValue("$hash", fingerprint);
        command.Parameters.AddWithValue("$date", date);
        command.Parameters.AddWithValue("$createdUtc", DateTimeOffset.UtcNow.ToString("o"));
        await command.ExecuteNonQueryAsync();
    }
}
