using System;
using HarmonyLib;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ConnectionResultsPatcher
    {
        public static void Patch(Harmony harmony)
        {
            var informationDialogType = AccessTools.TypeByName("StardewArchipelago.GameModifications.InformationDialog");
            if (informationDialogType != null)
            {
                var ctor = AccessTools.Constructor(informationDialogType, new[] { typeof(string), typeof(StardewValley.Menus.ConfirmationDialog.behavior), typeof(StardewValley.Menus.ConfirmationDialog.behavior) });
                if (ctor != null)
                {
                    harmony.Patch(
                        ctor,
                        prefix: new HarmonyMethod(typeof(ConnectionResultsPatcher), nameof(TranslateDialogMessage_Prefix))
                    );
                }
            }

            var reconnectDialogType = AccessTools.TypeByName("StardewArchipelago.GameModifications.ReconnectDialog");
            if (reconnectDialogType != null)
            {
                var ctor = AccessTools.GetDeclaredConstructors(reconnectDialogType)[0];
                if (ctor != null)
                {
                    harmony.Patch(
                        ctor,
                        prefix: new HarmonyMethod(typeof(ConnectionResultsPatcher), nameof(TranslateDialogMessage_Prefix))
                    );
                }
            }
        }

        public static void TranslateDialogMessage_Prefix(ref string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return;
            }

            message = TranslateConnectionMessage(message);
        }

        private static string TranslateConnectionMessage(string message)
        {
            if (message.StartsWith("You cannot play Archipelago with the following unsupported mods:", StringComparison.Ordinal))
            {
                return message.Replace(
                    "You cannot play Archipelago with the following unsupported mods:",
                    ModEntry.Translation.Get("connection.incompatible_mods.header").ToString()
                );
            }

            if (message.StartsWith("The slot you are connecting to has been created expecting modded content,", StringComparison.Ordinal))
            {
                return message.Replace(
                    "The slot you are connecting to has been created expecting modded content,\r\nbut not all expected mods are installed and active.",
                    ModEntry.Translation.Get("connection.missing_mods.header").ToString()
                ).Replace(
                    "Mod: ",
                    ModEntry.Translation.Get("connection.mod_prefix").ToString()
                ).Replace(
                    "expected version: ",
                    ModEntry.Translation.Get("connection.expected_version").ToString()
                ).Replace(
                    "current Version: ",
                    ModEntry.Translation.Get("connection.current_version").ToString()
                );
            }

            if (message.StartsWith("The slot you are connecting to requires a content patcher,", StringComparison.Ordinal))
            {
                return message.Replace(
                    "The slot you are connecting to requires a content patcher,\r\n mod, but not all expected mods are installed and active.",
                    ModEntry.Translation.Get("connection.missing_requirements.header").ToString()
                ).Replace(
                    "Mod: ",
                    ModEntry.Translation.Get("connection.mod_prefix").ToString()
                ).Replace(
                    "expected version: ",
                    ModEntry.Translation.Get("connection.expected_version").ToString()
                ).Replace(
                    "current Version: ",
                    ModEntry.Translation.Get("connection.current_version").ToString()
                );
            }

            if (message.StartsWith("The game being loaded has no connection information.", StringComparison.Ordinal))
            {
                return ModEntry.Translation.Get("connection.no_connection_info").ToString();
            }

            return message;
        }
    }
}
