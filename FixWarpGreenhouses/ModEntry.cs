using CpdnCristiano.StardewValleyMods.Common.Patching;
using CpdnCristiano.StardewValleyMods.FixWarpGreenhouses.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMods.FixWarpGreenhouses
{
    public class ModEntry : Mod
    {
        public override void Entry(IModHelper helper)
        {
            var patches = new List<IPatcher> { new LocationRequestPatcher() };
            HarmonyPatcher.Apply(this, patches.ToArray());
        }
    }
}
