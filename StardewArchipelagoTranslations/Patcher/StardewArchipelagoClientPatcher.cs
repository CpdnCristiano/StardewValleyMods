using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class StardewArchipelagoClientPatcher
    {
        public static void Patch(Harmony harmony)
        {
            var targetType = AccessTools.TypeByName("StardewArchipelago.Archipelago.StardewArchipelagoClient");
            if (targetType == null)
            {
                return;
            }

            PatchMethod(harmony, targetType, "OnError", new[] { typeof(string), typeof(Exception) }, nameof(OnError_Prefix));
            PatchMethod(harmony, targetType, "KillPlayerDeathLink", new[] { AccessTools.TypeByName("Archipelago.MultiClient.Net.BounceFeatures.DeathLink.DeathLink")! }, nameof(KillPlayerDeathLink_Prefix));
        }

        private static void PatchMethod(Harmony harmony, Type targetType, string methodName, Type[] args, string prefixName)
        {
            var method = AccessTools.Method(targetType, methodName, args);
            if (method == null)
            {
                return;
            }

            harmony.Patch(
                method,
                prefix: new HarmonyMethod(typeof(StardewArchipelagoClientPatcher), prefixName)
            );
        }

        public static bool OnError_Prefix(string message, Exception e)
        {
            Game1.chatBox?.addMessage(
                ModEntry.Translation.Get("client.connection_lost").ToString(),
                Color.Red
            );
            return false;
        }

        public static bool KillPlayerDeathLink_Prefix(object __instance, object deathlink)
        {
            var deathManager = AccessTools.Field(__instance.GetType(), "_deathManager")?.GetValue(__instance);
            AccessTools.Method(deathManager?.GetType(), "ReceiveDeathLink")?.Invoke(deathManager, null);

            var source = deathlink.GetType().GetProperty("Source")?.GetValue(deathlink)?.ToString() ?? string.Empty;
            var cause = deathlink.GetType().GetProperty("Cause")?.GetValue(deathlink)?.ToString() ?? string.Empty;
            var deathLinkMessage = ModEntry.Translation.Get("client.deathlink", new { source, cause }).ToString();
            Game1.chatBox?.addInfoMessage(deathLinkMessage);
            return false;
        }
    }
}
