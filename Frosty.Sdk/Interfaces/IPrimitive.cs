namespace Frosty.Sdk.Interfaces;

public interface IPrimitive
{
    object ToActualType();
    void FromActualType(object value);
}