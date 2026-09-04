namespace Parsec.Client.Tests;

/// <summary>
/// Groups the tests that must not run while another test of this project opens a socket.
/// </summary>
/// <remarks>
/// <c>AConnectThatFailsClosesTheSocketItOpened</c> counts the descriptors of the whole process,
/// because a socket that a failed connect leaves behind cannot be seen any other way. Anything
/// else that opens one while the count runs looks like the leak it hunts for. The integration
/// tests reach a container over Docker and open plenty, and the two lanes share one process when
/// the analysis job runs them together. Naming one collection keeps them apart.
/// </remarks>
[CollectionDefinition(nameof(SocketTestGroup))]
public sealed class SocketTestGroup;
