using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Frosty.Sdk;

/// <remarks>
///     A future instanced logger implementation is planned.
/// </remarks>
public static class FrostyLogger
{
    /// <summary>
    ///     Use <cref cref="NullLogger"/>. No null check needed.
    /// </summary>
    /// <remarks>
    ///     Do not use the logger as a progress reporter.
    /// </remarks>
    public static ILogger Logger { get; set; } = NullLogger.Instance;

    /// <summary>
    ///     The progress of the current task.
    /// </summary>
    public static IProgress<double>? Progress { get; set; }
}