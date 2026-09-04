namespace Parsec.Client.Tests;

/// <summary>
/// Groups the tests that must not run while another test of this project opens a socket.
/// </summary>
/// <remarks>
/// The collection owns the one service container that every integration test of this project
/// shares. One container per test class would be three containers for one service, which is slow
/// and which made a container test of the other project fail under the load.
/// <para>
/// <c>AConnectThatFailsClosesTheSocketItOpened</c> counts the descriptors of the whole process,
/// because a socket that a failed connect leaves behind cannot be seen any other way. Anything
/// else that opens one while the count runs looks like the leak it hunts for. The integration
/// tests reach a container over Docker and open plenty, and the two lanes share one process when
/// the analysis job runs them together. Naming one collection keeps them apart.
/// </para>
/// </remarks>
[CollectionDefinition(nameof(SocketTestGroup))]
public sealed class SocketTestGroup : ICollectionFixture<ParsecServiceFixture>;
