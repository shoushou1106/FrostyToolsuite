using System.Globalization;

namespace Frosty.Sdk.Exceptions;

// Temporary, lists the callers left until it is deleted.
[Obsolete("UnknownValueException will be removed. Throw an InvalidDataException with the value in message instead.",
    DiagnosticId = "FrostyTempUnknownValueExceptionIsDeprecated")]
public sealed class UnknownValueException : Exception
{
    public UnknownValueException()
    {
    }

    public UnknownValueException(string message)
        : base(message)
    {
    }

    public UnknownValueException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <param name="valueName">What the value is.</param>
    /// <param name="value">The value that is unknown.</param>
    public UnknownValueException(string valueName, object? value)
        : base(string.Create(CultureInfo.InvariantCulture, $"Unknown value of {valueName}: {value}."))
    {
        ValueName = valueName;
        Value = value;
    }

    public string? ValueName { get; }

    public object? Value { get; }
}