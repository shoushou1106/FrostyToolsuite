namespace Frosty.Sdk.Attributes;

/// <summary>
///     Specifies the guid for the class. Used when looking up type refs
/// </summary>
[AttributeUsage(FrostyAttributeTargets.Type, Inherited = false)]
#pragma warning disable CA1019
public sealed class GuidAttribute(string inGuid) : Attribute
#pragma warning restore CA1019
{
    public Guid Guid { get; set; } = Guid.Parse(inGuid);
}