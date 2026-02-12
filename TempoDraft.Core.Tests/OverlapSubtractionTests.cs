using System;
using System.Collections.Generic;
using TempoDraft.Core;
using Xunit;

namespace TempoDraft.Core.Tests;

public sealed class OverlapSubtractionTests
{
    [Fact]
    public void SubtractIntervals_RemovesMeetingOverlap()
    {
        var block = new TimeBlock
        {
            Start = new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 2, 10, 11, 0, 0, TimeSpan.Zero),
            IssueKey = "RM-1",
            Source = "git",
            Evidence = "git",
            Title = "Coding"
        };

        var meeting = new TimeBlock
        {
            Start = new DateTimeOffset(2026, 2, 10, 9, 30, 0, TimeSpan.Zero),
            End = new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero),
            IssueKey = "RM-2",
            Source = "outlook",
            Evidence = "outlook",
            Title = "Meet"
        };

        var fragments = WorkAggregator.SubtractIntervals(block, new List<TimeBlock> { meeting });

        Assert.Equal(2, fragments.Count);
        Assert.Equal(block.Start, fragments[0].Start);
        Assert.Equal(meeting.Start, fragments[0].End);
        Assert.Equal(meeting.End, fragments[1].Start);
        Assert.Equal(block.End, fragments[1].End);
    }
}
