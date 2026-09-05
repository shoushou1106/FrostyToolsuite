using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectString : DbObject
{
    private string _value;

    protected internal DbObjectString(Type inType)
        : base(inType)
    {
        _value = string.Empty;
    }

    public DbObjectString(string inValue)
        : base(Type.String | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectString(string inName, string inValue)
        : base(Type.String, inName)
    {
        _value = inValue;
    }

    public override string AsString()
    {
        return _value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.WriteSizedString(_value);
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        _value = stream.ReadSizedString();
    }

    public override string ToString()
    {
        return _value;
    }
}