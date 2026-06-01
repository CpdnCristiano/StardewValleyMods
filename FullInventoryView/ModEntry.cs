using CpdnCristiano.StardewValleyMod.Common.Log;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FullInventoryView.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.FullInventoryView;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        Log.Init(Monitor);
        var patches = new List<IPatcher> { new InventoryMenuPatcher() };
        if (Helper.ModRegistry.IsLoaded("Annosz.UiInfoSuite2"))
        {
            patches.Add(new UiInfo2Patcher());
        }
        else if (Helper.ModRegistry.IsLoaded("DazUki.UIInfoSuite2Alt"))
        {
            patches.Add(new UiInfo2AltPatcher());
        }
        HarmonyPatcher.Apply(this, patches.ToArray());
    }
}
