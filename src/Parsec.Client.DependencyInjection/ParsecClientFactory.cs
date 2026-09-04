namespace Parsec.Client.DependencyInjection;

/// <summary>
/// Builds the client on the first call and hands the same one back afterwards.
/// </summary>
/// <param name="options">The settings to build the client from.</param>
/// <remarks>
/// Both disposal interfaces are here on purpose. A service provider that holds a singleton which
/// implements only <see cref="IAsyncDisposable"/> raises when it is disposed synchronously, which
/// would turn a plain <c>provider.Dispose()</c> into a failure at shutdown. A host disposes
/// asynchronously and takes the first path; anything else takes the second.
/// </remarks>
internal sealed class ParsecClientFactory(ParsecClientOptions options)
    : IParsecClientFactory, IAsyncDisposable, IDisposable
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    private ParsecClient? _client;
    private bool _disposed;

    /// <inheritdoc/>
    public async ValueTask<IParsecClient> GetAsync(CancellationToken cancellationToken = default)
    {
        // Before the gate rather than after it. Disposal disposes the gate too, so waiting on it
        // first would raise from the semaphore and blame it for what is really a use after
        // dispose.
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_client is { } connected)
        {
            return connected;
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            // A connect that failed is not remembered. A service that was down when the
            // application started should not stay unreachable for the life of the process.
            _client ??= await ParsecClient.CreateAsync(options, cancellationToken).ConfigureAwait(false);

            return _client;
        }
        finally
        {
            _ = _gate.Release();
        }
    }

    /// <inheritdoc/>
    public ValueTask DisposeAsync()
    {
        var client = Release();

        return client?.DisposeAsync() ?? ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public void Dispose() =>

        // A client releases nothing that has to be awaited: it holds no connection between
        // calls, so its DisposeAsync completes without yielding. Blocking on it is a formality
        // rather than sync over async, and if that stops being true this is the line to change.
        Release()?.DisposeAsync().AsTask().GetAwaiter().GetResult();

    /// <summary>
    /// Marks the factory disposed and hands back the client that still needs disposing.
    /// </summary>
    /// <returns>The client, or <see langword="null"/> when there is nothing left to do.</returns>
    /// <remarks>
    /// Both disposal paths share this so that neither can drift from the other. Disposing twice,
    /// or once each way, does the work once.
    /// </remarks>
    private ParsecClient? Release()
    {
        if (_disposed)
        {
            return null;
        }

        _disposed = true;

        var client = _client;

        _client = null;
        _gate.Dispose();

        return client;
    }
}
