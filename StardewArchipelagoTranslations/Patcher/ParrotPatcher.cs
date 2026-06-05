using System;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ParrotPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var type = AccessTools.TypeByName(
                    "StardewArchipelago.Locations.ParrotUpgradePerchArchipelago"
                );
                if (type == null)
                {
                    ModEntry.Instance.Monitor.Log(
                        "ParrotUpgradePerchArchipelago type not found, skipping parrot patch.",
                        LogLevel.Debug
                    );
                    return;
                }

                var constructor = type.GetConstructor(
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                    null,
                    new Type[]
                    {
                        typeof(string),
                        AccessTools.TypeByName(
                            "StardewArchipelago.Archipelago.StardewArchipelagoClient"
                        ),
                        typeof(StardewValley.GameLocation),
                        typeof(Microsoft.Xna.Framework.Point),
                        typeof(Microsoft.Xna.Framework.Rectangle),
                        typeof(int),
                        typeof(Action),
                        typeof(Func<bool>),
                        typeof(string),
                        typeof(string),
                    },
                    null
                );

                if (constructor != null)
                {
                    harmony.Patch(
                        original: constructor,
                        postfix: new HarmonyMethod(
                            typeof(ParrotPatcher),
                            nameof(Constructor_Postfix)
                        )
                    );
                    ModEntry.Instance.Monitor.Log(
                        "Successfully patched ParrotUpgradePerchArchipelago constructor.",
                        LogLevel.Info
                    );
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch ParrotUpgradePerchArchipelago: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void Constructor_Postfix(object __instance)
        {
            try
            {
                var field = __instance
                    .GetType()
                    .GetField("_scoutedItemName", BindingFlags.Instance | BindingFlags.NonPublic);
                if (field != null)
                {
                    var val = field.GetValue(__instance) as string;
                    if (!string.IsNullOrEmpty(val))
                    {
                        var localized = TranslationHelper.GetLocalizedItemName(val);
                        field.SetValue(__instance, localized);
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error localizing parrot perch item name: {ex}",
                    LogLevel.Error
                );
            }
        }
    }
}
