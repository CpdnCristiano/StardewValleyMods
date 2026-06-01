using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewArchipelago.Locations.InGameLocations;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ShopPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StardewArchipelago.Locations.InGameLocations.ObtainableArchipelagoLocation");
                if (targetType != null)
                {
                    // Patch DisplayName getter
                    var displayNameGetter = AccessTools.PropertyGetter(targetType, "DisplayName");
                    if (displayNameGetter != null)
                    {
                        var displayNamePostfix = new HarmonyMethod(typeof(ShopPatcher), nameof(DisplayName_Postfix));
                        harmony.Patch(displayNameGetter, postfix: displayNamePostfix);
                    }

                    // Patch getDescription method
                    var getDescriptionMethod = AccessTools.Method(targetType, "getDescription");
                    if (getDescriptionMethod != null)
                    {
                        var getDescriptionPostfix = new HarmonyMethod(typeof(ShopPatcher), nameof(GetDescription_Postfix));
                        harmony.Patch(getDescriptionMethod, postfix: getDescriptionPostfix);
                    }

                    ModEntry.Instance.Monitor.Log("Successfully patched ObtainableArchipelagoLocation for shop translations!", LogLevel.Info);
                }
                else
                {
                    ModEntry.Instance.Monitor.Log("Could not find ObtainableArchipelagoLocation type in StardewArchipelago assembly.", LogLevel.Warn);
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch ObtainableArchipelagoLocation: {ex}", LogLevel.Error);
            }
        }

        public static void DisplayName_Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(__result)) return;

                __result = TranslationHelper.GetLocalizedLocationName(__result);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in DisplayName_Postfix: {ex}", LogLevel.Error);
            }
        }

        public static void GetDescription_Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(__result)) return;

                __result = TranslationHelper.TranslateDescription(__result);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in GetDescription_Postfix: {ex}", LogLevel.Error);
            }
        }
    }
}
