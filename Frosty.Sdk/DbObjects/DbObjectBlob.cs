using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectBlob : DbObject
{
    private byte[] _value;

    internal DbObjectBlob(Type type)
        : base(type)
    {
        _value = [];
    }

    public DbObjectBlob(byte[] value)
        : base(Type.Blob | Type.Anonymous)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
    }

    public DbObjectBlob(string name, byte[] value)
        : base(Type.Blob, name)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
    }

    public override byte[] AsBlob()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.Write7BitEncodedInt32(_value.Length);
        accessor.Write(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = new byte[accessor.Read7BitEncodedInt32()];
        accessor.ReadExactly(_value);
    }

    /// <remarks>Shows only the length, a blob can be megabytes long.</remarks>
    public override string ToString()
    {
        return string.Create(CultureInfo.InvariantCulture, $"byte[{_value.Length}]");
    }
}