namespace Parsec.Testcontainers;

/// <summary>
/// The log level of the Parsec service.
/// </summary>
/// <remarks>
/// The values match the log levels that the <c>core_settings.log_level</c> key of the service
/// configuration accepts. Each value includes the levels above it. For example
/// <see cref="Debug"/> also writes the <see cref="Info"/> records.
/// </remarks>
public enum ParsecLogLevel
{
    /// <summary>
    /// Write only the errors.
    /// </summary>
    Error = 0,

    /// <summary>
    /// Write the errors and the warnings.
    /// </summary>
    Warn = 1,

    /// <summary>
    /// Write the progress records. This is the level in the image configuration.
    /// </summary>
    Info = 2,

    /// <summary>
    /// Write the records that help you find a defect.
    /// </summary>
    Debug = 3,

    /// <summary>
    /// Write all the records. This level is very loud.
    /// </summary>
    Trace = 4,
}
