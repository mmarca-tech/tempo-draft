using System;
using System.Collections.Generic;

namespace TempoDraft.Core;

public sealed class WorkEvent
{
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }
    public int Minutes
    {
        get
        {
            var minutes = (int)Math.Round((End - Start).TotalMinutes);
            return Math.Max(0, minutes);
        }
    }
    public required string Source { get; init; }
    public required string Title { get; init; }
    public required string RawText { get; init; }
    public required string Evidence { get; init; }
}

public sealed class WorkLogDraft
{
    public required DateOnly Date { get; init; }
    public required string IssueKey { get; init; }
    public required int Minutes { get; init; }
    public required string Description { get; init; }
    public required string Source { get; init; }
    public required string Evidence { get; init; }
    public DateTimeOffset? Start { get; init; }
    public DateTimeOffset? End { get; init; }
}

internal sealed class TimeBlock
{
    public required DateTimeOffset Start { get; set; }
    public required DateTimeOffset End { get; set; }
    public required string IssueKey { get; set; }
    public required string Source { get; set; }
    public required string Evidence { get; set; }
    public required string Title { get; set; }

    public int Minutes
    {
        get
        {
            var minutes = (int)Math.Round((End - Start).TotalMinutes);
            return Math.Max(0, minutes);
        }
    }
}
