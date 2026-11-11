using System.Diagnostics;
using Frosty.Sdk.Exceptions;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.DbObjects;

public abstract class DbObject
{
    private readonly Type _type;

    private protected DbObject(Type type)
    {
        _type = type;
    }

    private protected DbObject(Type type, string name)
        : this(type)
    {
        ArgumentNullException.ThrowIfNull(name);

        Name = name;
    }

    public string Name { get; private set; } = string.Empty;

    /// <summary>
    ///     Serializes a <see cref="DbObject" /> to a file.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8 (names and string values)</remarks>
    /// <param name="path">The path of the file.</param>
    /// <param name="value">The <see cref="DbObject" /> to serialize.</param>
    public static void Serialize(string path, DbObject value)
    {
        using DataAccessor accessor = new(new FileStream(path, FileMode.Create, FileAccess.ReadWrite));
        Serialize(accessor, value);
    }

    /// <summary>
    ///     Serializes a <see cref="DbObject" /> to a <see cref="DataAccessor" />.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8 (names and string values)</remarks>
    /// <param name="accessor">The <see cref="DataAccessor" /> to serialize the <see cref="DbObject" /> to.</param>
    /// <param name="value">The <see cref="DbObject" /> to serialize.</param>
    public static void Serialize(DataAccessor accessor, DbObject value)
    {
        ArgumentNullException.ThrowIfNull(accessor);
        ArgumentNullException.ThrowIfNull(value);

        accessor.WriteByte((byte)value._type);

        if (!value._type.HasFlag(Type.Anonymous)) accessor.WriteNullTerminatedString(value.Name);

        value.InternalSerialize(accessor);
    }

    /// <summary>
    ///     Deserializes a <see cref="DbObject" /> from a file, deobfuscating it first if needed.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8 (names and string values)</remarks>
    /// <param name="path">The path of the file.</param>
    /// <returns>The deserialized <see cref="DbObject" />.</returns>
    public static DbObject? Deserialize(string path)
    {
        using DataAccessor accessor = new(ObfuscatedFile.OpenRead(path));
        return Deserialize(accessor);
    }

    /// <summary>
    ///     Deserializes a <see cref="DbObject" /> from a <see cref="DataAccessor" />.
    /// </summary>
    /// <remarks><b>Encoding:</b> UTF-8 (names and string values)</remarks>
    /// <param name="accessor">The <see cref="DataAccessor" /> to deserialize the <see cref="DbObject" /> from.</param>
    /// <returns>The deserialized <see cref="DbObject" />, or <see langword="null" /> for a terminator.</returns>
    public static DbObject? Deserialize(DataAccessor accessor)
    {
        ArgumentNullException.ThrowIfNull(accessor);

        var type = (Type)accessor.ReadByte();

        var obj = CreateDbObject(type);

        if (obj is null) return obj;

        if (!type.HasFlag(Type.Anonymous)) obj.Name = accessor.ReadNullTerminatedString();

        obj.InternalDeserialize(accessor);

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
        return [with(capacity)];
    }

    public static DbObjectList CreateList(string name, int capacity = 0)
    {
        return [with(name, capacity)];
    }

    private protected abstract void InternalSerialize(DataAccessor accessor);

    private protected abstract void InternalDeserialize(DataAccessor accessor);

    /// <summary>
    ///     Writes the size of <paramref name="children" />, the children and a <see cref="Type.Null" /> terminator.
    /// </summary>
    /// <remarks>
    ///     The size is a 7-bit encoded int in front of the children, so its length is only known after the children
    ///     are serialized into a buffer.
    /// </remarks>
    private protected static void SerializeChildren(DataAccessor accessor, IEnumerable<DbObject> children)
    {
        using MemoryStream buffer = new();
        using DataAccessor bufferAccessor = new(buffer, true);

        foreach (var child in children) Serialize(bufferAccessor, child);

        bufferAccessor.WriteByte((byte)Type.Null);

        accessor.Write7BitEncodedInt64(buffer.Length);
        accessor.Write(buffer.GetBuffer().AsSpan(0, (int)buffer.Length));
    }

    private static DbObject? CreateDbObject(Type type)
    {
        return (type & ~Type.Anonymous) switch
        {
            Type.Null => null,
            Type.List => new DbObjectList(type),
            Type.Dict => new DbObjectDict(type),
            Type.Boolean => new DbObjectBool(type),
            Type.String => new DbObjectString(type),
            Type.Int => new DbObjectInt(type),
            Type.Long => new DbObjectLong(type),
            Type.Float => new DbObjectFloat(type),
            Type.Double => new DbObjectDouble(type),
            Type.Guid => new DbObjectGuid(type),
            Type.Sha1 => new DbObjectSha1(type),
            Type.Blob => new DbObjectBlob(type),
            Type.Anonymous => throw new UnreachableException(),
            _ => throw new UnknownValueException<Type>("DbObject " + nameof(Type), type)
        };
    }
    
    [Flags]
    internal enum Type
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
}