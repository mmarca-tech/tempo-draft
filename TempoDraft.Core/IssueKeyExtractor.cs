using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace TempoDraft.Core;

public sealed class IssueKeyExtractor
{
    private static readonly Regex IssueKeyRegex = new(@"\b[A-Z][A-Z0-9]+-\d+\b", RegexOptions.Compiled);

    public IReadOnlyList<string> Extract(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return Array.Empty<string>();
        }

        var matches = IssueKeyRegex.Matches(text);
        return matches.Select(m => m.Value).Distinct().ToList();
    }
}
