using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class HintHelperPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StardewArchipelago.Archipelago.HintHelper");
                if (targetType == null)
                {
                    return;
                }

                var method = AccessTools.Method(targetType, "GiveHintTip");
                if (method == null)
                {
                    return;
                }

                harmony.Patch(
                    original: method,
                    prefix: new HarmonyMethod(typeof(HintHelperPatcher), nameof(GiveHintTip_Prefix))
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch HintHelper: {ex}", LogLevel.Error);
            }
        }

        public static bool GiveHintTip_Prefix(object __instance, object session)
        {
            try
            {
                if (session == null)
                {
                    return false;
                }

                if (!TryGetCurrentHintCost(session, out var hintCost))
                {
                    return false;
                }

                var roomState = GetMemberValue<object>(session, "RoomState");
                var hintPoints = GetMemberValue<int>(roomState, "HintPoints");
                var canAffordHintToday = hintPoints >= hintCost;
                var canAffordHintYesterday = GetFieldValue<bool>(__instance, "_canAffordHintYesterday");

                if (!canAffordHintYesterday && canAffordHintToday)
                {
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("hints.tip_available").ToString(),
                        Color.Gold
                    );
                }

                SetFieldValue(__instance, "_canAffordHintYesterday", canAffordHintToday);
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in GiveHintTip_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        private static bool TryGetCurrentHintCost(object session, out int hintCost)
        {
            hintCost = 0;

            var roomState = GetMemberValue(session, "RoomState");
            if (roomState == null)
            {
                return false;
            }

            hintCost = GetMemberValue<int>(roomState, "HintCost");
            if (hintCost > 0)
            {
                return true;
            }

            var locations = GetMemberValue(session, "Locations");
            var allLocations = locations == null ? null : GetMemberValue(locations, "AllLocations");
            var allLocationsCount = allLocations == null ? 0 : GetMemberValue<int>(allLocations, "Count");
            var hintCostPercentage = Convert.ToDecimal(GetMemberValue(roomState, "HintCostPercentage"));

            hintCost = (int)Math.Max(0M, allLocationsCount * 0.01M * hintCostPercentage);
            return hintCost > 0;
        }

        private static T GetMemberValue<T>(object target, string name)
        {
            var value = GetMemberValue(target, name);
            return value is T typed ? typed : default!;
        }

        private static object? GetMemberValue(object? target, string name)
        {
            if (target == null)
            {
                return null;
            }

            var property = AccessTools.Property(target.GetType(), name);
            if (property != null)
            {
                return property.GetValue(target);
            }

            var field = AccessTools.Field(target.GetType(), name);
            if (field != null)
            {
                return field.GetValue(target);
            }

            return null;
        }

        private static T GetFieldValue<T>(object target, string name)
        {
            var field = AccessTools.Field(target.GetType(), name);
            var value = field?.GetValue(target);
            return value is T typed ? typed : default!;
        }

        private static void SetFieldValue(object target, string name, object value)
        {
            var field = AccessTools.Field(target.GetType(), name);
            field?.SetValue(target, value);
        }
    }
}
