using System.Globalization;

using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectInt : DbObject
{
    private int _value;

    protected internal DbObjectInt(Type inType)
        : base(inType)
    {
    }

    public DbObjectInt(int inValue)
        : base(Type.Int | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectInt(string inName, int inValue)
        : base(Type.Int, inName)
    {
        _value = inValue;
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
        // TODO: Possible typo, (ulong) instead of (uint), delete this comment after fix / if its not
        return (uint)_value;
    }

    protected override void InternalSerialize(DataStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        stream.WriteInt32(_value);
    }

    protected override void InternalDeserialize(DataStream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);

        _value = stream.ReadInt32();
    }

    public override string ToString()
    {
        return _value.ToString(CultureInfo.InvariantCulture);
    }
}