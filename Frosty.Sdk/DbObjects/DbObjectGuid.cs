using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectGuid : DbObject
{
    private Guid _value;

    internal DbObjectGuid(Type type)
        : base(type)
    {
    }

    public DbObjectGuid(Guid value)
        : base(Type.Guid | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectGuid(string name, Guid value)
        : base(Type.Guid, name)
    {
        _value = value;
    }

    public override Guid AsGuid()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteGuid(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadGuid();
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}