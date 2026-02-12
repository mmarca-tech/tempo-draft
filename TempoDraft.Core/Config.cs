using System;
using System.Collections.Generic;

namespace TempoDraft.Core;

public sealed class AppSettings
{
    public required string TimeZoneId { get; init; }
    public required string WorkdayStart { get; init; }
    public required string WorkdayEnd { get; init; }
    public required int RoundingMinutes { get; init; }
    public required int MinBlockMinutes { get; init; }
    public required int MergeGapMinutes { get; init; }
    public required List<string> IgnoreCalendarSubjects { get; init; } = new();
    public required List<string> GitRepoPaths { get; init; } = new();
    public required string OutputFolder { get; init; }
    public int DefaultCodingBlockMinutes { get; init; } = 25;
    public int CommitClusterGapMinutes { get; init; } = 30;
    public string? GitRepoBasePath { get; init; }
    public string? UploadTimeZoneId { get; init; }
    public int? MeetingRoundUpToMinutes { get; init; }
    public JiraSettings? Jira { get; init; }
}

public sealed class MappingSettings
{
    public Dictionary<string, string> KeywordToIssueKey { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> SubjectContainsToIssueKey { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> DefaultIssueKeyByRepo { get; init; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class JiraSettings
{
    public string BaseUrl { get; init; } = "";
    public string Email { get; init; } = "";
    public string ApiToken { get; init; } = "";
}
