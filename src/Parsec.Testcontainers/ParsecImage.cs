namespace Parsec.Testcontainers;

/// <summary>
/// Holds the container image facts for the Parsec service that this module starts.
/// </summary>
/// <remarks>
/// <see cref="Digest"/> pins the exact image bytes. A digest makes each test run use the same
/// image. <see cref="Tag"/> is the matching version of the Parsec service. Keep the tag for
/// messages and for users who prefer a tag.
/// </remarks>
public static class ParsecImage
{
    /// <summary>
    /// The image repository.
    /// </summary>
    public const string Repository = "ghcr.io/marvin-hsu/parsec-testcontainers";

    /// <summary>
    /// The image tag. It matches the version of the Parsec service in the image.
    /// </summary>
    public const string Tag = "1.5.0";

    /// <summary>
    /// The manifest digest of the image that <see cref="Tag"/> points to.
    /// </summary>
    public const string Digest = "sha256:daf499328f06d2f2389d49fe692b7b9e48acabd178cc7a4d2e442c9bef4a63d3";
}
