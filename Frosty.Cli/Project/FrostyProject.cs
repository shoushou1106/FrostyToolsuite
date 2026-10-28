using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using Frosty.Modding.Mod;
using Frosty.Modding.Mod.Resources;
using Frosty.Sdk;
using Frosty.Sdk.Ebx;
using Frosty.Sdk.IO;
using Frosty.Sdk.Utils;

namespace Frosty.Cli.Project;

public class FrostyProject
{
    [JsonIgnore]
    public string BasePath { get; set; } = string.Empty;

    public FrostyModDetails Details { get; set; } = new(string.Empty, string.Empty, string.Empty, string.Empty, string.Empty, string.Empty);
    public List<Ebx> Ebx { get; set; } = new();
    public List<Res> Res { get; set; } = new();
    public List<Chunk> Chunks { get; set; } = new();

    public void CompileToMod(string inPath)
    {
        List<BaseModResource> resources = new(Ebx.Count + Res.Count + Chunks.Count);
        List<Block<byte>> datas = new(Ebx.Count + Res.Count + Chunks.Count);
        foreach (Ebx ebx in Ebx)
        {
            using BlockAccessor accessor = BlockAccessor.FromFile(Path.Combine(BasePath, $"{ebx.Name}.dbx"), false);
            DbxReader reader = new(accessor.BaseStream);
            EbxPartition ebxAsset = reader.ReadAsset();
            using Block<byte> data = new(0);
            using (BlockAccessor dataAccessor = new(data, true))
            {
                BaseEbxWriter writer = BaseEbxWriter.CreateWriter(dataAccessor);
                writer.WritePartition(ebxAsset);
            }

            Block<byte> compressedData = Cas.CompressData(data, ProfilesLibrary.EbxCompression, 0);
            Sha1 sha1 = Sha1.HashData(compressedData);

            resources.Add(new EbxModResource(datas.Count, ebx.Name, sha1, data.Size, 0, 0, string.Empty, [], []));
            datas.Add(compressedData);
        }

        FrostyMod.Save(inPath, resources.ToArray(), datas.ToArray(), Details);

        datas.ForEach(d => d.Dispose());
    }
}