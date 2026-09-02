namespace Parsec.Testcontainers.Tests;

/// <summary>
/// Groups the tests that start a container, so they never run at the same time as another
/// test class.
/// </summary>
/// <remarks>
/// xunit runs the tests of one class one after the other, but runs classes in parallel. A test
/// that starts a container also opens a TCP port and a Unix socket on this machine, and other
/// classes do the same with their own servers and sockets. Turning parallelization off for this
/// collection keeps those resources apart. The unit test classes stay parallel.
/// </remarks>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class DockerTestGroup
{
    /// <summary>
    /// The name that a test class gives to <see cref="CollectionAttribute"/> to join this collection.
    /// </summary>
    public const string Name = "Docker";
}
