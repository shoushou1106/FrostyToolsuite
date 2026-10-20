namespace Frosty.Sdk.Exceptions;

public class MissingEncryptionKeyException : Exception
{
    public MissingEncryptionKeyException()
    {
    }

    public MissingEncryptionKeyException(string inKey)
        : base($"Missing encryption key for {inKey}")
    {
    }

    public MissingEncryptionKeyException(string inKey, Exception innerException)
        : base($"Missing encryption key for {inKey}", innerException)
    {
    }
}