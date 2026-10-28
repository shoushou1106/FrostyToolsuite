using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

using Frosty.Modding.Interfaces;
using Frosty.Modding.ModEntries;
using Frosty.Modding.ModInfos;
using Frosty.Sdk;
using Frosty.Sdk.DbObjectElements;
using Frosty.Sdk.Exceptions;
using Frosty.Sdk.IO;
using Frosty.Sdk.Managers;
using Frosty.Sdk.Utils;

namespace Frosty.Modding;

public static class BinaryBundle
{
    public static Block<byte> Modify(BlockAccessor inAccessor, BundleModInfo inModInfo,
        Dictionary<string, EbxModEntry> inModifiedEbx, Dictionary<string, ResModEntry> inModifiedRes,
        Dictionary<Guid, ChunkModEntry> inModifiedChunks, Action<IModEntry, int, bool, bool, uint> modify)
    {
        // we use big endian for default
        Endian endian = Endian.Big;

        uint size = inAccessor.ReadUInt32(Endian.Big);

        long startPos = inAccessor.Position;

        Sdk.IO.BinaryBundle.Magic magic = (Sdk.IO.BinaryBundle.Magic)(inAccessor.ReadUInt32(endian) ^ Sdk.IO.BinaryBundle.GetSalt());

        // check what endian its written in
        if (!Sdk.IO.BinaryBundle.IsValidMagic(magic))
        {
            endian = Endian.Little;
            uint value = (uint)magic ^ Sdk.IO.BinaryBundle.GetSalt();
            magic = (Sdk.IO.BinaryBundle.Magic)(BinaryPrimitives.ReverseEndianness(value) ^ Sdk.IO.BinaryBundle.GetSalt());

            if (!Sdk.IO.BinaryBundle.IsValidMagic(magic))
            {
                throw new InvalidDataException("magic");
            }
        }

        bool containsSha1 = magic == Sdk.IO.BinaryBundle.Magic.Standard;

        uint totalCount = inAccessor.ReadUInt32(endian);
        int ebxCount = inAccessor.ReadInt32(endian);
        int resCount = inAccessor.ReadInt32(endian);
        int chunkCount = inAccessor.ReadInt32(endian);
        long stringsOffset = inAccessor.ReadUInt32(endian) + startPos;
        long metaOffset = inAccessor.ReadUInt32(endian) + startPos;
        uint metaSize = inAccessor.ReadUInt32(endian);

        // decrypt the data
        if (magic == Sdk.IO.BinaryBundle.Magic.Encrypted)
        {
            if (!KeyManager.HasKey("BundleEncryptionKey"))
            {
                throw new MissingEncryptionKeyException("bundles");
            }

            inAccessor.Decrypt(KeyManager.GetKey("BundleEncryptionKey"), (int)(size - 0x20), PaddingMode.None);
        }

        EbxModEntry[] ebx = new EbxModEntry[ebxCount + inModInfo.Added.Ebx.Count];
        ResModEntry[] res = new ResModEntry[resCount + inModInfo.Added.Res.Count];
        ChunkModEntry[] chunks = new ChunkModEntry[chunkCount + inModInfo.Added.Chunks.Count];

        uint offset = 0;
        Dictionary<string, uint> strings = new(ebx.Length + res.Length);

        // read sha1s
        Sha1[] sha1 = new Sha1[totalCount + inModInfo.Added.Ebx.Count + inModInfo.Added.Res.Count + inModInfo.Added.Chunks.Count];
        int b = 0;
        for (int i = 0; i < totalCount; i++)
        {
            if (i == ebxCount)
            {
                b += inModInfo.Added.Ebx.Count;
            }
            else if (i == ebxCount + resCount)
            {
                b += inModInfo.Added.Res.Count;
            }
            sha1[i + b] = containsSha1 ? inAccessor.ReadSha1() : Sha1.Zero;
        }

        int j = 0;
        for (int i = 0; i < ebxCount; i++, j++)
        {
            uint nameOffset = inAccessor.ReadUInt32(endian);
            uint originalSize = inAccessor.ReadUInt32(endian);

            long currentPos = inAccessor.Position;
            inAccessor.Position = stringsOffset + nameOffset;
            string name = inAccessor.ReadNullTerminatedString();

            if (inModInfo.Modified.Ebx.Contains(name))
            {
                EbxModEntry modEntry = inModifiedEbx[name];
                ebx[i] = modEntry;
                sha1[i] = modEntry.Sha1;
                modify(modEntry, j, false, true, originalSize);
            }
            else
            {
                ebx[i] = new EbxModEntry(name, sha1[j], originalSize);
                modify(ebx[i], j, false, false, originalSize);
            }

            inAccessor.Position = currentPos;

            if (strings.TryAdd(name, offset))
            {
                offset += (uint)name.Length + 1;
            }
        }

        int k = ebxCount;
        foreach (string name in inModInfo.Added.Ebx)
        {
            EbxModEntry modEntry = inModifiedEbx[name];
            ebx[k++] = modEntry;
            modify(modEntry, j , true, true, 0);
            sha1[j++] = modEntry.Sha1;
            if (strings.TryAdd(name, offset))
            {
                offset += (uint)name.Length + 1;
            }
        }

        long resTypeOffset = inAccessor.Position + resCount * 2 * sizeof(uint);
        long resMetaOffset = inAccessor.Position + resCount * 2 * sizeof(uint) + resCount * sizeof(uint);
        long resRidOffset = inAccessor.Position + resCount * 2 * sizeof(uint) + resCount * sizeof(uint) + resCount * 0x10;
        for (int i = 0; i < resCount; i++, j++)
        {
            uint nameOffset = inAccessor.ReadUInt32(endian);
            uint originalSize = inAccessor.ReadUInt32(endian);

            long currentPos = inAccessor.Position;
            inAccessor.Position = stringsOffset + nameOffset;
            string name = inAccessor.ReadNullTerminatedString();

            inAccessor.Position = resTypeOffset + i * sizeof(uint);
            uint resType = inAccessor.ReadUInt32(endian);

            inAccessor.Position = resMetaOffset + i * 0x10;
            byte[] resMeta = inAccessor.ReadBytes(0x10);

            inAccessor.Position = resRidOffset + i * sizeof(ulong);
            ulong resRid = inAccessor.ReadUInt64(endian);

            if (inModInfo.Modified.Res.Contains(name))
            {
                ResModEntry modEntry = inModifiedRes[name];
                res[i] = modEntry;
                sha1[i] = modEntry.Sha1;
                modify(modEntry, j, false, true, originalSize);
            }
            else
            {
                res[i] = new ResModEntry(name, sha1[j], originalSize, resRid, resType, resMeta);
                modify(res[i], j, false, false, originalSize);
            }

            inAccessor.Position = currentPos;

            if (strings.TryAdd(name, offset))
            {
                offset += (uint)name.Length + 1;
            }
        }

        k = resCount;
        foreach (string name in inModInfo.Added.Res)
        {
            ResModEntry modEntry = inModifiedRes[name];
            res[k++] = modEntry;
            modify(modEntry, j , true, true, 0);
            sha1[j++] = modEntry.Sha1;

            if (strings.TryAdd(name, offset))
            {
                offset += (uint)name.Length + 1;
            }
        }

        DbObjectList? chunkMeta = null;
        if (metaSize > 0)
        {
            inAccessor.Position = metaOffset;
            chunkMeta = DbObject.Deserialize(inAccessor)!.AsList();
        }

        inAccessor.Position = resRidOffset + resCount * sizeof(ulong);
        for (int i = 0; i < chunkCount; i++, j++)
        {
            Guid id = inAccessor.ReadGuid(endian);
            uint logicalOffset = inAccessor.ReadUInt32(endian);
            uint logicalSize = inAccessor.ReadUInt32(endian);

            if (inModInfo.Modified.Chunks.Contains(id))
            {
                ChunkModEntry modEntry = inModifiedChunks[id];
                chunks[i] = modEntry;
                sha1[i] = modEntry.Sha1;
                modify(modEntry, j, false, true, (logicalOffset & 0xFFFF) | logicalSize);

                if (modEntry.FirstMip != -1)
                {
                    DbObjectDict? meta = chunkMeta?.FirstOrDefault(m => m.AsDict().AsInt("h32") == modEntry.H32)?.AsDict();
                    if (meta is null)
                    {
                        meta = DbObject.CreateDict( 2);
                        meta.Set("h32", modEntry.H32);
                        meta.Set("meta", DbObject.CreateDict("meta", 1));
                        chunkMeta ??= DbObject.CreateList(1);
                        chunkMeta.Add(meta);
                    }
                    meta.AsDict("meta").Set("firstMip", modEntry.FirstMip);
                }
            }
            else
            {
                chunks[i] = new ChunkModEntry(id, sha1[j], logicalOffset, logicalSize);
                modify(chunks[i], j, false, false, (logicalOffset & 0xFFFF) | logicalSize);
            }
        }

        k = chunkCount;
        foreach (Guid id in inModInfo.Added.Chunks)
        {
            ChunkModEntry modEntry = inModifiedChunks[id];
            chunks[k++] = modEntry;
            modify(modEntry, j , true, true, 0);
            sha1[j++] = modEntry.Sha1;

            DbObjectDict meta = DbObject.CreateDict(2);
            meta.Set("h32", modEntry.H32);
            if (modEntry.FirstMip != -1)
            {
                DbObjectDict firstMip = DbObject.CreateDict("meta", 1);
                firstMip.Set("firstMip", modEntry.FirstMip);
                meta.Set("meta", firstMip);
            }
            chunkMeta!.Add(meta);
        }

        inAccessor.Position = startPos + size;

        // write new bundle
        stringsOffset = 32 + (containsSha1 ? sha1.Length * 20 : 0) + ebx.Length * 8 + res.Length * 36 +
                        chunks.Length * 24;
        Block<byte> retVal = new((int)(stringsOffset + offset));
        using (BlockAccessor accessor = new(retVal, true))
        {
            accessor.WriteUInt32(0xDEADBEEF, Endian.Big);

            accessor.WriteUInt32((uint)magic ^ Sdk.IO.BinaryBundle.GetSalt(), endian);

            accessor.WriteInt32(sha1.Length, endian);
            accessor.WriteInt32(ebx.Length, endian);
            accessor.WriteInt32(res.Length, endian);
            accessor.WriteInt32(chunks.Length, endian);

            accessor.WriteUInt32(0xDEADBEEF, endian);
            accessor.WriteUInt32(0xDEADBEEF, endian);
            accessor.WriteUInt32(0xDEADBEEF, endian);

            foreach (Sha1 value in sha1)
            {
                accessor.WriteSha1(value);
            }

            foreach (EbxModEntry entry in ebx)
            {
                accessor.WriteUInt32(strings[entry.Name], endian);
                accessor.WriteUInt32((uint)entry.OriginalSize, endian);
            }

            resTypeOffset = accessor.Position + res.Length * 2 * sizeof(uint);
            resMetaOffset = accessor.Position + res.Length * 2 * sizeof(uint) + res.Length * sizeof(uint);
            resRidOffset = accessor.Position + res.Length * 2 * sizeof(uint) + res.Length * sizeof(uint) + res.Length * 0x10;
            for (int i = 0; i < res.Length; i++)
            {
                ResModEntry entry = res[i];
                accessor.WriteUInt32(strings[entry.Name], endian);
                accessor.WriteUInt32((uint)entry.OriginalSize, endian);

                long currentPos = accessor.Position;
                accessor.Position = resTypeOffset + i * sizeof(uint);
                accessor.WriteUInt32(entry.ResType, endian);

                accessor.Position = resMetaOffset + i * 0x10;
                accessor.Write(entry.ResMeta);

                accessor.Position = resRidOffset + i * sizeof(ulong);
                accessor.WriteUInt64(entry.ResRid, endian);
                accessor.Position = currentPos;
            }

            accessor.Position = resRidOffset + res.Length * sizeof(ulong);

            foreach (ChunkModEntry entry in chunks)
            {
                accessor.WriteGuid(entry.Id, endian);
                accessor.WriteUInt32(entry.LogicalOffset, endian);
                accessor.WriteUInt32(entry.LogicalSize, endian);
            }

            uint chunkMetaOffset = (uint)accessor.Position - 4;
            if (chunkMeta is not null)
            {
                DbObject.Serialize(accessor, chunkMeta);
            }
            uint chunkMetaSize = (uint)(accessor.Position - 4 - chunkMetaOffset);

            foreach (KeyValuePair<string,uint> pair in strings)
            {
                accessor.WriteNullTerminatedString(pair.Key);
            }

            while (((accessor.Position - 0x24) & 15) != 0)
            {
                accessor.WriteByte(0);
            }

            accessor.Position = 0;
            accessor.WriteUInt32((uint)accessor.Length - 4, Endian.Big);

            accessor.Position = 0x18;
            accessor.WriteUInt32(chunkMetaOffset + chunkMetaSize, endian);
            accessor.WriteUInt32(chunkMetaOffset, endian);
            accessor.WriteUInt32(chunkMetaSize, endian);
        }

        return retVal;
    }
}