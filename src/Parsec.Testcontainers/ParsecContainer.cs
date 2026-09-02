using DotNet.Testcontainers.Containers;

namespace Parsec.Testcontainers;

/// <summary>
/// A container that runs the Parsec service.
/// </summary>
/// <remarks>
/// Use <see cref="ParsecBuilder"/> to make an instance. Call <c>StartAsync</c> before you use the
/// service, and dispose the instance to stop the container.
/// </remarks>
public sealed class ParsecContainer : DockerContainer
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ParsecContainer"/> class.
    /// </summary>
    /// <param name="configuration">The container configuration.</param>
    public ParsecContainer(ParsecConfiguration configuration)
        : base(configuration)
        => Configuration = configuration;

    /// <summary>
    /// Gets the configuration that the builder made for this container.
    /// </summary>
    internal ParsecConfiguration Configuration { get; }
}
