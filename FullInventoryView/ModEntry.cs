using CpdnCristiano.StardewValleyMods.Common.Log;
using CpdnCristiano.StardewValleyMods.Common.Patching;
using CpdnCristiano.StardewValleyMods.FullInventoryView.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMods.FullInventoryView
{
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
            HarmonyPatcher.Apply(this, patches.ToArray());
        }
    }
}
