using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectDouble : DbObject
{
    private double _value;

    protected internal DbObjectDouble(Type inType)
        : base(inType)
    {
    }

    public DbObjectDouble(double inValue)
        : base(Type.Double | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectDouble(string inName, double inValue)
        : base(Type.Double, inName)
    {
        _value = inValue;
    }

    public override float AsFloat()
    {
        return (float)_value;
    }

    public override double AsDouble()
    {
        return _value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.WriteDouble(_value);
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        _value = stream.ReadDouble();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.CurrentCulture);
    }
}