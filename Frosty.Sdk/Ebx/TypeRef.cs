using System.Diagnostics.CodeAnalysis;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.Ebx;

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

    public TypeRef(string inName)
    {
        if (inName == "null")
        {
            InternalType = null;
            return;
        }

        InternalType = TypeLibrary.GetType(inName);
    }

    public TypeRef(Guid typeGuid)
    {
        InternalType = TypeLibrary.GetType(typeGuid);
    }

    public TypeRef(IType? inType)
    {
        InternalType = inType;
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
        return obj is TypeRef b && Equals(b);
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

    public static bool operator ==(TypeRef a, object? b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(TypeRef a, object? b)
    {
        return !a.Equals(b);
    }

    public override string ToString()
    {
        return $"TypeRef '{(IsNull() ? "(null)" : Name)}'";
    }
}