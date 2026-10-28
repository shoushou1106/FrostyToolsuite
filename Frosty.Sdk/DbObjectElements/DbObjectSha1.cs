using Frosty.Sdk.IO;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectSha1 : DbObject
{
    private Sha1 _value;

    protected internal DbObjectSha1(Type inType)
        : base(inType)
    {
    }

    public DbObjectSha1(Sha1 inValue)
        : base(Type.Sha1 | Type.Anonymous)
    {
        _value = inValue;
    }

    public DbObjectSha1(string inName, Sha1 inValue)
        : base(Type.Sha1, inName)
    {
        _value = inValue;
    }

    public override Sha1 AsSha1()
    {
        return _value;
    }

    protected override void InternalSerialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        accessor.WriteSha1(_value);
    }

    protected override void InternalDeserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        _value = accessor.ReadSha1();
    }

    public override string ToString()
    {
        return _value.ToString();
    }
}