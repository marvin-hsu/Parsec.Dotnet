using System.Globalization;
using DotNet.Testcontainers.Containers;

namespace Parsec.Testcontainers;

/// <summary>
/// A container that runs the Parsec service.
/// </summary>
/// <remarks>
/// Use <see cref="ParsecBuilder"/> to make an instance. Call <c>StartAsync</c> before you use the
/// service, and dispose the instance to stop the container. After the start, give
/// <see cref="Endpoint"/> or <see cref="SocketPath"/> to the client under test.
/// </remarks>
public sealed class ParsecContainer : DockerContainer
{
    /// <summary>
    /// The name of the command line tool of the Parsec project. The image has the tool on the
    /// path.
    /// </summary>
    public const string ToolName = "parsec-tool";

    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecContainer"/> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    public ParsecContainer(ParsecConfiguration configuration)
        : base(configuration)
    {
        Configuration = configuration;
        ContainerSocketPath = ParsecSocketPath.InContainer(configuration);
    }

    /// <summary>
    /// Gets the path of the socket that a client on this machine connects to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is a file system path, not a network address, because the service accepts only a
    /// Unix domain socket. Read the value after <c>StartAsync</c>.
    /// </para>
    /// <para>
    /// The path is the path in the container while the container makes the socket directly
    /// available to this machine. When the socket needs a bridge, the container reports the path
    /// of the bridge instead.
    /// </para>
    /// </remarks>
    public string SocketPath => HostSocketPath ?? ContainerSocketPath;

    /// <summary>
    /// Gets the endpoint of the service as a <c>unix:</c> URI. A Parsec client accepts this form
    /// in the <c>PARSEC_SERVICE_ENDPOINT</c> environment variable.
    /// </summary>
    /// <remarks>The URI holds <see cref="SocketPath"/>. Read the value after <c>StartAsync</c>.</remarks>
    public Uri Endpoint => new("unix:" + SocketPath);

    /// <summary>
    /// Gets the configuration that the builder made for this container.
    /// </summary>
    internal ParsecConfiguration Configuration { get; }

    /// <summary>
    /// Gets the path of the socket of the service in the container.
    /// </summary>
    internal string ContainerSocketPath { get; }

    /// <summary>
    /// Gets or sets the path of a socket on this machine that forwards to the service, or
    /// <c>null</c> while no bridge is necessary.
    /// </summary>
    internal string? HostSocketPath { get; set; }

    /// <summary>
    /// Runs the command line tool of the Parsec project in the container.
    /// </summary>
    /// <param name="args">The arguments for the tool, for example <c>list-providers</c>.</param>
    /// <returns>The exit code, the standard output and the standard error of the tool.</returns>
    /// <remarks>
    /// The method does not look at the exit code. Read <see cref="ExecResult.ExitCode"/> to find
    /// out if the tool did the work.
    /// </remarks>
    public Task<ExecResult> ExecParsecToolAsync(params string[] args)
        => ExecParsecToolAsync(args, CancellationToken.None);

    /// <summary>
    /// Runs the command line tool of the Parsec project in the container.
    /// </summary>
    /// <param name="args">The arguments for the tool, for example <c>list-providers</c>.</param>
    /// <param name="cancellationToken">A token to cancel the wait for the tool.</param>
    /// <returns>The exit code, the standard output and the standard error of the tool.</returns>
    /// <remarks>
    /// The method does not look at the exit code. Read <see cref="ExecResult.ExitCode"/> to find
    /// out if the tool did the work.
    /// </remarks>
    public Task<ExecResult> ExecParsecToolAsync(IEnumerable<string> args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(args);

        var command = new List<string>(1) { ToolName };
        command.AddRange(args);

        return ExecAsync(command, cancellationToken);
    }

    /// <summary>
    /// Asks the service for the version of the wire protocol.
    /// </summary>
    /// <param name="cancellationToken">A token to cancel the wait for the answer.</param>
    /// <returns>A task that completes when the service answers.</returns>
    /// <exception cref="InvalidOperationException">The service did not answer.</exception>
    /// <remarks>
    /// The method runs <c>parsec-tool ping</c> in the container. It shows that the service listens
    /// on the socket and speaks the protocol. Use it in a test that must be sure the service is
    /// ready before the client under test connects.
    /// </remarks>
    public async Task PingAsync(CancellationToken cancellationToken = default)
    {
        var result = await ExecParsecToolAsync(["ping"], cancellationToken).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            throw new InvalidOperationException(string.Format(
                CultureInfo.InvariantCulture,
                "The Parsec service did not answer the ping. The tool gave exit code {0}. {1}{2}",
                result.ExitCode,
                result.Stdout,
                result.Stderr));
        }
    }
}
