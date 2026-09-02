using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Parsec.Testcontainers;

/// <summary>
/// The immutable configuration of a Parsec container.
/// </summary>
/// <remarks>
/// Each property is optional. A property that is <c>null</c> keeps the value that the image
/// configuration already has. The builder makes a new instance for each change, and merges the
/// new instance into the old one. In a merge, a value that is not <c>null</c> replaces the
/// earlier value. The image reference comes from the <c>Image</c> property of the base class,
/// so you can start a different build of the service.
/// </remarks>
public sealed class ParsecConfiguration : ContainerConfiguration
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecConfiguration"/> class.
    /// </summary>
    /// <param name="authType">The authenticator of the service.</param>
    /// <param name="logLevel">The log level of the service.</param>
    /// <param name="socketDirectory">The directory in the container that holds the socket.</param>
    public ParsecConfiguration(
        ParsecAuthType? authType = null,
        ParsecLogLevel? logLevel = null,
        string? socketDirectory = null)
    {
        AuthType = authType;
        LogLevel = logLevel;
        SocketDirectory = socketDirectory;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecConfiguration"/> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public ParsecConfiguration(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecConfiguration"/> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public ParsecConfiguration(IContainerConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecConfiguration"/> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    public ParsecConfiguration(ParsecConfiguration resourceConfiguration)
        : this(new ParsecConfiguration(), resourceConfiguration)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecConfiguration"/> class.
    /// </summary>
    /// <param name="oldValue">The old Docker resource configuration.</param>
    /// <param name="newValue">The new Docker resource configuration.</param>
    public ParsecConfiguration(ParsecConfiguration oldValue, ParsecConfiguration newValue)
        : base(oldValue, newValue)
    {
        // The null-conditional operators keep CA1062 satisfied. The base constructor reads both
        // arguments, so a null argument cannot reach this line.
        AuthType = BuildConfiguration.Combine(oldValue?.AuthType, newValue?.AuthType);
        LogLevel = BuildConfiguration.Combine(oldValue?.LogLevel, newValue?.LogLevel);
        SocketDirectory = BuildConfiguration.Combine(oldValue?.SocketDirectory, newValue?.SocketDirectory);
    }

    /// <summary>
    /// Gets the authenticator of the service, or <c>null</c> to keep the authenticator of the
    /// image. The image uses <see cref="ParsecImage.DefaultAuthType"/>.
    /// </summary>
    public ParsecAuthType? AuthType { get; }

    /// <summary>
    /// Gets the log level of the service, or <c>null</c> to keep the log level of the image. The
    /// image uses <see cref="ParsecImage.DefaultLogLevel"/>.
    /// </summary>
    public ParsecLogLevel? LogLevel { get; }

    /// <summary>
    /// Gets the directory in the container that holds the socket, or <c>null</c> to keep the
    /// directory of the image. The image uses <see cref="ParsecImage.SocketDirectory"/>.
    /// </summary>
    public string? SocketDirectory { get; }
}
