using Microsoft.Data.Sqlite;
using TempoDraft.Core;

namespace TempoDraft.Persistence;

public sealed class SqliteDraftOverrideStore
{
    private readonly string _dbPath;

    public SqliteDraftOverrideStore(string dbPath)
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
CREATE TABLE IF NOT EXISTS draft_overrides (
    date TEXT NOT NULL,
    source TEXT NOT NULL,
    evidence TEXT NOT NULL,
    issueKey TEXT NOT NULL,
    minutes INTEGER NOT NULL,
    description TEXT NOT NULL,
    start TEXT NULL,
    end TEXT NULL,
    updatedUtc TEXT NOT NULL,
    PRIMARY KEY (date, source, evidence)
);";
        await command.ExecuteNonQueryAsync();
    }

    public async Task SaveOverrideAsync(WorkLogDraft draft)
    {
        await EnsureCreatedAsync();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = @"
INSERT OR REPLACE INTO draft_overrides (
    date,
    source,
    evidence,
    issueKey,
    minutes,
    description,
    start,
    end,
    updatedUtc
) VALUES (
    $date,
    $source,
    $evidence,
    $issueKey,
    $minutes,
    $description,
    $start,
    $end,
    $updatedUtc
);";
        command.Parameters.AddWithValue("$date", draft.Date.ToString("yyyy-MM-dd"));
        command.Parameters.AddWithValue("$source", draft.Source);
        command.Parameters.AddWithValue("$evidence", draft.Evidence);
        command.Parameters.AddWithValue("$issueKey", draft.IssueKey);
        command.Parameters.AddWithValue("$minutes", draft.Minutes);
        command.Parameters.AddWithValue("$description", draft.Description);
        command.Parameters.AddWithValue("$start", draft.Start?.ToString("o"));
        command.Parameters.AddWithValue("$end", draft.End?.ToString("o"));
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("o"));
        await command.ExecuteNonQueryAsync();
    }

    public async Task<Dictionary<string, WorkLogDraft>> LoadOverridesAsync(DateOnly date)
    {
        await EnsureCreatedAsync();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT source, evidence, issueKey, minutes, description, start, end
FROM draft_overrides
WHERE date = $date";
        command.Parameters.AddWithValue("$date", date.ToString("yyyy-MM-dd"));

        var dict = new Dictionary<string, WorkLogDraft>(StringComparer.OrdinalIgnoreCase);
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var source = reader.GetString(0);
            var evidence = reader.GetString(1);
            var key = BuildKey(source, evidence);
            DateTimeOffset? start = reader.IsDBNull(5) ? null : DateTimeOffset.Parse(reader.GetString(5));
            DateTimeOffset? end = reader.IsDBNull(6) ? null : DateTimeOffset.Parse(reader.GetString(6));
            dict[key] = new WorkLogDraft
            {
                Date = date,
                Source = source,
                Evidence = evidence,
                IssueKey = reader.GetString(2),
                Minutes = reader.GetInt32(3),
                Description = reader.GetString(4),
                Start = start,
                End = end
            };
        }

        return dict;
    }

    public static string BuildKey(string source, string evidence)
    {
        return $"{source}|{evidence}";
    }
}
