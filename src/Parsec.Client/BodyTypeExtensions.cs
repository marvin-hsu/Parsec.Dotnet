namespace Parsec.Client;

/// <summary>
/// Helper methods for <see cref="BodyType"/>.
/// </summary>
internal static class BodyTypeExtensions
{
    /// <summary>
    /// Tells if the value is a body encoding that this protocol version defines.
    /// </summary>
    /// <param name="value">The value that came off the wire.</param>
    /// <returns><see langword="true"/> if the protocol defines the value.</returns>
    public static bool IsKnown(this BodyType value) => value is BodyType.Protobuf;
}
