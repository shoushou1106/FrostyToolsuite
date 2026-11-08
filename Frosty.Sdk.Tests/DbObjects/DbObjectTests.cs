using Frosty.Sdk.DbObjects;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.DbObjects;

/// <summary>
///     Tests for the binary <see cref="DbObject" /> format.
/// </summary>
internal sealed class DbObjectTests
{
    private static byte[] ToBytes(DbObject value)
    {
        using MemoryStream stream = new();
        using DataAccessor accessor = new(stream, true);
        DbObject.Serialize(accessor, value);
        return stream.ToArray();
    }

    private static DbObject? FromBytes(byte[] data)
    {
        using DataAccessor accessor = new(new MemoryStream(data));
        return DbObject.Deserialize(accessor);
    }

    /// <summary>
    ///     Verify the byte layout.
    /// </summary>
    [Test]
    public void Serialize_WritesFrostbiteLayout()
    {
        var dict = DbObject.CreateDict();
        dict.Set("a", 1);

        byte[] expected =
        [
            0x82, // Anonymous Dict
            0x08, // Size of the children and the terminator
            0x08, (byte)'a', 0x00, 0x01, 0x00, 0x00, 0x00, // Int "a" = 1
            0x00 // Terminator
        ];

        Assert.That(ToBytes(dict), Is.EqualTo(expected));
    }

    /// <summary>
    ///     Verify that every value type, nested in dicts and lists, keeps its name and value through a round trip.
    /// </summary>
    [Test]
    public void Deserialize_RoundTripsEveryType()
    {
        var guid = Guid.NewGuid();
        var sha1 = Sha1.HashData([1, 2, 3]);
        byte[] blob = [0xFF, 0x00];

        var root = DbObject.CreateDict();
        root.Set("bool", true);
        root.Set("string", "Größe");
        root.Set("int", -2);
        root.Set("long", long.MinValue);
        root.Set("float", 1.5f);
        root.Set("double", -2.25);
        root.Set("guid", guid);
        root.Set("sha1", sha1);
        root.Set("blob", blob);

        var list = DbObject.CreateList("list");
        list.Add(7u);
        var inner = DbObject.CreateDict("inner");
        inner.Set("x", ulong.MaxValue);
        root.Set("inner", inner);
        list.Add(DbObject.CreateDict());
        root.Set("list", list);

        var result = FromBytes(ToBytes(root))!.AsDict();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(result.AsBoolean("bool"), Is.True);
            Assert.That(result.AsString("string"), Is.EqualTo("Größe"));
            Assert.That(result.AsInt("int"), Is.EqualTo(-2));
            Assert.That(result.AsLong("long"), Is.EqualTo(long.MinValue));
            Assert.That(result.AsFloat("float"), Is.EqualTo(1.5f));
            Assert.That(result.AsDouble("double"), Is.EqualTo(-2.25));
            Assert.That(result.AsGuid("guid"), Is.EqualTo(guid));
            Assert.That(result.AsSha1("sha1"), Is.EqualTo(sha1));
            Assert.That(result.AsBlob("blob"), Is.EqualTo(blob));
            Assert.That(result.AsDict("inner").AsULong("x"), Is.EqualTo(ulong.MaxValue));
            Assert.That(result.AsList("list"), Has.Count.EqualTo(2));
            Assert.That(result.AsList("list")[0].AsUInt(), Is.EqualTo(7u));
            Assert.That(result.AsList("list")[1].IsDict(), Is.True);
        }
    }

    [Test]
    public void Dict_KeysIgnoreCase()
    {
        var dict = DbObject.CreateDict();
        dict.Set("superBundles", DbObject.CreateList("superBundles"));

        Assert.That(dict.AsList("superbundles", null), Is.Not.Null);
    }

    [Test]
    public void Set_RejectsValueNamedDifferently()
    {
        var dict = DbObject.CreateDict();

        using (Assert.EnterMultipleScope())
        {
            Assert.That(() => dict.Set("meta", DbObject.CreateDict()), Throws.ArgumentException);
            Assert.That(() => dict.Set("meta", DbObject.CreateList("other")), Throws.ArgumentException);
        }
    }
}