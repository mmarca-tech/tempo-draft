using System;
using System.Collections.Generic;

namespace TempoDraft.Core;

public static class AppSettingsDefaults
{
    public static AppSettings Create()
    {
        return new AppSettings
        {
            TimeZoneId = "Europe/Tallinn",
            WorkdayStart = "09:00",
            WorkdayEnd = "18:00",
            RoundingMinutes = 5,
            MinBlockMinutes = 5,
            MergeGapMinutes = 5,
            IgnoreCalendarSubjects = new List<string> { "Lunch", "Focus time" },
            GitRepoPaths = new List<string>(),
            OutputFolder = "export",
            DefaultCodingBlockMinutes = 25,
            CommitClusterGapMinutes = 30,
            GitRepoBasePath = null,
            UploadTimeZoneId = "Europe/Tallinn",
            MeetingRoundUpToMinutes = 60,
            Jira = new JiraSettings
            {
                BaseUrl = "",
                Email = "",
                ApiToken = ""
            }
        };
    }
}

public static class MappingSettingsDefaults
{
    public static MappingSettings Create()
    {
        return new MappingSettings
        {
            KeywordToIssueKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            SubjectContainsToIssueKey = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
            DefaultIssueKeyByRepo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        };
    }
}
