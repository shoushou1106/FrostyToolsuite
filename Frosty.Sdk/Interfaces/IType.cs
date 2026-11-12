using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Interfaces;

/// <summary>
///     A Frostbite type, either a type of the generated SDK or a type that is only known from a <c>TypeInfoAsset</c>.
/// </summary>
public interface IType
{
    /// <summary>
    ///     The Frostbite name of the type.
    /// </summary>
    string Name { get; }

    /// <summary>
    ///     The hash of <see cref="Name" /> that EBX files store.
    /// </summary>
    uint NameHash { get; }

    /// <summary>
    ///     The GUID of the type info.
    /// </summary>
    // CA1720: Identifiers should not contain type names
#pragma warning disable CA1720
    Guid Guid { get; }
#pragma warning restore CA1720

    /// <summary>
    ///     The signature that RIFF EBX files store for the type, or <see cref="uint.MaxValue" /> if unknown.
    /// </summary>
    uint Signature { get; }

    /// <summary>
    ///     The .NET type of the generated SDK.
    /// </summary>
    Type Type { get; }

    /// <summary>
    ///     Determines whether current type derives from the specified <paramref name="type" />, like <see cref="System.Type.IsSubclassOf" />.
    /// </summary>
    /// <returns><see langword="false" /> if both are the same type.</returns>
    bool IsSubclassOf(IType type);

    /// <summary>
    ///     Gets the flags of the type, include its <see cref="TypeFlags.TypeEnum" />.
    /// </summary>
    TypeFlags GetFlags();
}
