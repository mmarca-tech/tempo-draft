using System;
using System.Linq;

namespace TempoDraft.Core;

public sealed class WorkMapper
{
    private readonly MappingSettings _settings;
    private readonly IssueKeyExtractor _extractor;

    public WorkMapper(MappingSettings settings, IssueKeyExtractor extractor)
    {
        _settings = settings;
        _extractor = extractor;
    }

    public string ResolveIssueKey(WorkEvent workEvent)
    {
        var firstMatch = _extractor.Extract(workEvent.RawText).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(firstMatch))
        {
            return firstMatch;
        }

        foreach (var kvp in _settings.SubjectContainsToIssueKey)
        {
            if (ContainsInsensitive(workEvent.Title, kvp.Key) || ContainsInsensitive(workEvent.RawText, kvp.Key))
            {
                return kvp.Value;
            }
        }

        foreach (var kvp in _settings.KeywordToIssueKey)
        {
            if (ContainsInsensitive(workEvent.Title, kvp.Key) || ContainsInsensitive(workEvent.RawText, kvp.Key))
            {
                return kvp.Value;
            }
        }

        if (workEvent.Source.Equals("git", StringComparison.OrdinalIgnoreCase))
        {
            var repoName = ExtractRepoName(workEvent.Title);
            if (repoName != null && _settings.DefaultIssueKeyByRepo.TryGetValue(repoName, out var issueKey))
            {
                return issueKey;
            }
        }

        return "UNASSIGNED";
    }

    public string BuildDescription(WorkEvent workEvent)
    {
        if (workEvent.Source.Equals("outlook", StringComparison.OrdinalIgnoreCase))
        {
            return $"Meeting: {workEvent.Title}";
        }

        if (workEvent.Source.Equals("git", StringComparison.OrdinalIgnoreCase))
        {
            var repoName = ExtractRepoName(workEvent.Title) ?? "repo";
            var shortShas = GitEvidenceHelper.ShortShaList(workEvent.Evidence);
            return $"Coding: {repoName} (commits: {shortShas})";
        }

        return workEvent.Title;
    }

    private static bool ContainsInsensitive(string? text, string value)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        return text.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static string? ExtractRepoName(string title)
    {
        var start = title.IndexOf('(');
        var end = title.IndexOf(')');
        if (start >= 0 && end > start)
        {
            return title.Substring(start + 1, end - start - 1).Trim();
        }

        return null;
    }
}

internal static class GitEvidenceHelper
{
    public static string ShortShaList(string evidence)
    {
        if (string.IsNullOrWhiteSpace(evidence))
        {
            return "";
        }

        var parts = evidence.Split(':', 3, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 3)
        {
            return "";
        }

        var shas = parts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var shortShas = shas.Select(s => s.Length > 7 ? s[..7] : s);
        return string.Join(",", shortShas);
    }
}
