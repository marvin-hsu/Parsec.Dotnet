namespace Parsec.Testcontainers.Tests;

// The constants of the image are literals. A test that repeats such a literal can never fail for
// a real reason, so only the two decisions of this type have a test: the shape of the digest, and
// the reference that pins the digest.
public sealed class ParsecImageTests
{
    [Fact]
    public void ImageDigestHasTheSha256Shape()
    {
        Assert.StartsWith("sha256:", ParsecImage.Digest, StringComparison.Ordinal);

        var hex = ParsecImage.Digest["sha256:".Length..];

        Assert.Equal(64, hex.Length);
        Assert.All(hex, c => Assert.True(char.IsAsciiDigit(c) || c is >= 'a' and <= 'f'));
    }

    [Fact]
    public void ReferencePinsTheDigestAndNotTheTag()
    {
        Assert.Equal($"{ParsecImage.Repository}@{ParsecImage.Digest}", ParsecImage.Reference);
        Assert.DoesNotContain($":{ParsecImage.Tag}", ParsecImage.Reference, StringComparison.Ordinal);
    }
}
