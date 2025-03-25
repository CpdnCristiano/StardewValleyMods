using CpdnCristiano.StardewValleyMods.Common.Patching;
using CpdnCristiano.StardewValleyMods.FullInventoryView.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMods.FullInventoryView
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var patches = new List<IPatcher> { new InventoryMenuPatcher() };
            HarmonyPatcher.Apply(this, patches.ToArray());
        }
    }
}
