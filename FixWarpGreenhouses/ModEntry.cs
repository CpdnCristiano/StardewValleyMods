using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FixWarpGreenhouses.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.FixWarpGreenhouses;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        HarmonyPatcher.Apply(this, new LocationRequestPatcher());
    }
}
