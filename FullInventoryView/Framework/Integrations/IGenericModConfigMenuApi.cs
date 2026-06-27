using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView.Framework.Integrations;

/// <summary>Subset of the Generic Mod Config Menu API used by this mod.</summary>
public interface IGenericModConfigMenuApi
{
    void Register(
        IManifest mod,
        Action reset,
        Action save,
        bool titleScreenOnly = false
    );

    void AddSectionTitle(
        IManifest mod,
        Func<string> text,
        Func<string>? tooltip = null
    );

    void AddBoolOption(
        IManifest mod,
        Func<bool> getValue,
        Action<bool> setValue,
        Func<string> name,
        Func<string>? tooltip = null,
        string? fieldId = null
    );
}
