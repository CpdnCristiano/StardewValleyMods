using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewArchipelago.GameModifications.CodeInjections.Powers;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    [HarmonyPatch(typeof(PowersModifier), "AddPower")]
    public static class PowersPatcher
    {
        [HarmonyPostfix]
        public static void Postfix(object __instance, IDictionary<string, StardewValley.GameData.Powers.PowersData> powersData, ArchipelagoPower customPower)
        {
            try
            {
                if (powersData.TryGetValue(customPower.Name, out var powerData))
                {
                    var sanitizedName = customPower.Name.Replace(" ", "_").Replace("'", "").ToLower();
                    var nameKey = $"power.{sanitizedName}.name";
                    var descKey = $"power.{sanitizedName}.description";

                    if (ModEntry.Translation.ContainsKey(nameKey))
                    {
                        powerData.DisplayName = ModEntry.Translation.Get(nameKey).ToString();
                    }
                    if (ModEntry.Translation.ContainsKey(descKey))
                    {
                        powerData.Description = ModEntry.Translation.Get(descKey).ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in AddPower_Postfix: {ex}", LogLevel.Error);
            }
        }
    }

    [HarmonyPatch(typeof(PowersModifier), nameof(PowersModifier.PerformHoverAction_AddTooltipsOnApItems_Postfix))]
    public static class PowersTooltipPatcher
    {
        [HarmonyPostfix]
        public static void Postfix(PowersTab __instance)
        {
            try
            {
                PowersTooltipLocalization.TryLocalizeDescriptionText(__instance);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in PowersTooltipPatcher: {ex.Message}", LogLevel.Trace);
            }
        }
    }

    [HarmonyPatch(typeof(PowersTab), nameof(PowersTab.draw))]
    public static class PowersTabDrawTooltipPatcher
    {
        [HarmonyPrefix]
        public static void Prefix(PowersTab __instance, SpriteBatch b)
        {
            try
            {
                PowersTooltipLocalization.TryLocalizeDescriptionText(__instance);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Error in PowersTabDrawTooltipPatcher: {ex.Message}", LogLevel.Trace);
            }
        }
    }

    internal static class PowersTooltipLocalization
    {
        public static void TryLocalizeDescriptionText(PowersTab powersTab)
        {
            if (powersTab == null || string.IsNullOrWhiteSpace(powersTab.descriptionText))
            {
                return;
            }

            var rawDescription = powersTab.descriptionText;
            var normalizedDescription = Regex.Replace(rawDescription, @"\s+", " ").Trim();

            if (normalizedDescription.Equals("You can hint this item", StringComparison.OrdinalIgnoreCase))
            {
                powersTab.descriptionText = ModEntry.Translation.Get("powers.hint_available").ToString();
                return;
            }

            var match = Regex.Match(normalizedDescription, @"^At\s+(.+?)'s\s+(.+)$", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var findingPlayerName = match.Groups[1].Value.Trim();
                var locationName = match.Groups[2].Value.Trim();
                var localizedLocation = TranslationHelper.GetLocalizedLocationName(locationName);

                powersTab.descriptionText = ModEntry.Translation.Get(
                    "powers.hint_at_format",
                    new { player = findingPlayerName, location = localizedLocation }
                ).ToString();
            }
        }
    }
}
