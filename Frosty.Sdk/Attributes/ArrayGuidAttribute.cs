namespace Frosty.Sdk.Attributes;

[AttributeUsage(FrostyAttributeTargets.Type, Inherited = false)]
// CA1019: Define accessors for attribute arguments
// The GUID is passed as a string because Guid is not a valid attribute argument type.
// CA1720: Identifier 'guid' contains type name
// It is a GUID.
#pragma warning disable CA1019, CA1720
public sealed class ArrayGuidAttribute(string guid) : Attribute
#pragma warning restore CA1019, CA1720
{
    // CA1720: Identifier 'guid' contains type name
    // It is a GUID.
#pragma warning disable CA1720
    public Guid Guid { get; } = Guid.Parse(guid);
#pragma warning restore CA1720
}