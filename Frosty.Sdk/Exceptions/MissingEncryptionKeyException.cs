using Frosty.Sdk.Managers;

namespace Frosty.Sdk.Exceptions;

/// <summary>
///     The exception that is thrown when data is encrypted and its key is not loaded in <see cref="KeyManager" />.
/// </summary>
public sealed class MissingEncryptionKeyException : Exception
{
    public MissingEncryptionKeyException()
        : base("An encryption key is not loaded.")
    {
    }

    public MissingEncryptionKeyException(string message)
        : base(message)
    {
    }

    public MissingEncryptionKeyException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    ///     The ID of the missing key in <see cref="KeyManager" />, or <see langword="null" /> if unknown.
    /// </summary>
    public string? KeyId { get; private init; }

    /// <summary>
    ///     Create the exception for a key that is not loaded.
    /// </summary>
    /// <param name="keyId">The ID of the key in <see cref="KeyManager" />.</param>
    public static MissingEncryptionKeyException ForKey(string keyId)
    {
        return new MissingEncryptionKeyException($"The encryption key \"{keyId}\" is not loaded.") { KeyId = keyId };
    }
}
