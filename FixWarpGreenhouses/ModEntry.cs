using CpdnCristiano.StardewValleyMods.Common.Patching;
using CpdnCristiano.StardewValleyMods.FixWarpGreenhouses.Patcher;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMods.FixWarpGreenhouses;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        HarmonyPatcher.Apply(this, new LocationRequestPatcher());
    }
}
