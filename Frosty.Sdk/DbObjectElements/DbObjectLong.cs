using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectLong : DbObject
{
    private long _value;

    protected internal DbObjectLong(Type inType)
        : base(inType)
    {
    }

    public DbObjectLong(long inValue)
        : base(Type.Long | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectLong(string inName, long inValue)
        : base(Type.Long, inName)
    {
        _value = inValue;
    }

    public override int AsInt()
    {
        return (int)_value;
    }

    public override uint AsUInt()
    {
        return (uint)_value;
    }

    public override long AsLong()
    {
        return _value;
    }

    public override ulong AsULong()
    {
        return (ulong)_value;
    }

    protected override void InternalSerialize(DataStream? stream)
    {
        stream?.WriteInt64(_value);
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        if (stream is null)
        {
            return;
        }

        _value = stream.ReadInt64();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }
}