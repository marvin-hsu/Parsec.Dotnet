using System.Text;
using Docker.DotNet.Models;
using DotNet.Testcontainers;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;

namespace Parsec.Testcontainers;

/// <summary>
/// Builds a container that runs the Parsec service.
/// </summary>
/// <remarks>
/// <para>
/// The builder needs no settings. <c>new ParsecBuilder().Build()</c> gives a container that runs
/// the image that <see cref="ParsecImage"/> names, with the settings that the image already has.
/// </para>
/// <para>
/// The builder writes a new service configuration file only when you change the authenticator, the
/// log level, or the socket directory. This keeps the container start as near to the image as
/// possible.
/// </para>
/// </remarks>
public sealed class ParsecBuilder : ContainerBuilder<ParsecBuilder, ParsecContainer, ParsecConfiguration>
{
    /// <summary>
    /// The name of the environment variable that holds the endpoint of the service. A Parsec
    /// client reads this variable to find the socket.
    /// </summary>
    public const string EndpointEnvironmentVariable = "PARSEC_SERVICE_ENDPOINT";

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecBuilder"/> class.
    /// </summary>
    public ParsecBuilder()
        : this(new ParsecConfiguration())
        => DockerResourceConfiguration = Init().DockerResourceConfiguration;

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecBuilder"/> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    private ParsecBuilder(ParsecConfiguration resourceConfiguration)
        : base(resourceConfiguration)
        => DockerResourceConfiguration = resourceConfiguration;

    /// <inheritdoc/>
    protected override ParsecConfiguration DockerResourceConfiguration { get; }

    /// <summary>
    /// Sets the authenticator of the service.
    /// </summary>
    /// <param name="authType">The authenticator of the service.</param>
    /// <returns>A configured instance of <see cref="ParsecBuilder"/>.</returns>
    public ParsecBuilder WithAuthType(ParsecAuthType authType)
        => Merge(DockerResourceConfiguration, new ParsecConfiguration(authType: authType));

    /// <summary>
    /// Sets the log level of the service.
    /// </summary>
    /// <param name="logLevel">The log level of the service.</param>
    /// <returns>A configured instance of <see cref="ParsecBuilder"/>.</returns>
    public ParsecBuilder WithLogLevel(ParsecLogLevel logLevel)
        => Merge(DockerResourceConfiguration, new ParsecConfiguration(logLevel: logLevel));

    /// <summary>
    /// Sets the directory in the container that holds the socket of the service.
    /// </summary>
    /// <param name="socketDirectory">An absolute path in the container.</param>
    /// <returns>A configured instance of <see cref="ParsecBuilder"/>.</returns>
    /// <remarks>
    /// The service must be able to write in the directory. Give a directory that the image
    /// prepares, or mount a directory that the service user can write.
    /// </remarks>
    public ParsecBuilder WithSocketDirectory(string socketDirectory)
        => Merge(DockerResourceConfiguration, new ParsecConfiguration(socketDirectory: socketDirectory));

    /// <inheritdoc/>
    public override ParsecContainer Build()
    {
        Validate();

        // The endpoint variable must agree with the socket path in the configuration file, so the
        // builder sets it here, after the socket directory is known.
        var builder = WithEnvironment(EndpointEnvironmentVariable, "unix:" + ParsecSocketPath.InContainer(DockerResourceConfiguration));

        if (NeedsConfigFile(DockerResourceConfiguration))
        {
            var content = ParsecConfigFile.Build(
                DockerResourceConfiguration.AuthType ?? ParsecImage.DefaultAuthType,
                DockerResourceConfiguration.LogLevel ?? ParsecImage.DefaultLogLevel,
                ParsecSocketPath.DirectoryInContainer(DockerResourceConfiguration));

            // The file is root owned and world readable, because the service does not run as root.
            builder = builder.WithResourceMapping(
                Encoding.UTF8.GetBytes(content),
                ParsecImage.ConfigFilePath,
                fileMode: Unix.FileMode644);
        }

        return new ParsecContainer(builder.DockerResourceConfiguration);
    }

    /// <inheritdoc/>
    protected override ParsecBuilder Init()
        => base.Init()
            .WithImage(ParsecImage.Reference)
            .WithEnvironment(EndpointEnvironmentVariable, "unix:" + ParsecImage.SocketDirectory + "/" + ParsecImage.SocketFileName)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilCommandIsCompleted("parsec-tool", "ping"));

    /// <inheritdoc/>
    protected override void Validate()
    {
        base.Validate();

        var socketDirectory = DockerResourceConfiguration.SocketDirectory;
        if (socketDirectory is null)
        {
            return;
        }

        _ = Guard.Argument(socketDirectory, nameof(ParsecConfiguration.SocketDirectory))
            .NotEmpty()
            .ThrowIf(
                argument => !argument.Value.StartsWith('/'),
                argument => new ArgumentException(
                    "The socket directory must be an absolute path in the container. Give a path that starts with a slash.",
                    argument.Name))
            .ThrowIf(
                argument => argument.Value.TrimEnd('/').Length == 0,
                argument => new ArgumentException(
                    "The socket directory must not be the root directory. Give a directory that the service user can write.",
                    argument.Name));
    }

    /// <inheritdoc/>
    protected override ParsecBuilder Clone(IResourceConfiguration<CreateContainerParameters> resourceConfiguration)
        => Merge(DockerResourceConfiguration, new ParsecConfiguration(resourceConfiguration));

    /// <inheritdoc/>
    protected override ParsecBuilder Clone(IContainerConfiguration resourceConfiguration)
        => Merge(DockerResourceConfiguration, new ParsecConfiguration(resourceConfiguration));

    /// <inheritdoc/>
    protected override ParsecBuilder Merge(ParsecConfiguration oldValue, ParsecConfiguration newValue)
        => new(new ParsecConfiguration(oldValue, newValue));

    /// <summary>
    /// Tells if the settings are different from the settings in the image.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    /// <returns><c>true</c> when the builder must write a new configuration file.</returns>
    private static bool NeedsConfigFile(ParsecConfiguration configuration)
        => (configuration.AuthType is { } authType && authType != ParsecImage.DefaultAuthType)
            || (configuration.LogLevel is { } logLevel && logLevel != ParsecImage.DefaultLogLevel)
            || ParsecSocketPath.DirectoryInContainer(configuration) != ParsecImage.SocketDirectory;
}
