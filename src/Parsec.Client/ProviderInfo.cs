namespace Parsec.Client;

/// <summary>
/// Describes one provider of the Parsec service.
/// </summary>
/// <remarks>
/// The service reports the providers that it runs. A provider holds keys and does the
/// cryptography of a request. The core provider is not a cryptography provider: it reports the
/// state of the service.
/// </remarks>
/// <param name="id">The identifier that a request puts in the provider field of the header.</param>
/// <param name="uuid">The unique identifier of the provider implementation.</param>
/// <param name="description">The text that the provider gives about itself.</param>
/// <param name="vendor">The name of the vendor of the hardware or the library.</param>
/// <param name="version">The version of the provider.</param>
public sealed class ProviderInfo(
    ProviderId id,
    string uuid,
    string description,
    string vendor,
    Version version)
{
    /// <summary>Gets the identifier that a request puts in the provider field of the header.</summary>
    public ProviderId Id { get; } = id;

    /// <summary>
    /// Gets the unique identifier of the provider implementation.
    /// </summary>
    /// <remarks>
    /// The service sends the value as text. The client keeps the text as it came, because a
    /// value that is not a UUID must not stop the application from reading the other fields.
    /// </remarks>
    public string Uuid { get; } = uuid;

    /// <summary>Gets the text that the provider gives about itself.</summary>
    public string Description { get; } = description;

    /// <summary>Gets the name of the vendor of the hardware or the library.</summary>
    public string Vendor { get; } = vendor;

    /// <summary>
    /// Gets the version of the provider.
    /// </summary>
    /// <remarks>
    /// The service reports a major number, a minor number and a revision number. They become the
    /// major, the minor and the build parts of the version.
    /// </remarks>
    public Version Version { get; } = version;
}
