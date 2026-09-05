namespace Frosty.Sdk.Attributes;

/// <summary>
///     Overrides the display name of the property/class in the Property Grid
/// </summary>
[AttributeUsage(FrostyAttributeTargets.Type | FrostyAttributeTargets.Field, Inherited = false)]
public sealed class DisplayNameAttribute(string name) : Attribute
{
    public string Name { get; internal set; } = name;
}