using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectFloat : DbObject
{
    private float _value;

    internal DbObjectFloat(Type type)
        : base(type)
    {
    }

    public DbObjectFloat(float value)
        : base(Type.Float | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectFloat(string name, float value)
        : base(Type.Float, name)
    {
        _value = value;
    }

    public override float AsFloat()
    {
        return _value;
    }

    public override double AsDouble()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteSingle(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadSingle();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }
}