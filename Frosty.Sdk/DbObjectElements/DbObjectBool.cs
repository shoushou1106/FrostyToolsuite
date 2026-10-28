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

    protected override void InternalSerialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        accessor.WriteByte((byte)(_value ? 1 : 0));
    }

    protected override void InternalDeserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _value = accessor.ReadByte() != 0;
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}