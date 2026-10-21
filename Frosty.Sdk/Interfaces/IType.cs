using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Interfaces;

public interface IType
{
    string Name { get; }
    uint NameHash { get; }

    // CA1720: Identifiers should not contain type names
#pragma warning disable CA1720
    Guid Guid { get; }
#pragma warning restore CA1720

    uint Signature { get; }
    Type Type { get; }

    bool IsSubClassOf(IType inType);

    TypeFlags GetFlags();
}