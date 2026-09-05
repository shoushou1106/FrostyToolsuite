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
        return new DbObjectListEnum(_items);
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

    protected override void InternalSerialize(DataStream? stream)
    {
        Block<byte> sub = new(0);
        using (BlockStream subStream = new(sub, true))
        {
            foreach (DbObject value in _items)
            {
                Serialize(subStream, value);
            }

            // write terminator
            subStream.WriteByte((byte)Type.Null);
        }

        stream?.Write7BitEncodedInt64(sub.Size);
        stream?.Write(sub);
        sub.Dispose();
    }

    protected override void InternalDeserialize(DataStream? stream)
    {
        stream?.Read7BitEncodedInt64();
        while (true)
        {
            DbObject? obj = Deserialize(stream);

            if (obj is null)
            {
                break;
            }

            _items.Add(obj);
        }
    }

    private class DbObjectListEnum(List<DbObject> inItems) : IEnumerator<DbObject>
    {
        // Enumerators are positioned before the first element
        // until the first MoveNext() call.
        private int _position = -1;

        public bool MoveNext()
        {
            _position++;
            return _position < inItems.Count;
        }

        public void Reset()
        {
            _position = -1;
        }

        object IEnumerator.Current => Current;

        public DbObject Current
        {
            get
            {
                try
                {
                    return inItems[_position];
                }
                catch (IndexOutOfRangeException)
                {
                    throw new InvalidOperationException();
                }
            }
        }

        public void Dispose()
        {
        }
    }
}