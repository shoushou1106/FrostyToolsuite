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

    protected override void InternalSerialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        accessor.Write7BitEncodedInt32(_value.Length);
        accessor.Write(_value);
    }

    protected override void InternalDeserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _value = new byte[accessor.Read7BitEncodedInt32()];
        accessor.ReadExactly(_value);
    }

    public override string? ToString()
    {
        return _value.ToString();
    }
}