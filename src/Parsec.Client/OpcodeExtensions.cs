namespace Parsec.Client;

/// <summary>
/// Helper methods for <see cref="Opcode"/>.
/// </summary>
public static class OpcodeExtensions
{
    /// <summary>The one opcode value in the assigned range that the protocol does not use.</summary>
    private const uint UnassignedOpcode = 0x1D;

    /// <summary>
    /// Tells if the value is an operation that this protocol version defines.
    /// </summary>
    /// <param name="value">The value that came off the wire, or that the caller supplied.</param>
    /// <returns><see langword="true"/> if the protocol defines the value.</returns>
    /// <remarks>
    /// An unknown value is not an error here. The service can add operations, so the client
    /// must accept a value that it does not know and let the caller decide what to do.
    /// </remarks>
    public static bool IsKnown(this Opcode value) =>
        value is >= Opcode.Ping and <= Opcode.CanDoCrypto && (uint)value != UnassignedOpcode;
}
