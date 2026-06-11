using System;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class GiftSenderPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName("StardewArchipelago.Archipelago.Gifting.GiftSender");
                if (targetType == null)
                {
                    return;
                }

                PatchMethod(harmony, targetType, "GiveJojaPrimeInformationToPlayer", new[] { typeof(string), typeof(string), typeof(string), typeof(int) }, nameof(GiveJojaPrimeInformationToPlayer_Prefix));
                PatchMethod(harmony, targetType, "GiveTaxFeedbackToPlayer", new[] { typeof(string), typeof(int) }, nameof(GiveTaxFeedbackToPlayer_Prefix));
                PatchMethod(harmony, targetType, "GiveCantAffordTaxFeedbackToPlayer", new[] { typeof(int), typeof(int), typeof(double) }, nameof(GiveCantAffordTaxFeedbackToPlayer_Prefix));
                PatchMethod(harmony, targetType, "SendGift", new[] { typeof(string), typeof(bool) }, nameof(SendGift_Prefix));
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed to patch GiftSender: {ex}", LogLevel.Error);
            }
        }

        private static void PatchMethod(Harmony harmony, Type targetType, string methodName, Type[] args, string prefixName)
        {
            var method = AccessTools.Method(targetType, methodName, args);
            if (method == null)
            {
                return;
            }

            harmony.Patch(
                original: method,
                prefix: new HarmonyMethod(typeof(GiftSenderPatcher), prefixName)
            );
        }

        public static bool SendGift_Prefix(object __instance, string slotName, bool isTrap)
        {
            try
            {
                var trySendGiftToBundle = AccessTools.Method(__instance.GetType(), "TrySendGiftToBundle");
                if (trySendGiftToBundle?.Invoke(__instance, new object[] { slotName }) is bool sentToBundle && sentToBundle)
                {
                    return false;
                }

                var archipelago = GetFieldValue(__instance, "_archipelago");
                if (archipelago == null)
                {
                    return true;
                }

                if (!(InvokeMethod<bool>(archipelago, "PlayerExists", slotName)))
                {
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("gift.player_not_found", new { player = slotName }).ToString(),
                        Color.Gold
                    );
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in SendGift_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool GiveJojaPrimeInformationToPlayer_Prefix(object __instance, string recipient, string giftOrTrap, string giftName, int giftAmount)
        {
            try
            {
                if (InvokeMethod<bool>(__instance, "IsAyeishaHere", null))
                {
                    return false;
                }

                Game1.chatBox?.addMessage(
                    ModEntry.Translation.Get(
                        "gift.joja_prime_delivery",
                        new { recipient, kind = giftOrTrap, amount = giftAmount, item = giftName }
                    ).ToString(),
                    Color.Gold
                );
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in GiveJojaPrimeInformationToPlayer_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool GiveTaxFeedbackToPlayer_Prefix(object __instance, string recipient, int tax)
        {
            try
            {
                if (TryGetAyeisha(__instance, out var ayeisha))
                {
                    var tomorrowSentence = string.IsNullOrWhiteSpace(recipient)
                        ? ModEntry.Translation.Get("gift.ayeisha.tomorrow_generic").ToString()
                        : ModEntry.Translation.Get("gift.ayeisha.tomorrow_recipient", new { recipient }).ToString();
                    var dialogueText = ModEntry.Translation.Get(
                        "gift.ayeisha.delivery_cost",
                        new { tomorrow = tomorrowSentence, tax }
                    ).ToString();
                    var dialogue = new Dialogue(ayeisha, null, dialogueText);
                    ayeisha.setNewDialogue(dialogue);
                    Game1.drawDialogue(ayeisha);
                    return false;
                }

                Game1.chatBox?.addMessage(
                    ModEntry.Translation.Get("gift.tax_charged", new { tax }).ToString(),
                    Color.Gold
                );
                Game1.chatBox?.addMessage(
                    ModEntry.Translation.Get("gift.joja_prime_thank_you").ToString(),
                    Color.Gold
                );
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in GiveTaxFeedbackToPlayer_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        public static bool GiveCantAffordTaxFeedbackToPlayer_Prefix(object __instance, int itemValue, int tax, double taxRate)
        {
            try
            {
                if (TryGetAyeisha(__instance, out var ayeisha))
                {
                    ayeisha.setNewDialogue(
                        ModEntry.Translation.Get("gift.ayeisha.cant_afford", new { player = Game1.player.Name, tax }).ToString()
                    );
                    Game1.drawDialogue(ayeisha);
                    return false;
                }

                Game1.chatBox?.addMessage(
                    ModEntry.Translation.Get("gift.cant_afford_tax").ToString(),
                    Color.Gold
                );
                Game1.chatBox?.addMessage(
                    ModEntry.Translation.Get("gift.tax_formula", new { percent = taxRate * 100, value = itemValue, tax }).ToString(),
                    Color.Gold
                );
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log($"Failed in GiveCantAffordTaxFeedbackToPlayer_Prefix: {ex}", LogLevel.Error);
                return true;
            }
        }

        private static bool TryGetAyeisha(object giftSender, out NPC ayeisha)
        {
            ayeisha = null!;
            var args = new object?[] { null };
            var method = AccessTools.Method(giftSender.GetType(), "IsAyeishaHere");
            if (method == null)
            {
                return false;
            }

            var result = method.Invoke(giftSender, args);
            if (result is not bool success || !success || args[0] is not NPC npc)
            {
                return false;
            }

            ayeisha = npc;
            return true;
        }

        private static T InvokeMethod<T>(object target, string name, params object?[] args)
        {
            var method = AccessTools.Method(target.GetType(), name);
            if (method == null)
            {
                return default!;
            }

            var result = method.Invoke(target, args);
            return result is T typed ? typed : default!;
        }

        private static object? GetFieldValue(object target, string fieldName)
        {
            var field = AccessTools.Field(target.GetType(), fieldName);
            return field?.GetValue(target);
        }
    }
}
