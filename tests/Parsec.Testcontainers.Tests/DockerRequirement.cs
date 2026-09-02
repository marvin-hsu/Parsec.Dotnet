using DotNet.Testcontainers.Builders;

namespace Parsec.Testcontainers.Tests;

/// <summary>
/// Gives the tests a builder only when Docker answers.
/// </summary>
/// <remarks>
/// The Testcontainers base builder needs a reachable Docker endpoint before it starts a
/// container. It resolves the endpoint and the container operating system while it makes the
/// builder, and it checks the endpoint again in <c>Validate</c>. With no endpoint both steps
/// throw. A test in the integration lane must not fail for that reason, so the helper looks at
/// the endpoint one time and turns the failure into a skip.
/// </remarks>
internal static class DockerRequirement
{
    private static readonly Lazy<string?> _skipReason = new(FindSkipReason);

    /// <summary>
    /// Makes a new <see cref="ParsecBuilder"/>, or skips the test when Docker does not answer.
    /// </summary>
    /// <returns>A new builder with the default settings.</returns>
    internal static ParsecBuilder CreateBuilder()
    {
        if (_skipReason.Value is { } reason)
        {
            Assert.Skip(reason);
        }

        return new ParsecBuilder();
    }

    /// <summary>
    /// Tries the full path that a test takes, from the constructor to <c>Build</c>.
    /// </summary>
    /// <returns>The reason to skip, or <c>null</c> when Docker answers.</returns>
    private static string? FindSkipReason()
    {
        try
        {
            _ = new ParsecBuilder().Build();

            return null;
        }
        catch (DockerUnavailableException exception)
        {
            return "Docker is not reachable: " + exception.Message;
        }
    }
}
