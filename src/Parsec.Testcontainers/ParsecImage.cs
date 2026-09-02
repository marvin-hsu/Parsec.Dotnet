namespace Parsec.Testcontainers;

/// <summary>
/// Holds the container image facts for the Parsec service that this module starts.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Digest"/> pins the exact image bytes. A digest makes each test run use the same
/// image. <see cref="Tag"/> is the matching version of the Parsec service. Keep the tag for
/// messages and for users who prefer a tag.
/// </para>
/// <para>
/// The <c>Default</c> members give the values that the image configuration already contains.
/// The builder compares your settings to these values. It writes a new service configuration
/// file only when a value is different.
/// </para>
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

    /// <summary>
    /// The version of the Parsec service in the image.
    /// </summary>
    public const string ParsecVersion = Tag;

    /// <summary>
    /// The default image reference. It joins <see cref="Repository"/> and <see cref="Digest"/>,
    /// because a digest identifies one image and a tag can move to a new image.
    /// </summary>
    public const string Reference = Repository + "@" + Digest;

    /// <summary>
    /// The directory in the container that holds the socket of the service.
    /// </summary>
    public const string SocketDirectory = "/run/parsec";

    /// <summary>
    /// The path of the service configuration file in the container. The entry point of the image
    /// reads this file.
    /// </summary>
    public const string ConfigFilePath = "/etc/parsec/config.toml";

    /// <summary>
    /// The name of the socket file in <see cref="SocketDirectory"/>.
    /// </summary>
    public const string SocketFileName = "parsec.sock";

    /// <summary>
    /// The user that the image runs the service as. The image does not need root.
    /// </summary>
    public const string DefaultUser = "parsec";

    /// <summary>
    /// The authenticator in the image configuration.
    /// </summary>
    public const ParsecAuthType DefaultAuthType = ParsecAuthType.Direct;

    /// <summary>
    /// The log level in the image configuration.
    /// </summary>
    public const ParsecLogLevel DefaultLogLevel = ParsecLogLevel.Info;
}
