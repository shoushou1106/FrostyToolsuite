using System;
using Frosty.Sdk.Interfaces;
using Frosty.Sdk.IO;
using Frosty.Sdk.Managers.Infos.FileInfos.ResourceInfo;
using Frosty.Sdk.Utils;

namespace Frosty.Sdk.Managers.Infos.FileInfos;

public class CasFileInfo : IFileInfo
{
    public CasResourceInfo? GetBase() => m_base;

    private readonly CasResourceInfo? m_base;
    private readonly CasResourceInfo? m_delta;

    public CasFileInfo(CasResourceInfo? inBase, CasResourceInfo? inDelta = null)
    {
        m_base = inBase;
        m_delta = inDelta;
    }

    public CasFileInfo(CasFileIdentifier inCasFileIdentifier, uint inOffset, uint inSize, uint inLogicalOffset)
    {
        m_base = new CasResourceInfo(inCasFileIdentifier, inOffset, inSize, inLogicalOffset);
    }

    public CasFileInfo(CasFileIdentifier inCasFileIdentifier, uint inOffset, uint inSize, uint inLogicalOffset, string inKeyId)
    {
        m_base = new CasCryptoResourceInfo(inCasFileIdentifier, inOffset, inSize, inLogicalOffset, inKeyId);
    }

    public bool IsDelta() => m_delta is not null;
    public bool IsComplete() => (m_delta?.IsComplete() == true) || (m_base?.IsComplete() == true);

    public bool FileExists()
    {
        if (m_base is null)
        {
            throw new Exception("Base CasResourceInfo can't be null.");
        }

        bool exists = m_base.FileExists();

        if (m_delta is not null)
        {
            exists &= m_delta.FileExists();
        }

        return exists;
    }

    public long GetSize() => m_base?.GetSize() ?? 0;

    public long GetOriginalSize()
    {
        if (m_base is null)
        {
            throw new Exception("Base CasResourceInfo can't be null.");
        }

        if (m_delta is null)
        {
            using (BlockAccessor accessor = new(m_base.GetRawData()))
            {
                return Cas.GetOriginalSize(accessor);
            }
        }

        using (BlockAccessor deltaAccessor = new(m_delta.GetRawData()))
        {
            BlockAccessor? baseAccessor = null;
            if (m_base is not null)
            {
                baseAccessor = new BlockAccessor(m_base.GetRawData());
            }
            return Cas.GetOriginalSize(deltaAccessor, baseAccessor);
        }
    }

    public Block<byte> GetRawData()
    {
        if (m_delta is null)
        {
            if (m_base is null)
            {
                throw new Exception("Base CasResourceInfo can't be null.");
            }

            return m_base.GetRawData();
        }

        throw new NotImplementedException();
    }

    public Block<byte> GetData(int inOriginalSize)
    {
        if (m_delta is null)
        {
            if (m_base is null)
            {
                throw new Exception("Base CasResourceInfo can't be null.");
            }

            return m_base.GetData(inOriginalSize);
        }

        using (BlockAccessor deltaAccessor = new(m_delta.GetRawData()))
        {
            BlockAccessor? baseAccessor = null;
            if (m_base is not null)
            {
                baseAccessor = new BlockAccessor(m_base.GetRawData());
            }
            return Cas.DecompressData(deltaAccessor, baseAccessor, inOriginalSize);
        }
    }

    void IFileInfo.SerializeInternal(DataAccessor accessor)
    {
        CasResourceInfo.Serialize(accessor, m_base);
        CasResourceInfo.Serialize(accessor, m_delta);
    }

    internal static CasFileInfo DeserializeInternal(DataAccessor accessor)
    {
        return new CasFileInfo(CasResourceInfo.Deserialize(accessor), CasResourceInfo.Deserialize(accessor));
    }

    public bool Equals(CasFileInfo b)
    {
        return m_base == b.m_base &&
               m_delta == b.m_delta;
    }

    public override bool Equals(object? obj)
    {
        if (obj is CasFileInfo b)
        {
            return Equals(b);
        }

        return false;
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(m_base, m_delta);
    }
}