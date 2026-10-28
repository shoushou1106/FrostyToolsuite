using System;
using System.Collections.Generic;

using Frosty.Modding.Interfaces;
using Frosty.Sdk.Utils;
using Frosty.Sdk.IO;

namespace Frosty.Modding.Mod.Resources;

public abstract class BaseModResource
{
    [Flags]
    public enum ResourceFlags : byte
    {
        IsAdded = 1 << 3
    }

    /// <summary>
    /// The <see cref="ModResourceType"/> of this <see cref="BaseModResource"/>.
    /// </summary>
    public virtual ModResourceType Type => ModResourceType.Invalid;

    /// <summary>
    /// The index into the data array of the <see cref="IResourceContainer"/> or -1 if it doesn't have any data.
    /// </summary>
    public int ResourceIndex { get; }

    /// <summary>
    /// The name of this <see cref="BaseModResource"/>.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The <see cref="Sha1"/> hash of the data of this <see cref="BaseModResource"/>.
    /// </summary>
    public Sha1 Sha1 { get; internal set; }

    /// <summary>
    /// The uncompressed size of the data of this <see cref="BaseModResource"/>.
    /// </summary>
    public long OriginalSize { get; }

    /// <summary>
    /// The hash of the handler if this <see cref="BaseModResource"/> has one, else its 0.
    /// </summary>
    public int HandlerHash { get; }

    /// <summary>
    /// The user data of this <see cref="BaseModResource"/>.
    /// </summary>
    public string UserData { get; } = string.Empty;

    /// <summary>
    /// Indicates if this <see cref="BaseModResource"/> has data.
    /// </summary>
    public bool IsModified => ResourceIndex != -1 && Type != ModResourceType.Embedded && Type != ModResourceType.Bundle;

    /// <summary>
    /// The <see cref="ResourceFlags"/> of this <see cref="BaseModResource"/>.
    /// </summary>
    public ResourceFlags Flags { get; }

    /// <summary>
    /// Indicates if this <see cref="BaseModResource"/> has a handler.
    /// </summary>
    public bool HasHandler => HandlerHash != 0;

    /// <summary>
    /// The bundles this <see cref="BaseModResource"/> is added to.
    /// </summary>
    public IEnumerable<int> AddedBundles => m_bundlesToAdd;

    /// <summary>
    /// The bundles this <see cref="BaseModResource"/> is removed from.
    /// </summary>
    public IEnumerable<int> RemovedBundles => m_bundlesToRemove;

    private readonly HashSet<int> m_bundlesToAdd = new();
    private readonly HashSet<int> m_bundlesToRemove = new();

    protected BaseModResource(DataAccessor inAccessor)
    {
        ResourceIndex = inAccessor.ReadInt32();
        Name = inAccessor.ReadNullTerminatedString();
        Flags = (ResourceFlags)inAccessor.ReadByte();

        if (ResourceIndex != -1)
        {
            Sha1 = inAccessor.ReadSha1();
            OriginalSize = inAccessor.ReadInt64();
            HandlerHash = inAccessor.ReadInt32();
            UserData = inAccessor.ReadNullTerminatedString();
        }

        int addCount = inAccessor.ReadInt32();
        for (int i = 0; i < addCount; i++)
        {
            m_bundlesToAdd.Add(inAccessor.ReadInt32());
        }

        int removeCount = inAccessor.ReadInt32();
        for (int i = 0; i < removeCount; i++)
        {
            m_bundlesToRemove.Add(inAccessor.ReadInt32());
        }
    }

    protected BaseModResource(int inResourceIndex, string inName, Sha1 inSha1, long inOriginalSize,
        ResourceFlags inFlags, int inHandlerHash, string inUserData, IEnumerable<int> inBundlesToAdd,
        IEnumerable<int> inBundlesToRemove)
    {
        ResourceIndex = inResourceIndex;
        Name = inName;
        Sha1 = inSha1;
        OriginalSize = inOriginalSize;
        Flags = inFlags;
        HandlerHash = inHandlerHash;
        UserData = inUserData;
        m_bundlesToAdd.UnionWith(inBundlesToAdd);
        m_bundlesToRemove.UnionWith(inBundlesToRemove);
    }

    public virtual void Write(DataAccessor accessor)
    {
        accessor.WriteByte((byte)Type);
        accessor.WriteInt32(ResourceIndex);
        accessor.WriteNullTerminatedString(Name);
        accessor.WriteByte((byte)Flags);

        if (ResourceIndex != -1)
        {
            accessor.WriteSha1(Sha1);
            accessor.WriteInt64(OriginalSize);
            accessor.WriteInt32(HandlerHash);
            accessor.WriteNullTerminatedString(UserData);
        }

        accessor.WriteInt32(m_bundlesToAdd.Count);
        foreach (int bundleId in m_bundlesToAdd)
        {
            accessor.WriteInt32(bundleId);
        }

        accessor.WriteInt32(m_bundlesToRemove.Count);
        foreach (int bundleId in m_bundlesToRemove)
        {
            accessor.WriteInt32(bundleId);
        }
    }
}