namespace Frosty.Sdk.Interfaces;

/// <summary>
///     Loads the bundles and assets of all superbundles, in the format of one <see cref="Managers.BundleFormat" />.
/// </summary>
public interface IAssetLoader
{
    /// <summary>
    ///     Loads all superbundles, adds their bundles and assets to the managers.
    /// </summary>
    void Load();
}
