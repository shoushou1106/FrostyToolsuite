using Frosty.Sdk.Ebx;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.Tests.Ebx;

/// <summary>
///     Tests for the equality of the EBX value types and the instance lists of <see cref="EbxPartition" />.
/// </summary>
// .Equal? Equals! >> Should we call this the EA SPORTS yearly test?
internal sealed class EbxValueTests
{
    /// <summary>
    ///     Verify that two boxed values are equal when the values are equal, not only when they are the same object.
    /// </summary>
    [Test]
    public void BoxedValueRef_ComparesValues()
    {
        TypeFlags flags = new(TypeFlags.TypeEnum.Int32);
        BoxedValueRef a = new(42, flags);
        BoxedValueRef b = new(42, flags);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
            Assert.That(a, Is.Not.EqualTo(new BoxedValueRef(43, flags)));
            Assert.That(a, Is.Not.EqualTo(new BoxedValueRef(42, new TypeFlags(TypeFlags.TypeEnum.UInt32))));
        }
    }

    /// <summary>
    ///     Verify that an ID only equals another ID, so <see cref="object.Equals(object)" /> is symmetric.
    /// </summary>
    [Test]
    public void AssetClassGuid_EqualsOnlyAssetClassGuid()
    {
        Guid guid = Guid.NewGuid();
        AssetClassGuid exported = new(guid, 3);
        object boxedGuid = guid;
        object boxedId = 3;

        using (Assert.EnterMultipleScope())
        {
            Assert.That(exported, Is.EqualTo(new AssetClassGuid(guid, 3)));
            // The bad comparisons are the point of the test.
            // NUnit2010: Use EqualConstraint for better assertion messages in case of failure
#pragma warning disable NUnit2010
            Assert.That(exported.Equals(boxedGuid), Is.False);
            Assert.That(exported.Equals(boxedId), Is.False);
#pragma warning restore NUnit2010
            Assert.That(new AssetClassGuid(3).IsExported, Is.False);
        }
    }

    /// <summary>
    ///     Verify that every added instance has a reference count, so <see cref="EbxPartition.RootInstances" /> works.
    /// </summary>
    [Test]
    public void EbxPartition_CountsReferencesOfAddedInstances()
    {
        TestInstance primary = new();
        TestInstance second = new();
        EbxPartition partition = new(primary, second);

        TestInstance added = new() { Guid = new AssetClassGuid(Guid.Empty, -1) };
        partition.AddObject(added);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partition.RootInstances, Is.EqualTo(new IEbxInstance[] { primary, second, added }));
            Assert.That(added.GetInstanceGuid().InternalId, Is.EqualTo(2));
            Assert.That(partition.GetObject(second.GetInstanceGuid().ExportedGuid), Is.SameAs(second));
            Assert.That(partition.GetObject(Guid.NewGuid()), Is.Null);
        }
    }

    /// <summary>
    ///     Verify that removing an instance also removes its reference count, so <see cref="EbxPartition.RootInstances" /> works.
    /// </summary>
    [Test]
    public void EbxPartition_RemovesReferenceCountWithInstance()
    {
        TestInstance primary = new();
        TestInstance second = new();
        TestInstance third = new();
        EbxPartition partition = new(primary, second, third);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(partition.RemoveObject(second), Is.True);
            Assert.That(partition.RemoveObject(second), Is.False);
            Assert.That(partition.RootInstances, Is.EqualTo(new IEbxInstance[] { primary, third }));
        }
    }

    private sealed class TestInstance : IEbxInstance
    {
        public AssetClassGuid Guid { get; set; }

        public AssetClassGuid GetInstanceGuid()
        {
            return Guid;
        }

        public void SetInstanceGuid(AssetClassGuid newGuid)
        {
            Guid = newGuid;
        }
    }
}
