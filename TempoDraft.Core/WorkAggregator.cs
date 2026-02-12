using System;
using System.Collections.Generic;
using System.Linq;

namespace TempoDraft.Core;

public sealed class WorkAggregator
{
    private readonly AppSettings _settings;
    private readonly WorkMapper _mapper;

    public WorkAggregator(AppSettings settings, WorkMapper mapper)
    {
        _settings = settings;
        _mapper = mapper;
    }

    public IReadOnlyList<WorkLogDraft> BuildDrafts(DateOnly date, IReadOnlyList<WorkEvent> events)
    {
        var tz = TimeZoneInfo.FindSystemTimeZoneById(_settings.TimeZoneId);
        var dayStart = ToDateTimeOffset(date, _settings.WorkdayStart, tz);
        var dayEnd = ToDateTimeOffset(date, _settings.WorkdayEnd, tz);

        var timeBlocks = events
            .Select(e => new TimeBlock
            {
                Start = e.Start,
                End = e.End,
                IssueKey = _mapper.ResolveIssueKey(e),
                Source = e.Source,
                Evidence = e.Evidence,
                Title = e.Title
            })
            .Select(b => ClampToWorkday(b, dayStart, dayEnd))
            .Where(b => b != null)
            .Select(b => b!)
            .ToList();

        var meetings = timeBlocks.Where(b => b.Source.Equals("outlook", StringComparison.OrdinalIgnoreCase)).ToList();
        var coding = timeBlocks.Where(b => b.Source.Equals("git", StringComparison.OrdinalIgnoreCase)).ToList();

        var codingAfterSubtract = new List<TimeBlock>();
        foreach (var block in coding)
        {
            var fragments = SubtractIntervals(block, meetings);
            codingAfterSubtract.AddRange(fragments.Where(f => f.Minutes >= _settings.MinBlockMinutes));
        }

        var merged = MergeAdjacent(meetings.Concat(codingAfterSubtract).OrderBy(b => b.Start).ToList());

        var drafts = merged
            .Select(b => new WorkLogDraft
            {
                Date = date,
                IssueKey = b.IssueKey,
                Minutes = b.Minutes,
                Description = _mapper.BuildDescription(new WorkEvent
                {
                    Start = b.Start,
                    End = b.End,
                    Source = b.Source,
                    Title = b.Title,
                    RawText = b.Title,
                    Evidence = b.Evidence
                }),
                Source = b.Source,
                Evidence = b.Evidence,
                Start = b.Start,
                End = b.End
            })
            .Select(RoundDraft)
            .Where(d => d != null)
            .Select(d => d!)
            .ToList();

        return drafts;
    }

    private WorkLogDraft? RoundDraft(WorkLogDraft draft)
    {
        var minutes = draft.Minutes;
        if (draft.Source.Equals("outlook", StringComparison.OrdinalIgnoreCase) && _settings.MeetingRoundUpToMinutes.HasValue)
        {
            var target = _settings.MeetingRoundUpToMinutes.Value;
            if (minutes < target && minutes >= target - 15)
            {
                minutes = target;
            }
        }

        var rounded = RoundMinutes(minutes, _settings.RoundingMinutes);
        if (rounded < _settings.MinBlockMinutes)
        {
            return null;
        }

        return new WorkLogDraft
        {
            Date = draft.Date,
            IssueKey = draft.IssueKey,
            Minutes = rounded,
            Description = draft.Description,
            Source = draft.Source,
            Evidence = draft.Evidence,
            Start = draft.Start,
            End = draft.End
        };
    }

    private static int RoundMinutes(int minutes, int rounding)
    {
        if (rounding <= 1)
        {
            return minutes;
        }

        var rounded = (int)Math.Round(minutes / (double)rounding, MidpointRounding.AwayFromZero) * rounding;
        return Math.Max(0, rounded);
    }

    private static TimeBlock? ClampToWorkday(TimeBlock block, DateTimeOffset dayStart, DateTimeOffset dayEnd)
    {
        var start = block.Start < dayStart ? dayStart : block.Start;
        var end = block.End > dayEnd ? dayEnd : block.End;
        if (end <= start)
        {
            return null;
        }

        block.Start = start;
        block.End = end;
        return block;
    }

    internal static List<TimeBlock> SubtractIntervals(TimeBlock block, IEnumerable<TimeBlock> subtractors)
    {
        var fragments = new List<TimeBlock> { block };
        foreach (var meeting in subtractors.OrderBy(s => s.Start))
        {
            var next = new List<TimeBlock>();
            foreach (var frag in fragments)
            {
                if (meeting.End <= frag.Start || meeting.Start >= frag.End)
                {
                    next.Add(frag);
                    continue;
                }

                if (meeting.Start > frag.Start)
                {
                    next.Add(new TimeBlock
                    {
                        Start = frag.Start,
                        End = meeting.Start,
                        IssueKey = frag.IssueKey,
                        Source = frag.Source,
                        Evidence = frag.Evidence,
                        Title = frag.Title
                    });
                }

                if (meeting.End < frag.End)
                {
                    next.Add(new TimeBlock
                    {
                        Start = meeting.End,
                        End = frag.End,
                        IssueKey = frag.IssueKey,
                        Source = frag.Source,
                        Evidence = frag.Evidence,
                        Title = frag.Title
                    });
                }
            }

            fragments = next;
            if (fragments.Count == 0)
            {
                break;
            }
        }

        return fragments;
    }

    private List<TimeBlock> MergeAdjacent(List<TimeBlock> blocks)
    {
        if (blocks.Count == 0)
        {
            return blocks;
        }

        var merged = new List<TimeBlock>();
        var gap = TimeSpan.FromMinutes(_settings.MergeGapMinutes);

        TimeBlock current = blocks[0];
        for (int i = 1; i < blocks.Count; i++)
        {
            var next = blocks[i];
            if (next.IssueKey.Equals(current.IssueKey, StringComparison.OrdinalIgnoreCase)
                && next.Source.Equals(current.Source, StringComparison.OrdinalIgnoreCase)
                && next.Start - current.End <= gap)
            {
                current.End = next.End;
                if (!current.Title.Equals(next.Title, StringComparison.OrdinalIgnoreCase))
                {
                    current.Title = $"{current.Title} / {next.Title}";
                }

                current.Evidence = MergeEvidence(current.Source, current.Evidence, next.Evidence);

                continue;
            }

            merged.Add(current);
            current = next;
        }

        merged.Add(current);
        return merged;
    }

    private static string MergeEvidence(string source, string left, string right)
    {
        if (left.Equals(right, StringComparison.OrdinalIgnoreCase))
        {
            return left;
        }

        if (source.Equals("git", StringComparison.OrdinalIgnoreCase))
        {
            var leftParts = left.Split(':', 3);
            var rightParts = right.Split(':', 3);
            if (leftParts.Length == 3 && rightParts.Length == 3 && leftParts[0] == "git" && rightParts[0] == "git")
            {
                if (leftParts[1].Equals(rightParts[1], StringComparison.OrdinalIgnoreCase))
                {
                    var shas = leftParts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                        .Concat(rightParts[2].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        .Distinct(StringComparer.OrdinalIgnoreCase);
                    return $"git:{leftParts[1]}:{string.Join(',', shas)}";
                }
            }
        }

        return $"{left};{right}";
    }

    private static DateTimeOffset ToDateTimeOffset(DateOnly date, string time, TimeZoneInfo tz)
    {
        var parts = time.Split(':', StringSplitOptions.RemoveEmptyEntries);
        var hour = int.Parse(parts[0]);
        var minute = parts.Length > 1 ? int.Parse(parts[1]) : 0;
        var local = new DateTime(date.Year, date.Month, date.Day, hour, minute, 0, DateTimeKind.Unspecified);
        var offset = tz.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }
}
