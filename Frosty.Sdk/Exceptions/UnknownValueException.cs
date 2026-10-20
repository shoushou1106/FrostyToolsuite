namespace Frosty.Sdk.Exceptions;

public class UnknownValueException<T> : Exception
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

    public UnknownValueException(string inName, T inValue)
        : base($"Unknown value of {inName}: {inValue}.")
    {
    }
}