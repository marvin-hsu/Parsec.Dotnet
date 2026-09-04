using Google.Protobuf;
using Parsec.Client.Authentication;
using Parsec.Client.Errors;
using Parsec.Client.Protocol;
using Parsec.Client.Transport;

namespace Parsec.Client.Operations;

/// <summary>
/// Runs one operation against the Parsec service.
/// </summary>
/// <remarks>
/// <para>
/// The class holds the steps that every operation repeats: encode the body, open a connection,
/// send the request, read the answer, check the answer against the request, and decode the body.
/// The service answers one request per connection, so each call opens its own connection and
/// closes it again.
/// </para>
/// <para>
/// The class raises an exception for every outcome that is not a complete answer. A caller that
/// treats a status as a normal result, for example a signature that does not verify, calls
/// <see cref="ExchangeAsync"/> and reads the status itself.
/// </para>
/// </remarks>
/// <param name="transport">Opens the connections to the service.</param>
internal sealed class ParsecOperationClient(IParsecTransport transport)
{
    /// <summary>
    /// Raises the exception of the status when the service did not report success.
    /// </summary>
    /// <param name="opcode">The operation of the request.</param>
    /// <param name="response">The answer of the service.</param>
    /// <exception cref="ParsecServiceException">The service reported a status other than success.</exception>
    public static void ThrowIfFailed(Opcode opcode, ParsecResponse response)
    {
        if (!response.IsSuccess)
        {
            throw ParsecServiceException.FromStatus(response.Header.Status, opcode);
        }
    }

    /// <summary>
    /// Decodes the body of an answer.
    /// </summary>
    /// <typeparam name="TResult">The message that the operation answers with.</typeparam>
    /// <param name="opcode">The operation of the request.</param>
    /// <param name="parser">The decoder of the answer.</param>
    /// <param name="body">The bytes of the body. An empty body is not a fault.</param>
    /// <returns>The decoded answer.</returns>
    /// <exception cref="ParsecProtocolException">The body does not decode.</exception>
    /// <remarks>
    /// An empty body decodes to a message whose every field holds its default value. Protobuf
    /// leaves a field that holds a default value off the wire, so an answer of all default values
    /// carries no bytes at all.
    /// </remarks>
    public static TResult Decode<TResult>(Opcode opcode, MessageParser<TResult> parser, ReadOnlyMemory<byte> body)
        where TResult : IMessage<TResult>
    {
        try
        {
            return parser.ParseFrom(body.Span);
        }
        catch (InvalidProtocolBufferException fault)
        {
            throw ParsecProtocolException.DecodeFailed(opcode, fault);
        }
    }

    /// <summary>
    /// Sends one request and reads the answer, without looking at the status.
    /// </summary>
    /// <param name="opcode">The operation to run.</param>
    /// <param name="provider">The provider that runs the operation.</param>
    /// <param name="authentication">The authentication of the request.</param>
    /// <param name="operation">The message that carries the arguments of the operation.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The answer of the service, whatever its status.</returns>
    /// <exception cref="ParsecProtocolException">
    /// The answer does not follow the wire protocol, or a successful answer names another
    /// operation.
    /// </exception>
    /// <exception cref="ParsecTransportException">The connection failed.</exception>
    public async Task<ParsecResponse> ExchangeAsync(
        Opcode opcode,
        ProviderId provider,
        IParsecAuthentication authentication,
        IMessage operation,
        CancellationToken cancellationToken)
    {
        var request = ParsecRequest.Create(opcode, provider, authentication, operation.ToByteArray());

        var connection = await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
        FrameReadResult read;

        await using (connection.ConfigureAwait(false))
        {
            await connection.SendAsync(request, cancellationToken).ConfigureAwait(false);

            read = await connection.ReceiveAsync(cancellationToken).ConfigureAwait(false);
        }

        if (!read.IsSuccess)
        {
            throw ParsecProtocolException.FromFrameError(read.Error, opcode);
        }

        var response = read.Response;

        // The service copies the opcode of the request into the answer. Another value means that
        // the client and the service do not agree on where a message starts, so the body cannot
        // be trusted to hold the result of this operation.
        //
        // The check applies to a successful answer only. When the service cannot read the request
        // at all, it answers with a default header that names Ping and the core provider and
        // carries the true status. That status is the answer the caller must see, so a failed
        // answer goes back whatever opcode it names.
        if (response.IsSuccess && response.Header.Opcode != opcode)
        {
            throw ParsecProtocolException.MismatchedOpcode(opcode, response.Header.Opcode);
        }

        return response;
    }

    /// <summary>
    /// Sends one request and decodes a successful answer.
    /// </summary>
    /// <typeparam name="TResult">The message that the operation answers with.</typeparam>
    /// <param name="opcode">The operation to run.</param>
    /// <param name="provider">The provider that runs the operation.</param>
    /// <param name="authentication">The authentication of the request.</param>
    /// <param name="operation">The message that carries the arguments of the operation.</param>
    /// <param name="parser">The decoder of the answer.</param>
    /// <param name="cancellationToken">Stops the exchange.</param>
    /// <returns>The decoded answer.</returns>
    /// <exception cref="ParsecServiceException">The service reported a status other than success.</exception>
    /// <exception cref="ParsecProtocolException">
    /// The answer does not follow the wire protocol, a successful answer names another operation,
    /// or the body does not decode.
    /// </exception>
    /// <exception cref="ParsecTransportException">The connection failed.</exception>
    public async Task<TResult> ExecuteAsync<TResult>(
        Opcode opcode,
        ProviderId provider,
        IParsecAuthentication authentication,
        IMessage operation,
        MessageParser<TResult> parser,
        CancellationToken cancellationToken)
        where TResult : IMessage<TResult>
    {
        var response = await ExchangeAsync(opcode, provider, authentication, operation, cancellationToken)
            .ConfigureAwait(false);

        ThrowIfFailed(opcode, response);

        return Decode(opcode, parser, response.Body);
    }
}
