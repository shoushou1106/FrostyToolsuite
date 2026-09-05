using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectBlob : DbObject
{
    private byte[] _value;

    protected internal DbObjectBlob(Type inType)
        : base(inType)
    {
        _value = [];
    }

    public DbObjectBlob(byte[] inValue)
        : base(Type.Blob | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectBlob(string inName, byte[] inValue)
        : base(Type.Blob, inName)
    {
        _value = inValue;
    }

    public override byte[] AsBlob()
    {
        return _value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.Write7BitEncodedInt32(_value.Length);
        stream?.Write(_value);
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        _value = new byte[stream.Read7BitEncodedInt32()];
        stream.ReadExactly(_value);
    }

    public override string? ToString()
    {
        return _value.ToString();
    }
}