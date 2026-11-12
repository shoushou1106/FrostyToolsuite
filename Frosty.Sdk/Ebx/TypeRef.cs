using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     A value of an EBX field that holds a type, or <c>null</c>.
/// </summary>
public readonly struct TypeRef : IEquatable<TypeRef>
{
    public string? Name => InternalType?.Name;

    // CA1720: Identifiers should not contain type names
#pragma warning disable CA1720
    public Guid Guid => InternalType?.Guid ?? Guid.Empty;
#pragma warning restore CA1720

    public Type? Type => InternalType?.Type;

    internal IType? InternalType { get; }

    public TypeRef()
    {
    }

    /// <param name="name">The name of the type, or <c>null</c> for no type.</param>
    public TypeRef(string name)
    {
        if (name == "null")
        {
            InternalType = null;
            return;
        }

        InternalType = TypeLibrary.GetType(name);
    }

    public TypeRef(Guid typeGuid)
    {
        InternalType = TypeLibrary.GetType(typeGuid);
    }

    public TypeRef(IType? type)
    {
        InternalType = type;
    }

    public static implicit operator string(TypeRef value)
    {
        return value.Name ?? "null";
    }

    public static implicit operator TypeRef(string value)
    {
        return new TypeRef(value);
    }

    public static implicit operator TypeRef(Guid typeGuid)
    {
        return new TypeRef(typeGuid);
    }

    public static TypeRef FromString(string value)
    {
        return new TypeRef(value);
    }

    public static TypeRef FromGuid(Guid typeGuid)
    {
        return new TypeRef(typeGuid);
    }

    public bool IsNull()
    {
        return InternalType is null;
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is TypeRef other && Equals(other);
    }

    public bool Equals(TypeRef other)
    {
        return Guid == other.Guid;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Guid);
    }

    public static bool operator ==(TypeRef a, TypeRef b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(TypeRef a, TypeRef b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"TypeRef '{(IsNull() ? "(null)" : Name)}'");
    }
}