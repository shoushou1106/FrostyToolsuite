using System.Collections;

using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.DbObjectElements;

public class DbObjectList : DbObject, IEnumerable<DbObject>
{
    private readonly List<DbObject> _items;

    protected internal DbObjectList(Type inType)
        : base(inType)
    {
        _items = [];
    }

    protected internal DbObjectList(int inCapacity)
        : base(Type.List | Type.Anonymous)
    {
        _items = new List<DbObject>(inCapacity);
    }

    protected internal DbObjectList(string inName, int inCapacity)
        : base(Type.List, inName)
    {
        _items = new List<DbObject>(inCapacity);
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

    protected override void InternalSerialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        Block<byte> sub = new(0);
        using (BlockAccessor subAccessor = new(sub, true))
        {
            foreach (DbObject value in _items)
            {
                Serialize(subAccessor, value);
            }

            // Write terminator
            subAccessor.WriteByte((byte)Type.Null);
        }

        accessor.Write7BitEncodedInt64(sub.Size);
        accessor.Write(sub);
        sub.Dispose();
    }

    protected override void InternalDeserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        accessor.Read7BitEncodedInt64();
        while (true)
        {
            DbObject? obj = Deserialize(accessor);

            if (obj is null)
            {
                break;
            }

            _items.Add(obj);
        }
    }
}
