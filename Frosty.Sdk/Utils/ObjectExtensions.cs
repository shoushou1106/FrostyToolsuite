using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

using Frosty.Sdk.Interfaces;

namespace Frosty.Sdk.Utils;

public static class ObjectExtensions
{
    extension(object obj)
    {
        public T GetProperty<T>(string propertyName)
        {
            ArgumentNullException.ThrowIfNull(obj);

            Type type = obj.GetType();
            PropertyInfo? property = type.GetProperty(propertyName);
            if (property is null)
            {
                throw new MissingMemberException(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Property {propertyName} is not found in type {type}"));
            }

            object? value = property.GetValue(obj);
            return value switch
            {
                T result => result,
                IPrimitive primitive when primitive.ToActualType() is T result2 => result2,
                _ => throw new InvalidCastException(
                    string.Create(CultureInfo.InvariantCulture,
                        $"Property {propertyName} of type {type} is not a {typeof(T)}"))
            };
        }

        public bool TryGetProperty<T>(string propertyName, [NotNullWhen(true)] out T? result)
        {
            ArgumentNullException.ThrowIfNull(obj);

            Type type = obj.GetType();
            PropertyInfo? property = type.GetProperty(propertyName);
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