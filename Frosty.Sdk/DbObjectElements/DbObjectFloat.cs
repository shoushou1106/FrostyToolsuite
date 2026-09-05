using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectFloat : DbObject
{
    private float _value;

    protected internal DbObjectFloat(Type inType)
        : base(inType)
    {
    }

    public DbObjectFloat(float inValue)
        : base(Type.Float | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectFloat(string inName, float inValue)
        : base(Type.Float, inName)
    {
        _value = inValue;
    }

    public override float AsFloat()
    {
        return _value;
    }

    public override double AsDouble()
    {
        return _value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.WriteSingle(_value);
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        _value = stream.ReadSingle();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.CurrentCulture);
    }
}