using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.ArtisanQualityDowngrade.Patcher;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewValley;
using StardewValley.GameData.Machines;
using StardewValley.ItemTypeDefinitions;

namespace CpdnCristiano.StardewValleyMod.ArtisanQualityDowngrade;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        // Apply Harmony patches
        HarmonyPatcher.Apply(this, new MachineOutputPatcher());

        // Edit Data/Machines asset to set CopyQuality = true for artisan goods
        helper.Events.Content.AssetRequested += this.OnAssetRequested;
    }

    private void OnAssetRequested(object? sender, AssetRequestedEventArgs e)
    {
        if (Context.IsWorldReady && e.NameWithoutLocale.IsEquivalentTo("Data/Machines"))
        {
            e.Edit(delegate(IAssetData asset)
            {
                var data = asset.AsDictionary<string, MachineData>().Data;
                foreach (var machine in data.Values)
                {
                    if (machine.OutputRules == null)
                        continue;

                    foreach (var outputRule in machine.OutputRules)
                      {
                        if (outputRule.OutputItem == null)
                            continue;

                        foreach (var item in outputRule.OutputItem)
                        {
                            string itemId = item.ItemId;
                            if (!string.IsNullOrEmpty(itemId) && !itemId.Equals("DROP_IN", StringComparison.OrdinalIgnoreCase))
                            {
                                string qualifiedId = ItemRegistry.QualifyItemId(itemId) ?? itemId;
                                ParsedItemData itemData = ItemRegistry.GetData(qualifiedId);
                                bool isArtisan = itemData != null && itemData.Category == -26;

                                if (isArtisan && item.Quality <= 0 && !item.CopyQuality)
                                {
                                    item.CopyQuality = true;
                                }
                            }
                        }
                    }
                }
            }, AssetEditPriority.Late);
        }
    }
}
