using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.Utils;

/// <summary>
///     Tests for <see cref="FourCc" /> string conversion.
/// </summary>
internal sealed class FourCcTests
{
    /// <summary>
    ///     Verify that a FourCC with bytes above 0x7F survives the trip to a string and back.
    /// </summary>
    [TestCase(0x46464952u, "RIFF")]
    [TestCase(0xFF80A900u, "\0\u00A9\u0080\u00FF")]
    [TestCase(0x00000000u, "\0\0\0\0")]
    public void String_RoundTripsEveryByte(uint inValue, string expectedText)
    {
        string text = FourCc.FromFourCc(inValue);

        Assert.That(text, Is.EqualTo(expectedText));
        Assert.That(FourCc.ToFourCc(text).ToUInt32(), Is.EqualTo(inValue));
    }

    /// <summary>
    ///     Verify that non-four-bytes strings are rejected instead of being truncated.
    /// </summary>
    [TestCase("RIP")]
    [TestCase("RIPEA")]
    [TestCase("RIĀF")]
    public void ToFourCc_RejectsInvalid(string inText)
    {
        Assert.That(() => FourCc.ToFourCc(inText), Throws.ArgumentException);
    }
}
