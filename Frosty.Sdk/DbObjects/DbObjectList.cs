using System.Collections;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.DbObjects;

public sealed class DbObjectList : DbObject, IEnumerable<DbObject>
{
    private readonly List<DbObject> _items;

    internal DbObjectList(Type type)
        : base(type)
    {
        _items = [];
    }

    internal DbObjectList(int capacity)
        : base(Type.List | Type.Anonymous)
    {
        _items = new List<DbObject>(capacity);
    }

    internal DbObjectList(string name, int capacity)
        : base(Type.List, name)
    {
        _items = new List<DbObject>(capacity);
    }

    public int Count => _items.Count;

    public DbObject this[int index]
    {
        get => _items[index];
        set => _items[index] = value;
    }

    public IEnumerator<DbObject> GetEnumerator()
    {
        return _items.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }

    public override bool IsList()
    {
        return true;
    }

    public override DbObjectList AsList()
    {
        return this;
    }

    public void Add(DbObjectDict value)
    {
        _items.Add(value);
    }

    public void Add(DbObjectList value)
    {
        _items.Add(value);
    }

    public void Add(bool value)
    {
        _items.Add(new DbObjectBool(value));
    }

    public void Add(string value)
    {
        _items.Add(new DbObjectString(value));
    }

    public void Add(int value)
    {
        _items.Add(new DbObjectInt(value));
    }

    public void Add(uint value)
    {
        _items.Add(new DbObjectInt((int)value));
    }

    public void Add(long value)
    {
        _items.Add(new DbObjectLong(value));
    }

    public void Add(ulong value)
    {
        _items.Add(new DbObjectLong((long)value));
    }

    public void Add(float value)
    {
        _items.Add(new DbObjectFloat(value));
    }

    public void Add(double value)
    {
        _items.Add(new DbObjectDouble(value));
    }

    public void Add(Guid value)
    {
        _items.Add(new DbObjectGuid(value));
    }

    public void Add(Sha1 value)
    {
        _items.Add(new DbObjectSha1(value));
    }

    public void Add(byte[] value)
    {
        _items.Add(new DbObjectBlob(value));
    }

    private protected override void InternalSerialize(DataAccessor accessor)
    {
        SerializeChildren(accessor, _items);
    }

    private protected override void InternalDeserialize(DataAccessor accessor)
    {
        // The size of the children is not needed, they end with a terminator
        accessor.Read7BitEncodedInt64();

        while (Deserialize(accessor) is { } child) _items.Add(child);
    }
}