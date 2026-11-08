using System.Globalization;
using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectLong : DbObject
{
    private long _value;

    internal DbObjectLong(Type type)
        : base(type)
    {
    }

    public DbObjectLong(long value)
        : base(Type.Long | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectLong(string name, long value)
        : base(Type.Long, name)
    {
        _value = value;
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

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteInt64(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadInt64();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }
}