using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class JunimoShopStockModifierPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                ApplyTranslations();
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch JunimoShopStockModifier: {ex}",
                    LogLevel.Error
                );
            }
        }

        private static void ApplyTranslations()
        {
            var targetType = AccessTools.TypeByName(
                "StardewArchipelago.GameModifications.Modded.JunimoShopStockModifier"
            );
            if (targetType == null)
            {
                return;
            }

            var field = AccessTools.Field(targetType, "_junimoPhrase");
            if (field?.GetValue(null) is not Dictionary<string, string> phrases)
            {
                return;
            }

            ApplyPhrase(phrases, "Orange", "junimo.orange");
            ApplyPhrase(phrases, "Red", "junimo.red");
            ApplyPhrase(phrases, "Grey", "junimo.grey");
            ApplyPhrase(phrases, "Yellow", "junimo.yellow");
            ApplyPhrase(phrases, "Blue", "junimo.blue");
            ApplyPhrase(phrases, "Purple", "junimo.purple");
        }

        private static void ApplyPhrase(
            Dictionary<string, string> phrases,
            string color,
            string translationKey
        )
        {
            var translated = ModEntry.Translation.Get(translationKey).ToString();
            if (!string.IsNullOrWhiteSpace(translated))
            {
                phrases[color] = translated;
            }
        }
    }
}
