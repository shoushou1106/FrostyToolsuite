using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

// ReSharper disable PreferConcreteValueOverDefault

namespace Frosty.Sdk.DbObjects;

/// <remarks>
///     Keys ignore case because Frosty v1 does.
///     A child is serialized with its own <see cref="DbObject.Name" />,
///     which is its key when read back.
/// </remarks>
public sealed class DbObjectDict : DbObject
{
    private readonly Dictionary<string, DbObject> _items;

    internal DbObjectDict(Type type)
        : base(type)
    {
        _items = new Dictionary<string, DbObject>(StringComparer.OrdinalIgnoreCase);
    }

    internal DbObjectDict(int capacity)
        : base(Type.Dict | Type.Anonymous)
    {
        _items = new Dictionary<string, DbObject>(capacity, StringComparer.OrdinalIgnoreCase);
    }

    internal DbObjectDict(string name, int capacity)
        : base(Type.Dict, name)
    {
        _items = new Dictionary<string, DbObject>(capacity, StringComparer.OrdinalIgnoreCase);
    }

    public override bool IsDict()
    {
        return true;
    }

    public override DbObjectDict AsDict()
    {
        return this;
    }

    public DbObjectDict AsDict(string name)
    {
        return _items[name].AsDict();
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public DbObjectDict? AsDict(string name, DbObjectDict? defaultValue)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsDict() : defaultValue;
    }

    public DbObjectList AsList(string name)
    {
        return _items[name].AsList();
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public DbObjectList? AsList(string name, DbObjectList? defaultValue)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsList() : defaultValue;
    }

    public bool AsBoolean(string name, bool defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsBoolean() : defaultValue;
    }

    public string AsString(string name, string defaultValue = "")
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsString() : defaultValue;
    }

    public int AsInt(string name, int defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsInt() : defaultValue;
    }

    public uint AsUInt(string name, uint defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsUInt() : defaultValue;
    }

    public long AsLong(string name, long defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsLong() : defaultValue;
    }

    public ulong AsULong(string name, ulong defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsULong() : defaultValue;
    }

    public float AsFloat(string name, float defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsFloat() : defaultValue;
    }

    public double AsDouble(string name, double defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsDouble() : defaultValue;
    }

    public Guid AsGuid(string name, Guid defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsGuid() : defaultValue;
    }

    public Sha1 AsSha1(string name, Sha1 defaultValue = default)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsSha1() : defaultValue;
    }

    public byte[] AsBlob(string name)
    {
        return _items[name].AsBlob();
    }

    [return: NotNullIfNotNull(nameof(defaultValue))]
    public byte[]? AsBlob(string name, byte[]? defaultValue)
    {
        return _items.TryGetValue(name, out DbObject? item) ? item.AsBlob() : defaultValue;
    }

    public bool ContainsKey(string name)
    {
        return _items.ContainsKey(name);
    }

    /// <exception cref="ArgumentException"><paramref name="value" /> is not named <paramref name="name" />.</exception>
    public void Set(string name, DbObjectDict value)
    {
        ThrowIfNameDiffers(name, value);
        _items[name] = value;
    }

    /// <exception cref="ArgumentException"><paramref name="value" /> is not named <paramref name="name" />.</exception>
    public void Set(string name, DbObjectList value)
    {
        ThrowIfNameDiffers(name, value);
        _items[name] = value;
    }

    public void Set(string name, bool value)
    {
        _items[name] = new DbObjectBool(name, value);
    }

    public void Set(string name, string value)
    {
        _items[name] = new DbObjectString(name, value);
    }

    public void Set(string name, int value)
    {
        _items[name] = new DbObjectInt(name, value);
    }

    public void Set(string name, uint value)
    {
        _items[name] = new DbObjectInt(name, (int)value);
    }

    public void Set(string name, long value)
    {
        _items[name] = new DbObjectLong(name, value);
    }

    public void Set(string name, ulong value)
    {
        _items[name] = new DbObjectLong(name, (long)value);
    }

    public void Set(string name, float value)
    {
        _items[name] = new DbObjectFloat(name, value);
    }

    public void Set(string name, double value)
    {
        _items[name] = new DbObjectDouble(name, value);
    }

    public void Set(string name, Guid value)
    {
        _items[name] = new DbObjectGuid(name, value);
    }

    public void Set(string name, Sha1 value)
    {
        _items[name] = new DbObjectSha1(name, value);
    }

    public void Set(string name, byte[] value)
    {
        _items[name] = new DbObjectBlob(name, value);
    }

    public bool Remove(string name)
    {
        return _items.Remove(name);
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        SerializeChildren(accessor, _items.Values);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        // The size of the children is not needed, they end with a terminator
        accessor.Read7BitEncodedInt64();

        while (Deserialize(accessor) is { } child)
        {
            _items.Add(child.Name, child);
        }
    }

    private static void ThrowIfNameDiffers(string name, DbObject value)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (!string.Equals(value.Name, name, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture,
                    $"The value is named '{value.Name}' but stored as '{name}'."), nameof(value));
        }
    }
}