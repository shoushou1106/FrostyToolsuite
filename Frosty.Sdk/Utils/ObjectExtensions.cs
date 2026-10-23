using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.Utils;

public static class ObjectExtensions
{
    // CA1034: Do not nest type
#pragma warning disable CA1034
    extension(object obj)
    {
        public T GetProperty<T>(string inPropertyName)
        {
            ArgumentNullException.ThrowIfNull(obj);

            Type type = obj.GetType();
            PropertyInfo? property = type.GetProperty(inPropertyName);
            if (property is null)
            {
                throw new MissingMemberException($"Property {inPropertyName} is not found in type {type}");
            }

            object? value = property.GetValue(obj);
            return value switch
            {
                T result => result,
                IPrimitive primitive when primitive.ToActualType() is T result2 => result2,
                _ => throw new InvalidCastException()
            };
        }

        public bool TryGetProperty<T>(string inPropertyName, [NotNullWhen(true)] out T? result)
        {
            ArgumentNullException.ThrowIfNull(obj);

            Type type = obj.GetType();
            PropertyInfo? property = type.GetProperty(inPropertyName);
            if (property is null)
            {
                result = default;
                return false;
            }

            object? value = property.GetValue(obj);
            switch (value)
            {
                case T temp:
                    result = temp;
                    return true;
                case IPrimitive primitive when primitive.ToActualType() is T:
                    // TODO: Verify whether the IPrimitive branch should return true after assigning result.
                    break;
            }

            result = default;
            return false;
        }
    }
#pragma warning restore CA1034
}