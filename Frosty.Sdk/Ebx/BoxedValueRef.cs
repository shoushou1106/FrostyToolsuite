using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Ebx;

/// <summary>
///     A value of an EBX field whose type is stored next to the value, like a boxed <see cref="object" />.
/// </summary>
public struct BoxedValueRef(object? value, TypeFlags flags) : IEquatable<BoxedValueRef>
{
    private const string CollectionName = "ObservableCollection`1";

    private TypeFlags _flags = flags;

    public object? Value { get; private set; } = value;

    public TypeFlags.TypeEnum Type => _flags.GetTypeEnum();

    public TypeFlags.CategoryEnum Category => _flags.GetCategoryEnum();

    public static bool operator ==(BoxedValueRef a, BoxedValueRef b)
    {
        return a.Equals(b);
    }

    public static bool operator !=(BoxedValueRef a, BoxedValueRef b)
    {
        return !a.Equals(b);
    }

    public void SetValue(object value)
    {
        Value = value;
    }

    public override string ToString()
    {
        if (Value is null)
        {
            return "BoxedValueRef '(null)'";
        }

        Type type = Value.GetType();
        string typeName = type.Name == CollectionName
            ? string.Create(CultureInfo.InvariantCulture, $"Array<{type.GenericTypeArguments[0].GetName()}>")
            : type == typeof(PointerRef)
                ? "Class"
                : type.GetName();

        return string.Create(CultureInfo.InvariantCulture, $"BoxedValueRef '{typeName}'");
    }

    public override bool Equals([NotNullWhen(true)] object? obj)
    {
        return obj is BoxedValueRef other && Equals(other);
    }

    public bool Equals(BoxedValueRef other)
    {
        return Equals(Value, other.Value) && _flags == other._flags;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(Value, (ushort)_flags);
    }
}
