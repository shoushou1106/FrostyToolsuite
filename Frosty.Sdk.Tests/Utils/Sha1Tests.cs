using System.Security.Cryptography;

using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.Utils;

/// <summary>
///     Tests for <see cref="Frosty.Sdk.Utils.Sha1" /> class to make sure it behaves as the old version and v1.
/// </summary>
internal sealed class Sha1Tests
{
    [Test]
    public void HashData_MatchesKnownVectorAndRoundTrips()
    {
        const string expected = "a9993e364706816aba3e25717850c26c9cd0d89d";

        Sha1 hash = Sha1.HashData("abc"u8);

        using (Assert.EnterMultipleScope())
        {
            Assert.That(hash.ToString(), Is.EqualTo(expected));
            // CA5350: Do Not Use Weak Cryptographic Algorithms
#pragma warning disable CA5350
            Assert.That(hash.ToByteArray(), Is.EqualTo(SHA1.HashData("abc"u8)));
#pragma warning restore CA5350
            Assert.That(new Sha1(expected), Is.EqualTo(hash));
            Assert.That(new Sha1(expected.ToUpperInvariant()), Is.EqualTo(hash));
            Assert.That(hash, Is.Not.EqualTo(Sha1.Zero));
        }
    }
}
