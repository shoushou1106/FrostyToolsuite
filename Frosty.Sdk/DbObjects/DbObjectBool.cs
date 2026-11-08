using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectBool : DbObject
{
    private bool _value;

    internal DbObjectBool(Type type)
        : base(type)
    {
    }

    public DbObjectBool(bool value)
        : base(Type.Boolean | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectBool(string name, bool value)
        : base(Type.Boolean, name)
    {
        _value = value;
    }

    public override bool AsBoolean()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteByte((byte)(_value ? 1 : 0));
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadByte() != 0;
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}