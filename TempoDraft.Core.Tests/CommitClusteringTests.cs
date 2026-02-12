using System;
using System.Collections.Generic;
using TempoDraft.Sources.Git;
using Xunit;

namespace TempoDraft.Core.Tests;

public sealed class CommitClusteringTests
{
    [Fact]
    public void ClusterCommits_GroupsByGap()
    {
        var commits = new List<GitCollector.GitCommit>
        {
            new() { Sha = "a", Timestamp = new DateTimeOffset(2026, 2, 10, 9, 0, 0, TimeSpan.Zero), Message = "one" },
            new() { Sha = "b", Timestamp = new DateTimeOffset(2026, 2, 10, 9, 20, 0, TimeSpan.Zero), Message = "two" },
            new() { Sha = "c", Timestamp = new DateTimeOffset(2026, 2, 10, 10, 0, 0, TimeSpan.Zero), Message = "three" }
        };

        var clusters = GitCollector.ClusterCommits(commits, TimeSpan.FromMinutes(30));

        Assert.Equal(2, clusters.Count);
        Assert.Equal(2, clusters[0].Count);
        Assert.Single(clusters[1]);
    }
}
