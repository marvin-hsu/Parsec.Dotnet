namespace Parsec.Testcontainers.Tests;

public sealed class SmokeTests
{
    [Fact]
    public void ImageRepositoryIsTheGhcrPackage() =>
        Assert.Equal("ghcr.io/marvin-hsu/parsec-testcontainers", ParsecImage.Repository);

    [Fact]
    public void ImageTagIsThePinnedParsecVersion() =>
        Assert.Equal("1.5.0", ParsecImage.Tag);

    [Fact]
    public void ImageDigestIsThePinnedDigest() =>
        Assert.Equal(
            "sha256:daf499328f06d2f2389d49fe692b7b9e48acabd178cc7a4d2e442c9bef4a63d3",
            ParsecImage.Digest);

    [Fact]
    public void ImageDigestHasTheSha256Shape()
    {
        Assert.StartsWith("sha256:", ParsecImage.Digest, StringComparison.Ordinal);

        var hex = ParsecImage.Digest["sha256:".Length..];

        Assert.Equal(64, hex.Length);
        Assert.All(hex, c => Assert.True(char.IsAsciiDigit(c) || (c is >= 'a' and <= 'f')));
    }
}
