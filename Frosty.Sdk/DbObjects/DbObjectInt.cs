using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectInt : DbObject
{
    private int _value;

    internal DbObjectInt(Type type)
        : base(type)
    {
    }

    public DbObjectInt(int value)
        : base(Type.Int | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectInt(string name, int value)
        : base(Type.Int, name)
    {
        _value = value;
    }

    public override int AsInt()
    {
        return _value;
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
        // Zero-extend, so the value matches AsUInt
        return AsUInt();
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteInt32(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadInt32();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }
}