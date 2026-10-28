using System;
using System.IO;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Managers.Infos.FileInfos;

public class NonCasFileInfo : IFileInfo
{
    private readonly string m_superBundlePath;
    private readonly long m_offset;
    private readonly uint m_size;
    private readonly uint m_logicalOffset;

    private readonly bool m_isDelta;
    private readonly string? m_superBundleBasePath;
    private readonly long m_baseOffset;
    private readonly uint m_baseSize;
    private readonly int m_midInstructionSize;

    private readonly string m_fullPath;
    private readonly string? m_fullBasePath;

    public NonCasFileInfo(string inSuperBundlePath, long inOffset, uint inSize, uint inLogicalOffset = 0)
    {
        m_superBundlePath = inSuperBundlePath;
        m_offset = inOffset;
        m_size = inSize;
        m_logicalOffset = inLogicalOffset;
        m_fullPath = Path.Combine(FileSystemManager.BasePath, m_superBundlePath);
    }

    public NonCasFileInfo(string inSuperBundlePath, string? inSuperBundleBasePath, long inDeltaOffset, uint inDeltaSize, long inBaseOffset, uint inBaseSize, int inMidInstructionSize, uint inLogicalOffset = 0)
    {
        m_isDelta = true;
        m_superBundlePath = inSuperBundlePath;
        m_superBundleBasePath = inSuperBundleBasePath;
        m_offset = inDeltaOffset;
        m_size = inDeltaSize;
        m_baseOffset = inBaseOffset;
        m_baseSize = inBaseSize;
        m_midInstructionSize = inMidInstructionSize;
        m_logicalOffset = inLogicalOffset;
        m_fullPath = Path.Combine(FileSystemManager.BasePath, m_superBundlePath);
        m_fullBasePath = m_superBundleBasePath is not null ? Path.Combine(FileSystemManager.BasePath, m_superBundleBasePath) : null;
    }

    public bool IsDelta() => m_isDelta;

    public bool IsComplete() => m_logicalOffset == 0;

    public bool FileExists() => true;

    public long GetOriginalSize()
    {
        if (m_isDelta)
        {
            BlockAccessor? baseAccessor = null;
            if (m_baseSize > 0)
            {
                baseAccessor = BlockAccessor.FromFile(m_fullBasePath!, m_baseOffset, (int)m_baseSize);
            }

            using (BlockAccessor deltaAccessor = BlockAccessor.FromFile(m_fullPath, m_offset, (int)m_size))
            {
                return Cas.GetOriginalSize(deltaAccessor, baseAccessor, m_midInstructionSize);
            }
        }

        using (BlockAccessor accessor = BlockAccessor.FromFile(m_fullPath, m_offset, (int)m_size))
        {
            return Cas.GetOriginalSize(accessor);
        }
    }

    public Block<byte> GetRawData()
    {
        if (m_isDelta)
        {
            throw new NotImplementedException();
        }

        using (FileStream stream = new(m_fullPath, FileMode.Open, FileAccess.Read))
        {
            stream.Position = m_offset;

            Block<byte> retVal = Block<byte>.CreateUninitialized((int)m_size);

            stream.ReadExactly(retVal);
            return retVal;
        }
    }

    public Block<byte> GetData(int inOriginalSize)
    {
        if (m_isDelta)
        {
            BlockAccessor? baseAccessor = null;
            if (m_baseSize > 0)
            {
                baseAccessor = BlockAccessor.FromFile(m_fullBasePath!, m_baseOffset, (int)m_baseSize);
            }

            using (BlockAccessor deltaAccessor = BlockAccessor.FromFile(m_fullPath, m_offset, (int)m_size))
            {
                Block<byte> retVal = Cas.DecompressData(deltaAccessor, baseAccessor, inOriginalSize, m_midInstructionSize);
                baseAccessor?.Dispose();
                return retVal;
            }
        }

        using (BlockAccessor accessor = BlockAccessor.FromFile(m_fullPath, m_offset, (int)m_size))
        {
            return Cas.DecompressData(accessor, inOriginalSize);
        }
    }

    public void SerializeInternal(DataAccessor accessor)
    {
        accessor.WriteBoolean(m_isDelta);
        accessor.WriteNullTerminatedString(m_superBundlePath);

        if (m_isDelta)
        {
            accessor.WriteNullTerminatedString(m_superBundleBasePath ?? string.Empty);
        }

        accessor.WriteInt64(m_offset);
        accessor.WriteUInt32(m_size);

        if (m_isDelta)
        {
            accessor.WriteInt64(m_baseOffset);
            accessor.WriteUInt32(m_baseSize);
            accessor.WriteInt32(m_midInstructionSize);
        }

        accessor.WriteUInt32(m_logicalOffset);
    }

    public static NonCasFileInfo DeserializeInternal(DataAccessor accessor)
    {
        bool isDelta = accessor.ReadBoolean();
        if (isDelta)
        {
            return new NonCasFileInfo(accessor.ReadNullTerminatedString(), accessor.ReadNullTerminatedString(),
                accessor.ReadInt64(), accessor.ReadUInt32(), accessor.ReadInt64(), accessor.ReadUInt32(), accessor.ReadInt32(),
                accessor.ReadUInt32());
        }
        return new NonCasFileInfo(accessor.ReadNullTerminatedString(), accessor.ReadInt64(), accessor.ReadUInt32(),
            accessor.ReadUInt32());
    }
}