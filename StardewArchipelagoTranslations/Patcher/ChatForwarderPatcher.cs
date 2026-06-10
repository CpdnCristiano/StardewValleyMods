using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ChatForwarderPatcher
    {
        private const string CommandPrefix = "!!";

        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StardewArchipelago.Archipelago.ChatForwarder");
                if (targetType == null)
                {
                    return;
                }

                var method = AccessTools.Method(targetType, "PrintCommandHelp");
                if (method == null)
                {
                    return;
                }

                harmony.Patch(
                    original: method,
                    prefix: new HarmonyMethod(typeof(ChatForwarderPatcher), nameof(PrintCommandHelp_Prefix))
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch ChatForwarder: {ex}", LogLevel.Error);
            }
        }

        public static bool PrintCommandHelp_Prefix()
        {
            try
            {
                Game1.chatBox?.addMessage($"{CommandPrefix}help - {ModEntry.Translation.Get("commands.help.help")}", Color.Gold);
                Game1.chatBox?.addMessage($"{CommandPrefix}goal - {ModEntry.Translation.Get("commands.help.goal")}", Color.Gold);
                Game1.chatBox?.addMessage($"{CommandPrefix}experience - {ModEntry.Translation.Get("commands.help.experience")}", Color.Gold);
                Game1.chatBox?.addMessage($"{CommandPrefix}bank [deposit|withdraw] [amount] - {ModEntry.Translation.Get("commands.help.bank")}", Color.Gold);

                if (GetSlotDataBool("Gifting"))
                {
                    Game1.chatBox?.addMessage($"{CommandPrefix}gift [slotName] - {ModEntry.Translation.Get("commands.help.gift")}", Color.Gold);
                }

                Game1.chatBox?.addMessage($"{CommandPrefix}unstuck - {ModEntry.Translation.Get("commands.help.unstuck")}", Color.Gold);
                Game1.chatBox?.addMessage($"{CommandPrefix}sleep - {ModEntry.Translation.Get("commands.help.sleep")}", Color.Gold);

                if (IsPrankDay())
                {
                    Game1.chatBox?.addMessage($"{CommandPrefix}fish - {ModEntry.Translation.Get("commands.help.fish")}", Color.Orange);
                }

#if DEBUG
                Game1.chatBox?.addMessage($"{CommandPrefix}sprite - {ModEntry.Translation.Get("commands.help.sprite")}", Color.Gold);
                Game1.chatBox?.addMessage($"{CommandPrefix}sync - {ModEntry.Translation.Get("commands.help.sync")}", Color.Gold);
#endif

                Game1.chatBox?.addMessage($"{CommandPrefix}arcade_release [game] - {ModEntry.Translation.Get("commands.help.arcade_release")}", Color.Gold);

                if (GetTilesanityName() != "Nope")
                {
                    Game1.chatBox?.addMessage($"{CommandPrefix}where - {ModEntry.Translation.Get("commands.help.where")}", Color.Gold);
                    Game1.chatBox?.addMessage($"{CommandPrefix}tilesanity_ui - {ModEntry.Translation.Get("commands.help.tilesanity_ui")}", Color.Gold);
                    Game1.chatBox?.addMessage($"{CommandPrefix}tilesanity_ui_black - {ModEntry.Translation.Get("commands.help.tilesanity_ui_black")}", Color.Gold);
                }

                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in PrintCommandHelp_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        private static bool GetSlotDataBool(string memberName)
        {
            var chatForwarderType = AccessTools.TypeByName("StardewArchipelago.Archipelago.ChatForwarder");
            if (chatForwarderType == null)
            {
                return false;
            }

            var archipelago = AccessTools.Field(chatForwarderType, "_archipelago")?.GetValue(null);
            if (archipelago == null)
            {
                return false;
            }

            var slotData = GetMemberValue(archipelago, "SlotData");
            return slotData != null && GetMemberValue<bool>(slotData, memberName);
        }

        private static string GetTilesanityName()
        {
            var chatForwarderType = AccessTools.TypeByName("StardewArchipelago.Archipelago.ChatForwarder");
            if (chatForwarderType == null)
            {
                return string.Empty;
            }

            var archipelago = AccessTools.Field(chatForwarderType, "_archipelago")?.GetValue(null);
            var slotData = archipelago == null ? null : GetMemberValue(archipelago, "SlotData");
            return GetMemberValue(slotData, "Tilesanity")?.ToString() ?? string.Empty;
        }

        private static bool IsPrankDay()
        {
            var foolManagerType = AccessTools.TypeByName("StardewArchipelago.GameModifications.FoolManager");
            var method = AccessTools.Method(foolManagerType, "IsPrankDay");
            return method?.Invoke(null, null) is bool result && result;
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
    }
}
