using Frosty.Sdk.DbObjectElements;
using Frosty.Sdk.Exceptions;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk;

public abstract class DbObject(DbObject.Type inType)
{
    private readonly Type _type = inType;

    protected DbObject(Type inType, string inName)
        : this(inType)
    {
        Name = inName;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    ///     Serializes a <see cref="DbObject" /> to a file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <param name="value">The <see cref="DbObject" /> to serialize.</param>
    public static void Serialize(string path, DbObject value)
    {
        using DataStream stream = new(new FileStream(path, FileMode.Create, FileAccess.ReadWrite));
        Serialize(stream, value);
    }

    /// <summary>
    ///     Serializes a <see cref="DbObject" /> to a <see cref="DataStream" />.
    /// </summary>
    /// <param name="stream">The <see cref="DataStream" /> to serialize the <see cref="DbObject" /> to.</param>
    /// <param name="value">The <see cref="DbObject" /> to serialize.</param>
    public static void Serialize(DataStream? stream, DbObject? value)
    {
        // TODO: Null check decision pending on throw or ignore.
        if (stream is null || value is null)
        {
            throw new NotImplementedException("TODO: Null check decision pending on throw or ignore.");
        }

        stream.WriteByte((byte)value._type);

        if (!value._type.HasFlag(Type.Anonymous))
        {
            stream.WriteNullTerminatedString(value.Name);
        }

        value.InternalSerialize(stream);
    }

    /// <summary>
    ///     Deserializes a <see cref="DbObject" /> from a file.
    /// </summary>
    /// <param name="path">The path of the file.</param>
    /// <returns>The deserialized <see cref="DbObject" />.</returns>
    public static DbObject? Deserialize(string path)
    {
        using BlockStream stream = BlockStream.FromFile(path, true);
        return Deserialize(stream);
    }

    /// <summary>
    ///     Deserializes a <see cref="DbObject" /> from a <see cref="DataStream" />.
    /// </summary>
    /// <param name="stream">The <see cref="DataStream" /> to deserialize the <see cref="DbObject" /> from.</param>
    /// <returns>The deserialized <see cref="DbObject" />.</returns>
    public static DbObject? Deserialize(DataStream? stream)
    {
        // TODO: Null check decision pending on throw or ignore.
        if (stream is null)
        {
            throw new NotImplementedException("TODO: Null check decision pending on throw or ignore.");
        }

        Type type = (Type)stream.ReadByte();

        DbObject? obj = CreateDbObject(type);

        if (obj is null)
        {
            return obj;
        }

        if (!type.HasFlag(Type.Anonymous))
        {
            obj.Name = stream.ReadNullTerminatedString();
        }

        obj.InternalDeserialize(stream);

        return obj;
    }

    public virtual bool IsDict()
    {
        return false;
    }

    public virtual DbObjectDict AsDict()
    {
        throw new InvalidCastException();
    }

    public virtual bool IsList()
    {
        return false;
    }

    public virtual DbObjectList AsList()
    {
        throw new InvalidCastException();
    }

    public virtual bool AsBoolean()
    {
        throw new InvalidCastException();
    }

    public virtual string AsString()
    {
        throw new InvalidCastException();
    }

    public virtual int AsInt()
    {
        throw new InvalidCastException();
    }

    public virtual uint AsUInt()
    {
        throw new InvalidCastException();
    }

    public virtual long AsLong()
    {
        throw new InvalidCastException();
    }

    public virtual ulong AsULong()
    {
        throw new InvalidCastException();
    }

    public virtual float AsFloat()
    {
        throw new InvalidCastException();
    }

    public virtual double AsDouble()
    {
        throw new InvalidCastException();
    }

    public virtual Guid AsGuid()
    {
        throw new InvalidCastException();
    }

    public virtual Sha1 AsSha1()
    {
        throw new InvalidCastException();
    }

    public virtual byte[] AsBlob()
    {
        throw new InvalidCastException();
    }

    public static DbObjectDict CreateDict(int capacity = 0)
    {
        return new DbObjectDict(capacity);
    }

    public static DbObjectDict CreateDict(string name, int capacity = 0)
    {
        return new DbObjectDict(name, capacity);
    }

    public static DbObjectList CreateList(int capacity = 0)
    {
        return new DbObjectList(capacity);
    }

    public static DbObjectList CreateList(string name, int capacity = 0)
    {
        return new DbObjectList(name, capacity);
    }

    protected abstract void InternalSerialize(DataStream? stream);

    protected abstract void InternalDeserialize(DataStream? stream);

    private static DbObject? CreateDbObject(Type type)
    {
        DbObject obj;
        // ReSharper disable once SwitchStatementHandlesSomeKnownEnumValuesWithDefault
        switch (type & ~Type.Anonymous)
        {
            case Type.Null:
                return null;
            case Type.List:
                obj = new DbObjectList(type);
                break;
            case Type.Dict:
                obj = new DbObjectDict(type);
                break;
            case Type.Boolean:
                obj = new DbObjectBool(type);
                break;
            case Type.String:
                obj = new DbObjectString(type);
                break;
            case Type.Int:
                obj = new DbObjectInt(type);
                break;
            case Type.Long:
                obj = new DbObjectLong(type);
                break;
            case Type.Float:
                obj = new DbObjectFloat(type);
                break;
            case Type.Double:
                obj = new DbObjectDouble(type);
                break;
            case Type.Guid:
                obj = new DbObjectGuid(type);
                break;
            case Type.Sha1:
                obj = new DbObjectSha1(type);
                break;
            case Type.Blob:
                obj = new DbObjectBlob(type);
                break;
            default:
                throw new UnknownValueException<Type>("DbObject " + nameof(Type), type);
        }

        return obj;
    }

    // CA1008: Enums should have zero value
    // CA1720: Identifiers should not contain type names
    // CA2217: Do not mark enums with FlagsAttribute
#pragma warning disable CA1008, CA1720, CA2217
    [Flags]
    protected internal enum Type
    {
        Null = 0,
        List = 1,
        Dict = 2,
        Boolean = 6,
        String = 7,
        Int = 8,
        Long = 9,
        Float = 11,
        Double = 12,
        Guid = 15,
        Sha1 = 16,
        Blob = 19,

        Anonymous = 1 << 7
    }
#pragma warning restore CA1008, CA1720, CA2217
}