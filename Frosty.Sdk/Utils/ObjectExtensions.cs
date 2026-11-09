using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.Utils;

public static class ObjectExtensions
{
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
                _ => throw new InvalidCastException(
                    $"Property {inPropertyName} of type {type} is not a {typeof(T)}")
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
                case IPrimitive primitive when primitive.ToActualType() is T actual:
                    result = actual;
                    return true;
            }

            result = default;
            return false;
        }
    }
}