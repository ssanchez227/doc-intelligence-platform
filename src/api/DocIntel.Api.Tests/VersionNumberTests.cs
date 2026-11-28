using DocIntel.Api.Content;

namespace DocIntel.Api.Tests;

// Drafts take the next minor number after the highest version; publishing takes
// the next major (decision log).
public class VersionNumberTests
{
    [Fact]
    public void A_new_document_starts_at_0_1() => Assert.Equal((0, 1), VersionNumbers.First);

    [Theory]
    [InlineData(new[] { 0, 1 }, 0, 2)]
    [InlineData(new[] { 0, 1, 0, 2, 0, 3 }, 0, 4)]
    [InlineData(new[] { 0, 1, 0, 2, 1, 0 }, 1, 1)]
    [InlineData(new[] { 1, 0, 1, 1, 1, 2 }, 1, 3)]
    [InlineData(new[] { 0, 9, 0, 10 }, 0, 11)]
    [InlineData(new[] { 3, 0, 1, 0, 2, 0 }, 3, 1)]
    public void Next_draft_is_the_next_minor_after_the_highest(int[] existing, int major, int minor) =>
        Assert.Equal((major, minor), VersionNumbers.NextMinor(Pairs(existing)));

    [Theory]
    [InlineData(new[] { 0, 1 }, 1)]
    [InlineData(new[] { 0, 1, 0, 2, 0, 3 }, 1)]
    [InlineData(new[] { 1, 0, 1, 1, 1, 2 }, 2)]
    [InlineData(new[] { 2, 0, 3, 0, 3, 4 }, 4)]
    public void Publishing_takes_the_next_major(int[] existing, int major) =>
        Assert.Equal((major, 0), VersionNumbers.NextMajor(Pairs(existing)));

    private static IEnumerable<(int, int)> Pairs(int[] flat) =>
        flat.Chunk(2).Select(p => (p[0], p[1]));
}
