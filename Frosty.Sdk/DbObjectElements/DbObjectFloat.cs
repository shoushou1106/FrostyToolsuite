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

    protected override void InternalSerialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        accessor.WriteSingle(_value);
    }

    protected override void InternalDeserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _value = accessor.ReadSingle();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.CurrentCulture);
    }
}