using System.Buffers.Binary;

using Frosty.Sdk.TypeSdk;

namespace Frosty.Sdk.IO.LegacyEbx;

internal struct EbxTypeDescriptor
{
    public string Name;
    public uint NameHash;
    public int FieldIndex;
    public byte FieldCount;
    public byte Alignment;
    public TypeFlags Flags;
    public ushort Size;
    public ushort SecondSize;

    public int Index = -1;

    /// <summary>
    ///     Unpacks a shared type descriptor key, which is the descriptor itself stored in the 16 bytes of a guid.
    /// </summary>
    public EbxTypeDescriptor(Guid inKey)
    {
        Span<byte> bytes = stackalloc byte[16];
        inKey.TryWriteBytes(bytes);

        Name = string.Empty;
        NameHash = BinaryPrimitives.ReadUInt32LittleEndian(bytes);
        FieldIndex = BinaryPrimitives.ReadInt32LittleEndian(bytes[4..]);
        FieldCount = bytes[8];
        Alignment = bytes[9];
        Flags = BinaryPrimitives.ReadUInt16LittleEndian(bytes[10..]);
        Size = BinaryPrimitives.ReadUInt16LittleEndian(bytes[12..]);
        SecondSize = BinaryPrimitives.ReadUInt16LittleEndian(bytes[14..]);
    }

    // The 9th bit of the field count is stored in the top bit of Alignment
    public ushort GetFieldCount() => (ushort)(FieldCount | ((Alignment & 0x80) << 1));

    public void SetFieldCount(ushort value)
    {
        FieldCount = (byte)value;
        Alignment = (byte)((Alignment & ~0x80) | ((value & 0x100) >> 1));
    }

    public byte GetAlignment() => (byte)(Alignment & 0x7F);

    public void SetAlignment(byte value) => Alignment = (byte)((Alignment & ~0x7F) | value);

    public bool IsSharedTypeDescriptorKey() => (FieldIndex & 0x80000000) != 0;

    public Guid ToKey()
    {
        Span<byte> key = stackalloc byte[16];

        BinaryPrimitives.WriteUInt32LittleEndian(key, NameHash);
        BinaryPrimitives.WriteInt32LittleEndian(key[4..], FieldIndex);
        key[8] = FieldCount;
        key[9] = Alignment;
        BinaryPrimitives.WriteUInt16LittleEndian(key[10..], Flags);
        BinaryPrimitives.WriteUInt16LittleEndian(key[12..], Size);
        BinaryPrimitives.WriteUInt16LittleEndian(key[14..], SecondSize);

        return new Guid(key);
    }
}