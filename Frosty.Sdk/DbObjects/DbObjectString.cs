using Frosty.Sdk.IO;

namespace Frosty.Sdk.DbObjects;

/// <remarks><b>Encoding:</b> UTF-8</remarks>
public sealed class DbObjectString : DbObject
{
    private string _value;

    internal DbObjectString(Type type)
        : base(type)
    {
        _value = string.Empty;
    }

    public DbObjectString(string value)
        : base(Type.String | Type.Anonymous)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
    }

    public DbObjectString(string name, string value)
        : base(Type.String, name)
    {
        ArgumentNullException.ThrowIfNull(value);

        _value = value;
    }

    public override string AsString()
    {
        return _value;
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        accessor.WriteSizedString(_value);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        _value = accessor.ReadSizedString();
    }

    public override string ToString()
    {
        return _value;
    }
}