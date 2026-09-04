namespace Parsec.Client.Protocol;

/// <summary>
/// Helper methods for <see cref="ResponseStatus"/>.
/// </summary>
public static class ResponseStatusExtensions
{
    /// <summary>The one value in the PSA range that the protocol does not use.</summary>
    private const ushort UnassignedPsaStatus = 1144;

    /// <summary>
    /// Tells if the value is a status that this protocol version defines.
    /// </summary>
    /// <param name="value">The value that came off the wire.</param>
    /// <returns><see langword="true"/> if the protocol defines the value.</returns>
    /// <remarks>
    /// An unknown value is not an error here. The service can add statuses, so the client must
    /// accept a value that it does not know and report it as a failure of an unknown cause.
    /// </remarks>
    public static bool IsKnown(this ResponseStatus value) =>
        value is >= ResponseStatus.Success and <= ResponseStatus.AdminOperation
        || (value is >= ResponseStatus.PsaErrorGenericError and <= ResponseStatus.PsaErrorDataCorrupt
            && (ushort)value != UnassignedPsaStatus);
}
