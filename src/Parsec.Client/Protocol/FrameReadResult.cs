namespace Parsec.Client.Protocol;

/// <summary>
/// The outcome of a read of one message from a stream.
/// </summary>
/// <remarks>
/// A read reports a malformed message through <see cref="Error"/> and throws nothing. An
/// asynchronous method cannot use an output parameter, so the outcome and the message travel
/// together in this type.
/// </remarks>
internal readonly record struct FrameReadResult
{
    /// <summary>Gets the cause of a failed read, or <see cref="ParsecFrameError.None"/>.</summary>
    public ParsecFrameError Error { get; init; }

    /// <summary>Gets the message, or the default value if the read failed.</summary>
    public ParsecResponse Response { get; init; }

    /// <summary>Gets a value indicating whether the read produced a message.</summary>
    public bool IsSuccess => Error == ParsecFrameError.None;

    /// <summary>Makes the outcome of a read that produced a message.</summary>
    /// <param name="response">The message that was read.</param>
    /// <returns>A successful outcome.</returns>
    public static FrameReadResult Succeeded(ParsecResponse response) => new() { Response = response };

    /// <summary>Makes the outcome of a read that did not produce a message.</summary>
    /// <param name="error">The cause of the failure.</param>
    /// <returns>A failed outcome.</returns>
    public static FrameReadResult Failed(ParsecFrameError error) => new() { Error = error };
}
