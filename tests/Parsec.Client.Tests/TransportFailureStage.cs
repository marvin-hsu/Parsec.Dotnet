namespace Parsec.Client.Tests;

/// <summary>
/// Names the step of an exchange at which <see cref="FailingTransport"/> throws.
/// </summary>
internal enum TransportFailureStage
{
    /// <summary>The transport throws when the client opens a connection.</summary>
    Connect,

    /// <summary>The connection opens, then the transport throws when the client sends.</summary>
    Send,

    /// <summary>The request goes out, then the transport throws when the client reads the answer.</summary>
    Receive,
}
