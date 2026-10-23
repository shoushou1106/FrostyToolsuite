using System.Diagnostics.CodeAnalysis;

namespace Frosty.Sdk.Attributes;

[AttributeUsage(AttributeTargets.Struct)]
public sealed class FunctionAttribute(params string[] argumentTypes) : Attribute, IEquatable<FunctionAttribute>
{
    public string[] ArgumentTypes { get; } = argumentTypes;

    public bool Equals([NotNullWhen(true)] FunctionAttribute? other)
    {
        return other is not null && ArgumentTypes.AsSpan().SequenceEqual(other.ArgumentTypes);
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FunctionAttribute other && Equals(other);
    }

    public override int GetHashCode()
    {
        HashCode hash = new();
        foreach (string argumentType in ArgumentTypes)
        {
            hash.Add(argumentType);
        }

        return hash.ToHashCode();
    }
}
