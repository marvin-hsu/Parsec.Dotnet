namespace Parsec.Client.Protocol;

/// <summary>
/// Helper methods for <see cref="ProviderId"/>.
/// </summary>
public static class ProviderIdExtensions
{
    /// <summary>
    /// Tells if the value is a provider that this protocol version defines.
    /// </summary>
    /// <param name="value">The value that came off the wire, or that the caller supplied.</param>
    /// <returns><see langword="true"/> if the protocol defines the value.</returns>
    /// <remarks>
    /// An unknown value is not an error here. The service can add providers, so the client
    /// must accept a value that it does not know and let the caller decide what to do.
    /// </remarks>
    public static bool IsKnown(this ProviderId value) =>
        value is >= ProviderId.Core and <= ProviderId.CryptoAuthLib;

    /// <summary>
    /// Tells if the provider answers cryptographic operations.
    /// </summary>
    /// <param name="value">The provider to test.</param>
    /// <returns>
    /// <see langword="true"/> for a known provider that is not <see cref="ProviderId.Core"/>.
    /// </returns>
    public static bool SupportsCrypto(this ProviderId value) =>
        value.IsKnown() && value != ProviderId.Core;
}
