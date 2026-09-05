using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectBool : DbObject
{
    private bool _value;

    protected internal DbObjectBool(Type inType)
        : base(inType)
    {
    }

    public DbObjectBool(bool inValue)
        : base(Type.Boolean | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectBool(string inName, bool inValue)
        : base(Type.Boolean, inName)
    {
        _value = inValue;
    }

    public override bool AsBoolean()
    {
        return _value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.WriteByte((byte)(_value ? 1 : 0));
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        _value = stream?.ReadByte() != 0;
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}