using System;
using System.Collections.Generic;
using TempoDraft.Core;
using Xunit;

namespace TempoDraft.Core.Tests;

public sealed class RoundingAndMergeTests
{
    [Fact]
    public void BuildDrafts_RoundsMinutes()
    {
        var settings = new AppSettings
        {
            TimeZoneId = "UTC",
            WorkdayStart = "09:00",
            WorkdayEnd = "18:00",
            RoundingMinutes = 5,
            MinBlockMinutes = 5,
            MergeGapMinutes = 5,
            IgnoreCalendarSubjects = new List<string>(),
            GitRepoPaths = new List<string>(),
            OutputFolder = "export",
            DefaultCodingBlockMinutes = 25,
            CommitClusterGapMinutes = 30
        };
        var mapping = new MappingSettings();
        var mapper = new WorkMapper(mapping, new IssueKeyExtractor());
        var aggregator = new WorkAggregator(settings, mapper);

        var events = new List<WorkEvent>
        {
            new()
            {
                Start = new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 2, 10, 9, 12, 0, TimeSpan.Zero),
                Source = "outlook",
                Title = "RM-1 status",
                RawText = "RM-1",
                Evidence = "outlook"
            }
        };

        var drafts = aggregator.BuildDrafts(new DateOnly(2026, 2, 10), events);

        Assert.Single(drafts);
        Assert.Equal(10, drafts[0].Minutes);
    }

    [Fact]
    public void BuildDrafts_MergesAdjacentBlocks()
    {
        var settings = new AppSettings
        {
            TimeZoneId = "UTC",
            WorkdayStart = "09:00",
            WorkdayEnd = "18:00",
            RoundingMinutes = 1,
            MinBlockMinutes = 5,
            MergeGapMinutes = 5,
            IgnoreCalendarSubjects = new List<string>(),
            GitRepoPaths = new List<string>(),
            OutputFolder = "export",
            DefaultCodingBlockMinutes = 25,
            CommitClusterGapMinutes = 30
        };
        var mapping = new MappingSettings();
        var mapper = new WorkMapper(mapping, new IssueKeyExtractor());
        var aggregator = new WorkAggregator(settings, mapper);

        var events = new List<WorkEvent>
        {
            new()
            {
                Start = new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 2, 10, 9, 20, 0, TimeSpan.Zero),
                Source = "outlook",
                Title = "RM-1 status",
                RawText = "RM-1",
                Evidence = "outlook1"
            },
            new()
            {
                Start = new DateTimeOffset(2026, 2, 10, 9, 25, 0, TimeSpan.Zero),
                End = new DateTimeOffset(2026, 2, 10, 9, 35, 0, TimeSpan.Zero),
                Source = "outlook",
                Title = "RM-1 followup",
                RawText = "RM-1",
                Evidence = "outlook2"
            }
        };

        var drafts = aggregator.BuildDrafts(new DateOnly(2026, 2, 10), events);

        Assert.Single(drafts);
        Assert.Equal(35, drafts[0].Minutes);
    }
}
