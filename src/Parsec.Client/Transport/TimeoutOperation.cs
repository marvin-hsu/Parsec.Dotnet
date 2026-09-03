using System.Globalization;

namespace Parsec.Client.Transport;

/// <summary>
/// Runs one asynchronous operation with a time limit.
/// </summary>
/// <remarks>
/// A socket read waits for as long as the other side keeps the connection open. The client puts
/// a limit on each connect, send and receive, so a service that stops answering does not stop
/// the application.
/// </remarks>
internal static class TimeoutOperation
{
    /// <summary>
    /// Runs an operation and stops it after a time.
    /// </summary>
    /// <param name="timeout">
    /// The time limit, or <see cref="Timeout.InfiniteTimeSpan"/> for no limit.
    /// </param>
    /// <param name="operation">The work to run. It gets the token to observe.</param>
    /// <param name="cancellationToken">The token of the caller.</param>
    /// <returns>A task that completes when the operation completes.</returns>
    /// <exception cref="TimeoutException">The operation did not finish inside the limit.</exception>
    public static async ValueTask RunAsync(
        TimeSpan timeout,
        Func<CancellationToken, ValueTask> operation,
        CancellationToken cancellationToken)
    {
        await RunAsync<object?>(
            timeout,
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Runs an operation that has a result and stops it after a time.
    /// </summary>
    /// <typeparam name="TResult">The type of the result.</typeparam>
    /// <param name="timeout">
    /// The time limit, or <see cref="Timeout.InfiniteTimeSpan"/> for no limit.
    /// </param>
    /// <param name="operation">The work to run. It gets the token to observe.</param>
    /// <param name="cancellationToken">The token of the caller.</param>
    /// <returns>The result of the operation.</returns>
    /// <exception cref="TimeoutException">The operation did not finish inside the limit.</exception>
    public static async ValueTask<TResult> RunAsync<TResult>(
        TimeSpan timeout,
        Func<CancellationToken, ValueTask<TResult>> operation,
        CancellationToken cancellationToken)
    {
        if (timeout == Timeout.InfiniteTimeSpan)
        {
            return await operation(cancellationToken).ConfigureAwait(false);
        }

        using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        source.CancelAfter(timeout);

        try
        {
            return await operation(source.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            // The caller did not cancel, so the limit did. Report the limit, because a
            // cancellation that the caller did not ask for is confusing to handle.
            throw new TimeoutException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"The Parsec service did not answer inside the time limit of {timeout}."),
                exception);
        }
    }
}
