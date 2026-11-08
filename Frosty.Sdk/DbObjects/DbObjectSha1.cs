using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectSha1 : DbObject
{
    private Sha1 _value;

    internal DbObjectSha1(Type type)
        : base(type)
    {
    }

    public DbObjectSha1(Sha1 value)
        : base(Type.Sha1 | Type.Anonymous)
    {
        _value = value;
    }

    public DbObjectSha1(string name, Sha1 value)
        : base(Type.Sha1, name)
    {
        _value = value;
    }

    public override Sha1 AsSha1()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteSha1(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadSha1();
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}