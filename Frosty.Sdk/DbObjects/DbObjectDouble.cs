using System.Globalization;
using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectDouble : DbObject
{
    private double _value;

    internal DbObjectDouble(Type type)
        : base(type)
    {
    }

    public DbObjectDouble(double value)
        : base(Type.Double | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectDouble(string name, double value)
        : base(Type.Double, name)
    {
        _value = value;
    }

    public override float AsFloat()
    {
        return (float)_value;
    }

    public override double AsDouble()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteDouble(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadDouble();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }
}