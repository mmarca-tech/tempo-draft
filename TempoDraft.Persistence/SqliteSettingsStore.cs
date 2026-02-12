using Microsoft.Data.Sqlite;
using TempoDraft.Core;

namespace TempoDraft.Persistence;

public sealed class SqliteSettingsStore
{
    private readonly string _dbPath;

    public SqliteSettingsStore(string dbPath)
    {
        _dbPath = dbPath;
    }

    public async Task EnsureCreatedAsync()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_dbPath) ?? ".");
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        if (await TableHasColumnAsync(connection, "app_settings", "json"))
        {
            var drop = connection.CreateCommand();
            drop.CommandText = "DROP TABLE IF EXISTS app_settings; DROP TABLE IF EXISTS mappings;";
            await drop.ExecuteNonQueryAsync();

            await CreateSchemaAsync(connection);

            await SaveAppSettingsAsync(AppSettingsDefaults.Create());
            await SaveMappingsAsync(MappingSettingsDefaults.Create());

            return;
        }

        await CreateSchemaAsync(connection);
    }

    private static async Task CreateSchemaAsync(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS app_settings (
    id INTEGER PRIMARY KEY CHECK (id = 1),
    timeZoneId TEXT NOT NULL,
    workdayStart TEXT NOT NULL,
    workdayEnd TEXT NOT NULL,
    roundingMinutes INTEGER NOT NULL,
    minBlockMinutes INTEGER NOT NULL,
    mergeGapMinutes INTEGER NOT NULL,
    outputFolder TEXT NOT NULL,
    defaultCodingBlockMinutes INTEGER NOT NULL,
    commitClusterGapMinutes INTEGER NOT NULL,
    gitRepoBasePath TEXT NULL,
    uploadTimeZoneId TEXT NULL,
    meetingRoundUpToMinutes INTEGER NULL,
    jiraBaseUrl TEXT NOT NULL,
    jiraEmail TEXT NOT NULL,
    jiraApiToken TEXT NOT NULL,
    updatedUtc TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS app_git_repo_paths (
    path TEXT PRIMARY KEY
);
CREATE TABLE IF NOT EXISTS app_ignore_subjects (
    subject TEXT PRIMARY KEY
);
CREATE TABLE IF NOT EXISTS mapping_keyword (
    keyword TEXT PRIMARY KEY,
    issueKey TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS mapping_subject_contains (
    subject TEXT PRIMARY KEY,
    issueKey TEXT NOT NULL
);
CREATE TABLE IF NOT EXISTS mapping_repo_default (
    repo TEXT PRIMARY KEY,
    issueKey TEXT NOT NULL
);";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<bool> TableHasColumnAsync(SqliteConnection connection, string table, string column)
    {
        var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({table});";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var name = reader.GetString(1);
            if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }


    public async Task SaveAppSettingsAsync(AppSettings settings)
    {
        await EnsureCreatedAsync();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = @"
INSERT OR REPLACE INTO app_settings (
    id,
    timeZoneId,
    workdayStart,
    workdayEnd,
    roundingMinutes,
    minBlockMinutes,
    mergeGapMinutes,
    outputFolder,
    defaultCodingBlockMinutes,
    commitClusterGapMinutes,
    gitRepoBasePath,
    uploadTimeZoneId,
    meetingRoundUpToMinutes,
    jiraBaseUrl,
    jiraEmail,
    jiraApiToken,
    updatedUtc
) VALUES (
    1,
    $timeZoneId,
    $workdayStart,
    $workdayEnd,
    $roundingMinutes,
    $minBlockMinutes,
    $mergeGapMinutes,
    $outputFolder,
    $defaultCodingBlockMinutes,
    $commitClusterGapMinutes,
    $gitRepoBasePath,
    $uploadTimeZoneId,
    $meetingRoundUpToMinutes,
    $jiraBaseUrl,
    $jiraEmail,
    $jiraApiToken,
    $updatedUtc
);";
        command.Parameters.AddWithValue("$timeZoneId", settings.TimeZoneId);
        command.Parameters.AddWithValue("$workdayStart", settings.WorkdayStart);
        command.Parameters.AddWithValue("$workdayEnd", settings.WorkdayEnd);
        command.Parameters.AddWithValue("$roundingMinutes", settings.RoundingMinutes);
        command.Parameters.AddWithValue("$minBlockMinutes", settings.MinBlockMinutes);
        command.Parameters.AddWithValue("$mergeGapMinutes", settings.MergeGapMinutes);
        command.Parameters.AddWithValue("$outputFolder", settings.OutputFolder);
        command.Parameters.AddWithValue("$defaultCodingBlockMinutes", settings.DefaultCodingBlockMinutes);
        command.Parameters.AddWithValue("$commitClusterGapMinutes", settings.CommitClusterGapMinutes);
        command.Parameters.AddWithValue("$gitRepoBasePath", (object?)settings.GitRepoBasePath ?? DBNull.Value);
        command.Parameters.AddWithValue("$uploadTimeZoneId", (object?)settings.UploadTimeZoneId ?? DBNull.Value);
        command.Parameters.AddWithValue("$meetingRoundUpToMinutes", (object?)settings.MeetingRoundUpToMinutes ?? DBNull.Value);
        command.Parameters.AddWithValue("$jiraBaseUrl", settings.Jira?.BaseUrl ?? "");
        command.Parameters.AddWithValue("$jiraEmail", settings.Jira?.Email ?? "");
        command.Parameters.AddWithValue("$jiraApiToken", settings.Jira?.ApiToken ?? "");
        command.Parameters.AddWithValue("$updatedUtc", DateTimeOffset.UtcNow.ToString("o"));
        await command.ExecuteNonQueryAsync();

        var clearRepos = connection.CreateCommand();
        clearRepos.Transaction = transaction;
        clearRepos.CommandText = "DELETE FROM app_git_repo_paths";
        await clearRepos.ExecuteNonQueryAsync();

        foreach (var path in settings.GitRepoPaths ?? new List<string>())
        {
            var insertRepo = connection.CreateCommand();
            insertRepo.Transaction = transaction;
            insertRepo.CommandText = "INSERT INTO app_git_repo_paths (path) VALUES ($path)";
            insertRepo.Parameters.AddWithValue("$path", path);
            await insertRepo.ExecuteNonQueryAsync();
        }

        var clearSubjects = connection.CreateCommand();
        clearSubjects.Transaction = transaction;
        clearSubjects.CommandText = "DELETE FROM app_ignore_subjects";
        await clearSubjects.ExecuteNonQueryAsync();

        foreach (var subject in settings.IgnoreCalendarSubjects ?? new List<string>())
        {
            var insertSubject = connection.CreateCommand();
            insertSubject.Transaction = transaction;
            insertSubject.CommandText = "INSERT INTO app_ignore_subjects (subject) VALUES ($subject)";
            insertSubject.Parameters.AddWithValue("$subject", subject);
            await insertSubject.ExecuteNonQueryAsync();
        }

        transaction.Commit();
    }

    public async Task SaveMappingsAsync(MappingSettings mappings)
    {
        await EnsureCreatedAsync();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();
        await using var transaction = connection.BeginTransaction();

        await ClearAndInsertMappingsAsync(connection, transaction, "mapping_keyword", "keyword", mappings.KeywordToIssueKey);
        await ClearAndInsertMappingsAsync(connection, transaction, "mapping_subject_contains", "subject", mappings.SubjectContainsToIssueKey);
        await ClearAndInsertMappingsAsync(connection, transaction, "mapping_repo_default", "repo", mappings.DefaultIssueKeyByRepo);

        transaction.Commit();
    }

    public async Task<AppSettings> LoadAppSettingsAsync()
    {
        await EnsureCreatedAsync();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT timeZoneId, workdayStart, workdayEnd, roundingMinutes, minBlockMinutes, mergeGapMinutes, outputFolder, defaultCodingBlockMinutes, commitClusterGapMinutes, gitRepoBasePath, uploadTimeZoneId, meetingRoundUpToMinutes, jiraBaseUrl, jiraEmail, jiraApiToken FROM app_settings WHERE id = 1";
        await using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            var defaults = AppSettingsDefaults.Create();
            await SaveAppSettingsAsync(defaults);
            return defaults;
        }

        var gitRepos = await LoadListAsync(connection, "app_git_repo_paths", "path");
        var ignoreSubjects = await LoadListAsync(connection, "app_ignore_subjects", "subject");

        return new AppSettings
        {
            TimeZoneId = reader.GetString(0),
            WorkdayStart = reader.GetString(1),
            WorkdayEnd = reader.GetString(2),
            RoundingMinutes = reader.GetInt32(3),
            MinBlockMinutes = reader.GetInt32(4),
            MergeGapMinutes = reader.GetInt32(5),
            OutputFolder = reader.GetString(6),
            DefaultCodingBlockMinutes = reader.GetInt32(7),
            CommitClusterGapMinutes = reader.GetInt32(8),
            GitRepoBasePath = reader.IsDBNull(9) ? null : reader.GetString(9),
            UploadTimeZoneId = reader.IsDBNull(10) ? null : reader.GetString(10),
            MeetingRoundUpToMinutes = reader.IsDBNull(11) ? null : reader.GetInt32(11),
            GitRepoPaths = gitRepos,
            IgnoreCalendarSubjects = ignoreSubjects,
            Jira = new JiraSettings
            {
                BaseUrl = reader.GetString(12),
                Email = reader.GetString(13),
                ApiToken = reader.GetString(14)
            }
        };
    }

    public async Task<MappingSettings> LoadMappingsAsync()
    {
        await EnsureCreatedAsync();
        await using var connection = new SqliteConnection($"Data Source={_dbPath}");
        await connection.OpenAsync();

        var keyword = await LoadMapAsync(connection, "mapping_keyword", "keyword");
        var subject = await LoadMapAsync(connection, "mapping_subject_contains", "subject");
        var repo = await LoadMapAsync(connection, "mapping_repo_default", "repo");

        if (keyword.Count == 0 && subject.Count == 0 && repo.Count == 0)
        {
            var defaults = MappingSettingsDefaults.Create();
            await SaveMappingsAsync(defaults);
            return defaults;
        }

        return new MappingSettings
        {
            KeywordToIssueKey = new Dictionary<string, string>(keyword, StringComparer.OrdinalIgnoreCase),
            SubjectContainsToIssueKey = new Dictionary<string, string>(subject, StringComparer.OrdinalIgnoreCase),
            DefaultIssueKeyByRepo = new Dictionary<string, string>(repo, StringComparer.OrdinalIgnoreCase)
        };
    }

    private static async Task ClearAndInsertMappingsAsync(SqliteConnection connection, SqliteTransaction transaction, string table, string keyColumn, Dictionary<string, string> values)
    {
        var clear = connection.CreateCommand();
        clear.Transaction = transaction;
        clear.CommandText = $"DELETE FROM {table}";
        await clear.ExecuteNonQueryAsync();

        foreach (var kvp in values)
        {
            var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText = $"INSERT INTO {table} ({keyColumn}, issueKey) VALUES ($key, $issueKey)";
            insert.Parameters.AddWithValue("$key", kvp.Key);
            insert.Parameters.AddWithValue("$issueKey", kvp.Value);
            await insert.ExecuteNonQueryAsync();
        }
    }

    private static async Task<List<string>> LoadListAsync(SqliteConnection connection, string table, string column)
    {
        var list = new List<string>();
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT {column} FROM {table}";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    private static async Task<Dictionary<string, string>> LoadMapAsync(SqliteConnection connection, string table, string keyColumn)
    {
        var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var command = connection.CreateCommand();
        command.CommandText = $"SELECT {keyColumn}, issueKey FROM {table}";
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            dict[reader.GetString(0)] = reader.GetString(1);
        }
        return dict;
    }
}
