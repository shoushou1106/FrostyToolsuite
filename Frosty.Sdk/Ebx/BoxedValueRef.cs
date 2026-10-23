using System.Diagnostics.CodeAnalysis;

using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Ebx;

public struct BoxedValueRef(object? inValue, TypeFlags inFlags) : IEquatable<BoxedValueRef>
{
    public object? Value { get; private set; } = inValue;
    public TypeFlags.TypeEnum Type => _flags.GetTypeEnum();
    public TypeFlags.CategoryEnum Category => _flags.GetCategoryEnum();

    private TypeFlags _flags = inFlags;

    private const string CollectionName = "ObservableCollection`1";

    public void SetValue(object inValue)
    {
        Value = inValue;
    }

    public override string ToString()
    {
        if (Value is null)
        {
            return "BoxedValueRef '(null)'";
        }

        Type type = Value.GetType();

        return
            $"BoxedValueRef '{(type.Name == CollectionName ? $"Array<{type.GenericTypeArguments[0].GetName()}>" : type == typeof(PointerRef) ? "Class" : type.GetName())}'";
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is BoxedValueRef b && Equals(b);
    }

    public bool Equals(BoxedValueRef other)
    {
        return Value == other.Value && _flags == other._flags;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, (ushort)_flags);
    }

    public static bool operator ==(BoxedValueRef a, object? b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(BoxedValueRef a, object? b)
    {
        return !a.Equals(b);
    }
}