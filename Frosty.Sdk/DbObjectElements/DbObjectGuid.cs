using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectGuid : DbObject
{
    private Guid _value;

    protected internal DbObjectGuid(Type inType)
        : base(inType)
    {
    }

    public DbObjectGuid(Guid inValue)
        : base(Type.Guid | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectGuid(string inName, Guid inValue)
        : base(Type.Guid, inName)
    {
        _value = inValue;
    }

    public override Guid AsGuid()
    {
        return _value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.WriteGuid(_value);
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        _value = stream.ReadGuid();
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}