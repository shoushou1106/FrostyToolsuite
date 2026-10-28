using System.Security.Cryptography;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Managers.Infos.FileInfos.ResourceInfo;

public class CasCryptoResourceInfo : CasResourceInfo
{
    private readonly string m_keyId;

    public CasCryptoResourceInfo(CasFileIdentifier inCasFileIdentifier, uint inOffset, uint inSize, uint inLogicalOffset, string inKeyId)
        : base(inCasFileIdentifier, inOffset, inSize, inLogicalOffset)
    {
        m_keyId = inKeyId;
    }

    public CasCryptoResourceInfo(bool inIsPatch, uint inInstallChunkIndex, int inCasIndex, uint inOffset, uint inSize, uint inLogicalOffset, string inKeyId)
        : base(inIsPatch, inInstallChunkIndex, inCasIndex, inOffset, inSize, inLogicalOffset)
    {
        m_keyId = inKeyId;
    }

    public override Block<byte> GetRawData()
    {
        // we need to align the size to 16
        int size = (int)GetSize();
        size = size + 15 & ~15;

        using (BlockAccessor accessor = BlockAccessor.FromFile(m_path, GetOffset(), size))
        {
            accessor.Decrypt(KeyManager.GetKey(m_keyId), PaddingMode.None);

            Block<byte> retVal = Block<byte>.CreateUninitialized((int)GetSize());
            accessor.ReadExactly(retVal);
            return retVal;
        }
    }

    public override Block<byte> GetData(int inOriginalSize)
    {
        // we need to align the size to 16
        int size = (int)GetSize();
        size = size + 15 & ~15;
        using (BlockAccessor accessor = BlockAccessor.FromFile(m_path, GetOffset(), size))
        {
            accessor.Decrypt(KeyManager.GetKey(m_keyId), PaddingMode.None);
            accessor.SetLength((int)GetSize());
            return Cas.DecompressData(accessor, inOriginalSize);
        }
    }

    internal static void SerializeInternal(DataAccessor accessor, CasCryptoResourceInfo info)
    {
        CasResourceInfo.SerializeInternal(accessor, info);
        accessor.WriteNullTerminatedString(info.m_keyId);
    }

    internal static CasCryptoResourceInfo DeserializeInternal(DataAccessor accessor)
    {
        CasFileIdentifier file = CasFileIdentifier.FromFileIdentifier(accessor.ReadUInt32());
        uint offset = accessor.ReadUInt32();
        uint size = accessor.ReadUInt32();
        uint logicalOffset = accessor.ReadUInt32();
        string keyId = accessor.ReadNullTerminatedString();

        return new CasCryptoResourceInfo(file, offset, size, logicalOffset, keyId);
    }
}