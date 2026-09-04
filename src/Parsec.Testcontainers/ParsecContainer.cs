using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using DotNet.Testcontainers.Containers;
using Parsec.Testcontainers.Sockets;

namespace Parsec.Testcontainers;

/// <summary>
/// A container that runs the Parsec service.
/// </summary>
/// <remarks>
/// Use <see cref="ParsecBuilder"/> to make an instance. Call <c>StartAsync</c> before you use the
/// service, and dispose the instance to stop the container. After the start, give
/// <see cref="Endpoint"/> or <see cref="SocketPath"/> to the client under test.
/// </remarks>
/// <param name="configuration">The container configuration.</param>
public sealed class ParsecContainer(ParsecConfiguration configuration) : DockerContainer(configuration)
{
    /// <summary>
    /// The name of the command line tool of the Parsec project. The image has the tool on the
    /// path.
    /// </summary>
    public const string ToolName = "parsec-tool";

    /// <summary>
    /// The value of the option <c>-t</c> of socat, in seconds. It is the time that socat holds a
    /// connection open after one side closes its half of the connection.
    /// </summary>
    private const string SocatHalfCloseSeconds = "60";

    /// <summary>
    /// The number of times that the shell looks for the port of socat in the container.
    /// </summary>
    private const string SocatTries = "100";

    /// <summary>
    /// The time between two looks of the shell for the port of socat, in seconds.
    /// </summary>
    private const string SocatPollSeconds = "0.1";

    /// <summary>
    /// The file in the container that holds the output of socat. The shell gives the file with
    /// the error when socat does not listen.
    /// </summary>
    [SuppressMessage(
        "Minor Code Smell",
        "S5443:Use a directory that is not publicly writable",
        Justification = "The path is inside the container, not on this machine. The container holds one service, it is thrown away after the test, and nothing else writes to its file system.")]
    private const string SocatLogPath = "/tmp/parsec-socat.log";

    /// <summary>
    /// The bridge on this machine, or <c>null</c> while no bridge runs.
    /// </summary>
    [SuppressMessage(
        "Usage",
        "CA2213:Disposable fields should be disposed",
        Justification = "The stop of the container and DisposeAsyncCore both close the bridge. The base class gives no DisposeAsync method to override, so the rule cannot see the call.")]
    private ParsecSocketBridge? _bridge;

    /// <summary>
    /// Gets the path of the socket that a client on this machine connects to.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The value is a file system path, not a network address, because the service accepts only a
    /// Unix domain socket. Read the value after <c>StartAsync</c>.
    /// </para>
    /// <para>
    /// On a Linux host the container mounts a directory of this machine over the socket directory,
    /// so the path is a path on this machine, under the temporary area. On another host system the
    /// container starts a bridge, and the path is the path of the socket of the bridge, in the
    /// same temporary area. Both paths only carry a connection after <c>StartAsync</c>.
    /// </para>
    /// </remarks>
    public string SocketPath => HostSocketDirectory?.SocketPath ?? ContainerSocketPath;

    /// <summary>
    /// Gets the endpoint of the service as a <c>unix:</c> URI. A Parsec client accepts this form
    /// in the <c>PARSEC_SERVICE_ENDPOINT</c> environment variable.
    /// </summary>
    /// <remarks>The URI holds <see cref="SocketPath"/>. Read the value after <c>StartAsync</c>.</remarks>
    public Uri Endpoint => new("unix:" + SocketPath);

    /// <summary>
    /// Gets the configuration that the builder made for this container.
    /// </summary>
    internal ParsecConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Gets the path of the socket of the service in the container.
    /// </summary>
    internal string ContainerSocketPath { get; } = ParsecSocketPath.InContainer(configuration);

    /// <summary>
    /// Gets the directory on this machine that holds the socket a client connects to, or
    /// <c>null</c> when only the socket in the container exists.
    /// </summary>
    internal ParsecHostSocketDirectory? HostSocketDirectory { get; init; }

    /// <summary>
    /// Gets a value indicating whether the container must bridge the socket of the service to a
    /// socket of this machine.
    /// </summary>
    /// <remarks>
    /// The builder sets the value. It is <c>true</c> when the host system gives no usable socket
    /// through a bind mount, and the configuration of the container then maps a port for the
    /// bridge.
    /// </remarks>
    internal bool NeedsSocketBridge { get; init; }

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

    /// <inheritdoc/>
    protected override async ValueTask DisposeAsyncCore()
    {
        // The bridge must close even when a caller disposes the container without a stop.
        await StopBridgeAsync().ConfigureAwait(false);

        await base.DisposeAsyncCore().ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task UnsafeStartAsync(CancellationToken ct = default)
    {
        await base.UnsafeStartAsync(ct).ConfigureAwait(false);

        if (!NeedsSocketBridge || HostSocketDirectory is null || _bridge is not null)
        {
            return;
        }

        // The service answers a ping now, because the wait strategy of the builder asked it. The
        // bridge can start.
        await StartSocatAsync(ct).ConfigureAwait(false);

        _bridge = ParsecSocketBridge.Start(
            HostSocketDirectory.SocketPath,
            Hostname,
            GetMappedPublicPort(ParsecSocketBridge.PortInContainer));
    }

    /// <inheritdoc/>
    protected override async Task UnsafeStopAsync(CancellationToken ct = default)
    {
        // The bridge holds connections to the container, so it closes first.
        await StopBridgeAsync().ConfigureAwait(false);

        await base.UnsafeStopAsync(ct).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    protected override async Task UnsafeCreateAsync(CancellationToken ct = default)
    {
        // The directory must exist, and it must have full permissions, before Docker makes the
        // container. Docker makes a missing bind mount source as root, and the service in the
        // container is not root. A host with a bridge keeps the socket of the bridge in the same
        // directory.
        HostSocketDirectory?.MakeDirectory();

        try
        {
            await base.UnsafeCreateAsync(ct).ConfigureAwait(false);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // The Docker client reports a status code and little else. A caller who reads only
            // the message would not learn which image failed, and the image is the setting they
            // are most likely to have changed.
            throw new InvalidOperationException(
                string.Format(
                    CultureInfo.InvariantCulture,
                    "The Parsec container could not be created from image \"{0}\". Check that the image name and tag are right and that this machine can pull them.",
                    Configuration.Image.FullName),
                e);
        }
    }

    /// <inheritdoc/>
    protected override async Task UnsafeDeleteAsync(CancellationToken ct = default)
    {
        // A delete without a stop also has to close the bridge. The method runs one time only.
        await StopBridgeAsync().ConfigureAwait(false);

        try
        {
            await base.UnsafeDeleteAsync(ct).ConfigureAwait(false);
        }
        finally
        {
            // The container holds the socket, so the directory goes away after the container.
            HostSocketDirectory?.Remove();
        }
    }

    /// <summary>
    /// Starts the part of the bridge that runs in the container.
    /// </summary>
    /// <param name="ct">A token to cancel the wait for the start.</param>
    /// <returns>A task that completes when socat listens on the port.</returns>
    /// <exception cref="InvalidOperationException">socat did not listen on the port.</exception>
    /// <remarks>
    /// <para>
    /// socat listens for the life of the container, so the shell puts it in the background. The
    /// shell then waits for the entry of the port in <c>/proc/net/tcp</c>, because only the
    /// network namespace of the container shows whether socat listens. A connection from this
    /// machine shows nothing: the port forward of Docker accepts the connection of a client even
    /// when no process in the container listens. The exit code of the shell is therefore the one
    /// signal that socat is ready, and a failure of socat gives an error with the output of the
    /// process.
    /// </para>
    /// <para>
    /// The option <c>-t</c> holds the connection open after one side closes its half. The default
    /// of socat is half a second, which is less than the time that the service needs for the
    /// answer of an operation such as the make of a key. A client that closes the half of the
    /// request would lose that answer.
    /// </para>
    /// <para>
    /// The image has socat, so the container needs no other container and no volume.
    /// </para>
    /// </remarks>
    private async Task StartSocatAsync(CancellationToken ct)
    {
        var port = ParsecSocketBridge.PortInContainer.ToString(CultureInfo.InvariantCulture);
        var portInHex = ParsecSocketBridge.PortInContainer.ToString("X4", CultureInfo.InvariantCulture);

        // The state 0A of an entry of /proc/net/tcp means that a process listens on the port.
        var command =
            $"socat -t {SocatHalfCloseSeconds} TCP-LISTEN:{port},fork,reuseaddr UNIX-CONNECT:{ContainerSocketPath} >{SocatLogPath} 2>&1 &"
            + $" for _ in $(seq 1 {SocatTries}); do"
            + $" if grep -qE '^ *[0-9]+: [0-9A-F]+:{portInHex} [0-9A-F]+:0000 0A' /proc/net/tcp; then exit 0; fi;"
            + $" sleep {SocatPollSeconds}; done;"
            + $" cat {SocatLogPath} >&2; exit 1";

        var result = await ExecAsync(["sh", "-c", command], ct).ConfigureAwait(false);

        if (result.ExitCode != 0)
        {
            var message = string.Format(
                CultureInfo.InvariantCulture,
                "The container did not start the bridge. The shell gave exit code {0}. {1}{2}",
                result.ExitCode,
                result.Stdout,
                result.Stderr);

            throw new InvalidOperationException(message);
        }
    }

    /// <summary>
    /// Closes the bridge on this machine, if one runs.
    /// </summary>
    /// <returns>A task that completes when the socket of the bridge is closed.</returns>
    private async Task StopBridgeAsync()
    {
        if (_bridge is { } bridge)
        {
            _bridge = null;

            await bridge.DisposeAsync().ConfigureAwait(false);
        }
    }
}
