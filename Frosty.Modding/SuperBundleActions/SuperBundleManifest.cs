using System;

using Frosty.Modding.Archive;
using Frosty.Modding.ModInfos;
using Frosty.Sdk.Managers.Infos;

namespace Frosty.Modding;

internal class SuperBundleManifest : IDisposable
{
    public void Dispose()
    {
        // TODO: release managed resources here
    }
}

public partial class FrostyModExecutor
{
    private void ModSuperBundleManifest(SuperBundleInstallChunk inSbIc, SuperBundleModInfo inModInfo,
        InstallChunkWriter inInstallChunkWriter)
    {

    }
}