using System.Reflection;

using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Tests.Utils;

internal sealed class HuffmanEncodingTests
{
    private static HuffmanDecoder CreateDecoderFromTree(IList<uint> encodingTree)
    {
        HuffmanDecoder decoder = new();

        using MemoryStream stream = new();
        using DataStream ds = new(stream);

        foreach (uint value in encodingTree)
        {
            ds.WriteUInt32(value);
        }

        ds.Position = 0;

        decoder.ReadHuffmanTable(ds, (uint)encodingTree.Count);

        return decoder;
    }

    private static void ReadEncodedData(HuffmanDecoder inDecoder, byte[] inData, Endian? inEndian = null)
    {
        using MemoryStream stream = new();
        using DataStream ds = new(stream);

        ds.Write(inData);
        ds.Position = 0;

        if (inEndian.HasValue)
        {
            inDecoder.ReadOddSizedEncodedData(ds, (uint)inData.Length, inEndian.Value);
        }
        else
        {
            inDecoder.ReadOddSizedEncodedData(ds, (uint)inData.Length);
        }
    }

    /// <summary>
    ///     Tests the encoding and decoding of some test strings.
    ///     The argument source once encodes the strings with reusing existing entries,
    ///     and once without, leading to different result byte lengths.
    /// </summary>
    [TestCase(false, 195, 25, Endian.Little)]
    [TestCase(false, 195, 28, Endian.Big)]
    [TestCase(true, 175, 22, Endian.Little)]
    [TestCase(true, 175, 24, Endian.Big)]
    public void EncodeThenDecode(bool inCompressResults, int inEncodedBitSize, int inEncodedByteSize,
        Endian inEndian)
    {
        string[] texts = ["These are ", "", "some ", "Test Texts", " for tests ", "some ", " these are"];

        HuffmanEncoder encoder = new();

        IList<uint> encodingTree = encoder.BuildHuffmanEncodingTree(texts);

        List<Tuple<int, string>> input =
        [
            .. texts.Select(static (text, index) => Tuple.Create(index, text))
        ];

        // No padding here
        HuffmanEncodedTextArray<int> encodingResult = encoder.EncodeTexts(input, inEndian, inCompressResults, false);
        using (Assert.EnterMultipleScope())
        {
            Assert.That(encodingResult.EncodedTestsAsBools, Has.Count.EqualTo(inEncodedBitSize),
                "Encoded bit count does not match expected count");
            Assert.That(encodingResult.EncodedTexts, Is.Not.Null);
            Assert.That(encodingResult.EncodedTexts, Has.Length.EqualTo(inEncodedByteSize),
                "Encoded data-length does not match expected length");
            Assert.That(encodingResult.EncodedTextPositions, Has.Count.EqualTo(texts.Length),
                "Encoded text position has different number of entries than the number of encoded texts!");
        }

        HuffmanDecoder decoder = CreateDecoderFromTree(encodingTree);
        ReadEncodedData(decoder, encodingResult.EncodedTexts, inEndian);

        List<string> decoded =
        [
            .. encodingResult.EncodedTextPositions.Select(textId => decoder.ReadHuffmanEncodedString(textId.Position))
        ];

        // Assert that the texts can be decoded again
        using (Assert.EnterMultipleScope())
        {
            Assert.That(decoded, Has.Count.EqualTo(texts.Length),
                "Decoded number of texts does not match input number of texts!");
            Assert.That(decoded.ToArray(), Is.EqualTo(texts),
                "Decoded texts do not match given texts to encode!");
        }
    }


    [Test]
    public void EncodeThenDecode_TextAsKey()
    {
        string[] texts =
        [
            "Some ", "more ", "Text that might be ", "stored together ", "or whatever ",
            "these are only ", "for test usage"
        ];

        HuffmanEncoder encoder = new();
        IList<uint> encodingTree = encoder.BuildHuffmanEncodingTree(texts);

        HuffmanEncodedTextArray<string> encodingResult =
            encoder.EncodeTexts([
                .. texts.Select(static text => Tuple.Create(text, text))
            ], Endian.Little);

        byte[]? encoded = encodingResult.EncodedTexts;
        Assert.That(encoded, Is.Not.Null);

        HuffmanDecoder decoder = CreateDecoderFromTree(encodingTree);
        ReadEncodedData(decoder, encoded);

        Dictionary<string, int> lookupMap =
            encodingResult.EncodedTextPositions.ToDictionary(
                static t => t.Identifier, static t => t.Position);

        List<string> decoded =
        [
            .. texts.Select(text => decoder.ReadHuffmanEncodedString(lookupMap[text]))
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decoded, Has.Count.EqualTo(texts.Length),
                "Decoded number of texts does not match input number of texts!");
            Assert.That(decoded, Is.EqualTo(texts),
                "Decoded texts do not match given texts to encode!");
            Assert.That(encoded.Length & 3, Is.Zero, // Size Modulo Op
                "Encoded byte array is not divisible by 4 without rest!");
        }
    }

    [Test]
    public void EncodeThenDecode_WithPadding()
    {
        string[] texts =
        [
            "I'm a mog, half man, half dog", "I'm my own best friend!",
            "Oh yes, now they are small and cute and cuddly",
            " and next they suddenly have teeth",
            " and there is a thousand of them"
        ];

        EncodingResult encodingResult = HuffmanEncoder.Encode(texts);
        byte[]? encoded = encodingResult.EncodedTexts;
        Assert.That(encoded, Is.Not.Null);

        HuffmanDecoder decoder = CreateDecoderFromTree(encodingResult.EncodingTree);
        ReadEncodedData(decoder, encoded);

        Dictionary<string, int> lookupMap = encodingResult.GetTextPositionsDictionary();

        List<string> decoded =
        [
            .. texts.Select(text => decoder.ReadHuffmanEncodedString(lookupMap[text]))
        ];

        using (Assert.EnterMultipleScope())
        {
            Assert.That(decoded, Has.Count.EqualTo(texts.Length),
                "Decoded number of texts does not match input number of texts!");
            Assert.That(decoded, Is.EqualTo(texts),
                "Decoded texts do not match given texts to encode!");
            Assert.That(encoded.Length & 3, Is.Zero, // Size Modulo Op
                "Encoded byte array is not divisible by 4 without rest!");
        }
    }

    [TestCase(0, false, ExpectedResult = 0)]
    [TestCase(0, true, ExpectedResult = 0)]
    [TestCase(1, false, ExpectedResult = 1)]
    [TestCase(1, true, ExpectedResult = 4)]
    [TestCase(8, false, ExpectedResult = 1)]
    [TestCase(8, true, ExpectedResult = 4)]
    [TestCase(9, false, ExpectedResult = 2)]
    [TestCase(9, true, ExpectedResult = 4)]
    [TestCase(16, false, ExpectedResult = 2)]
    [TestCase(16, true, ExpectedResult = 4)]
    [TestCase(17, false, ExpectedResult = 3)]
    [TestCase(17, true, ExpectedResult = 4)]
    [TestCase(24, false, ExpectedResult = 3)]
    [TestCase(24, true, ExpectedResult = 4)]
    [TestCase(25, false, ExpectedResult = 4)]
    [TestCase(25, true, ExpectedResult = 4)]
    [TestCase(32, false, ExpectedResult = 4)]
    [TestCase(32, true, ExpectedResult = 4)]
    [TestCase(33, false, ExpectedResult = 5)]
    [TestCase(33, true, ExpectedResult = 8)]
    [TestCase(2400, false, ExpectedResult = 300)]
    [TestCase(2400, true, ExpectedResult = 300)]
    public int GetDataLengthInBytes_WithPadding(int inBitSize, bool inUsePadding)
    {
        return HuffmanEncoder.GetDataLengthInBytes(inBitSize, inUsePadding);
    }

    // ReSharper disable StringLiteralTypo
    [TestCase("Frosty.Sdk.Tests.TestData.original_huffman", Endian.Big,
        ExpectedResult = "win32/content/common/configs/bundles/careermodestory_sba")]
    [TestCase("Frosty.Sdk.Tests.TestData.new_huffman", Endian.Little,
        ExpectedResult = "win32/content/cinematic/scenes/livinghubs/f22_pap_lh_ll/f22_pap_lh_ll_set_sublevel")]
    // ReSharper enable StringLiteralTypo
    public string TestReadFirstEntryFromFile(string inTestFilePath, Endian inEndian)
    {
        using Stream? stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(inTestFilePath);
        Assert.That(stream, Is.Not.Null,
            "Cannot read resource file!");

        // Same for both test files
        const uint textLengthInBytes = 300;

        HuffmanDecoder decoder = new();
        using (DataStream ds = new(stream))
        {
            // New Huffman was created when writing the string data ignored endianness and always used little endian.
            decoder.ReadOddSizedEncodedData(ds, textLengthInBytes, inEndian);

            ds.Position = textLengthInBytes;

            uint numberOfTreeNodes = (uint)(ds.Length - ds.Position) / 4;

            // Both test files were created with big endian as setting.
            decoder.ReadHuffmanTable(ds, numberOfTreeNodes, Endian.Big);
        }

        return decoder.ReadHuffmanEncodedString(0);
    }
}