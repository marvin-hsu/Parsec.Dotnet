using System.Text;
using Docker.DotNet.Models;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Configurations;
using Parsec.Testcontainers.Configuration;
using Parsec.Testcontainers.Sockets;

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
/// <para>
/// On a Linux host the builder mounts a new directory of this machine over the socket directory of
/// the container. A client on this machine then connects to
/// <see cref="ParsecContainer.SocketPath"/> with no bridge. The container makes the directory
/// before the start and removes it after the stop.
/// </para>
/// <para>
/// On another host system the builder maps a port of the container instead, and the container
/// puts a bridge between that port and a socket of this machine.
/// <see cref="ParsecContainer.SocketPath"/> then gives the socket of the bridge. The client under
/// test speaks only to a Unix socket in both cases.
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
    {
        DockerResourceConfiguration = Init().DockerResourceConfiguration;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecBuilder"/> class.
    /// </summary>
    /// <param name="resourceConfiguration">The Docker resource configuration.</param>
    private ParsecBuilder(ParsecConfiguration resourceConfiguration)
        : base(resourceConfiguration)
    {
        DockerResourceConfiguration = resourceConfiguration;
    }

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

    /// <summary>
    /// Gives the service a configuration file of your own.
    /// </summary>
    /// <param name="configFilePath">A path on this machine to a Parsec configuration file.</param>
    /// <returns>A configured instance of <see cref="ParsecBuilder"/>.</returns>
    /// <remarks>
    /// <para>
    /// The module writes a configuration file from the settings you give it, and that file
    /// covers the authenticator, the log level and the socket directory. Reach for this method
    /// when you need a setting the module does not offer, such as an administrator list, another
    /// key info manager, or another provider.
    /// </para>
    /// <para>
    /// The file replaces the one in the image, so it has to be complete. The schema follows the
    /// Parsec release in the image. A provider only works when the service in the image was
    /// built with it.
    /// </para>
    /// <para>
    /// Your file decides where the socket goes. Tell the module the same directory with
    /// <see cref="WithSocketDirectory"/> whenever it is not the directory of the image, because
    /// the module cannot read your file to find out.
    /// </para>
    /// </remarks>
    public ParsecBuilder WithConfigFile(string configFilePath)
        => Merge(DockerResourceConfiguration, new ParsecConfiguration(configFilePath: configFilePath));

    /// <inheritdoc/>
    public override ParsecContainer Build()
    {
        Validate();

        // The endpoint variable must agree with the socket path in the configuration file, so the
        // builder sets it here, after the socket directory is known.
        var builder = WithEnvironment(EndpointEnvironmentVariable, "unix:" + ParsecSocketPath.InContainer(DockerResourceConfiguration));

        // The file is root owned and world readable, because the service does not run as root.
        if (DockerResourceConfiguration.ConfigFilePath is { } callerFile)
        {
            builder = builder.WithResourceMapping(
                File.ReadAllBytes(callerFile),
                ParsecImage.ConfigFilePath,
                fileMode: Unix.FileMode644);
        }
        else if (NeedsConfigFile(DockerResourceConfiguration))
        {
            var content = ParsecConfigFile.Build(
                DockerResourceConfiguration.AuthType ?? ParsecImage.DefaultAuthType,
                DockerResourceConfiguration.LogLevel ?? ParsecImage.DefaultLogLevel,
                ParsecSocketPath.DirectoryInContainer(DockerResourceConfiguration));

            builder = builder.WithResourceMapping(
                Encoding.UTF8.GetBytes(content),
                ParsecImage.ConfigFilePath,
                fileMode: Unix.FileMode644);
        }

        // The directory holds the socket that a client on this machine connects to. On a Linux
        // host the container writes its own socket there through a bind mount. On another host
        // system the container runs in a virtual machine, where only the socket file crosses the
        // file system and carries no connection. There the bridge makes the socket instead, and
        // the container maps the port that socat listens on.
        var hostSocketDirectory = ParsecHostSocketDirectory.Create();
        var needsBridge = !ParsecHostSocketDirectory.IsBindMountSupported;

        builder = needsBridge
            ? builder.WithPortBinding(ParsecSocketBridge.PortInContainer, assignRandomHostPort: true)
            : builder.WithBindMount(
                hostSocketDirectory.DirectoryPath,
                ParsecSocketPath.DirectoryInContainer(DockerResourceConfiguration),
                AccessMode.ReadWrite);

        return new ParsecContainer(builder.DockerResourceConfiguration)
        {
            HostSocketDirectory = hostSocketDirectory,
            NeedsSocketBridge = needsBridge,
        };
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

        ParsecSocketPath.ValidateDirectory(DockerResourceConfiguration.SocketDirectory);

        if (DockerResourceConfiguration.ConfigFilePath is not { } configFilePath)
        {
            return;
        }

        // Validate runs on the configuration, not on a call, so there is no argument to name.
        if (configFilePath.Length == 0)
        {
            throw new InvalidOperationException(
                "WithConfigFile needs a path to a Parsec configuration file.");
        }

        if (!File.Exists(configFilePath))
        {
            throw new FileNotFoundException(
                "The Parsec configuration file of this build is not there.",
                configFilePath);
        }

        // The settings below write into a file that this build no longer produces, so keeping
        // them would drop them without a word.
        if (DockerResourceConfiguration.AuthType is not null || DockerResourceConfiguration.LogLevel is not null)
        {
            throw new InvalidOperationException(
                "WithConfigFile replaces the configuration file, so WithAuthType and WithLogLevel have nothing to write to. Put the authenticator and the log level in your file.");
        }
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
