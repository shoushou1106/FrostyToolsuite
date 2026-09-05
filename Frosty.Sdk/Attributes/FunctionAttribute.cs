using System.Diagnostics.CodeAnalysis;

namespace Frosty.Sdk.Attributes;

/// <remarks>
///     TODO: This file is likely not finished. Suggest adding sealed, dropping protected, and use IEquatable.
/// </remarks>
[AttributeUsage(AttributeTargets.Struct)]
public class FunctionAttribute(params string[] argumentTypes) : Attribute
{
    public string[] ArgumentTypes { get; } = argumentTypes;

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is FunctionAttribute other && other.ArgumentTypes.SequenceEqual(ArgumentTypes);
    }

    protected bool Equals(FunctionAttribute other)
    {
        return base.Equals(other) && ArgumentTypes.Equals(other.ArgumentTypes);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(base.GetHashCode(), ArgumentTypes);
    }
}