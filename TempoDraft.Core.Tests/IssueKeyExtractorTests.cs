using TempoDraft.Core;
using Xunit;

namespace TempoDraft.Core.Tests;

public sealed class IssueKeyExtractorTests
{
    [Fact]
    public void Extract_ReturnsFirstAndDistinctKeys()
    {
        var extractor = new IssueKeyExtractor();
        var text = "Worked on RM-123 and AA9-456 then RM-123 again.";

        var keys = extractor.Extract(text);

        Assert.Equal(2, keys.Count);
        Assert.Equal("RM-123", keys[0]);
        Assert.Equal("AA9-456", keys[1]);
    }

    [Fact]
    public void Extract_EmptyText_ReturnsEmpty()
    {
        var extractor = new IssueKeyExtractor();

        var keys = extractor.Extract("");

        Assert.Empty(keys);
    }
}
