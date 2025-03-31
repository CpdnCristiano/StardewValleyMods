using System;
using CpdnCristiano.StardewValleyMods.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMods.FixWarpGreenhouses.Patcher
{
    internal class LocationRequestPatcher : BasePatcher
    {
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            harmony.Patch(
                original: this.RequireMethod<Game1>(nameof(Game1.getLocationRequest)),
                prefix: this.GetHarmonyMethod(nameof(getLocationRequest_Prefix))
            );
        }

        public static void getLocationRequest_Prefix(ref string locationName)
        {
            if (string.Equals(locationName, "Greenhouse", StringComparison.OrdinalIgnoreCase))
            {
                string currentLocation = Game1.currentLocation?.NameOrUniqueName ?? "Farm";

                if (
                    currentLocation.StartsWith("Greenhouse", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        currentLocation,
                        locationName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    locationName = currentLocation;
                }
            }
        }
    }
}
