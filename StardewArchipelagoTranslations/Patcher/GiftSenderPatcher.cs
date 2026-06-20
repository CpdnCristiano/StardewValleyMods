using System;
using System.Linq;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class GiftSenderPatcher
    {
        public static void Patch(Harmony harmony)
        {
            var crossGiftHandlerType = AccessTools.TypeByName("StardewArchipelago.Archipelago.Gifting.CrossGiftHandler");
            if (crossGiftHandlerType != null)
            {
                var handleGiftItemCommandMethod = AccessTools.Method(crossGiftHandlerType, "HandleGiftItemCommand", new[] { typeof(string) });
                if (handleGiftItemCommandMethod != null)
                {
                    harmony.Patch(
                        handleGiftItemCommandMethod,
                        prefix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(HandleGiftItemCommand_Prefix))
                    );
                }
            }

            var targetType = AccessTools.TypeByName("StardewArchipelago.Archipelago.Gifting.GiftSender");
            if (targetType == null)
            {
                return;
            }

            var sendGiftMethod = AccessTools.Method(targetType, "SendGift", new[] { typeof(string), typeof(bool) });
            if (sendGiftMethod != null)
            {
                harmony.Patch(
                    sendGiftMethod,
                    prefix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(SendGift_Prefix)),
                    postfix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(SendGift_Postfix))
                );
            }

            var giveJojaPrimeInformationMethod = AccessTools.Method(
                targetType,
                "GiveJojaPrimeInformationToPlayer",
                new[] { typeof(string), typeof(string), typeof(string), typeof(int) }
            );
            if (giveJojaPrimeInformationMethod != null)
            {
                harmony.Patch(
                    giveJojaPrimeInformationMethod,
                    prefix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(GiveJojaPrimeInformationToPlayer_Prefix))
                );
            }

            var giveTaxFeedbackMethod = AccessTools.Method(
                targetType,
                "GiveTaxFeedbackToPlayer",
                new[] { typeof(string), typeof(int) }
            );
            if (giveTaxFeedbackMethod != null)
            {
                harmony.Patch(
                    giveTaxFeedbackMethod,
                    prefix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(GiveTaxFeedbackToPlayer_Prefix))
                );
            }

            var giveCantAffordTaxFeedbackMethod = AccessTools.Method(
                targetType,
                "GiveCantAffordTaxFeedbackToPlayer",
                new[] { typeof(int), typeof(int), typeof(double) }
            );
            if (giveCantAffordTaxFeedbackMethod != null)
            {
                harmony.Patch(
                    giveCantAffordTaxFeedbackMethod,
                    prefix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(GiveCantAffordTaxFeedbackToPlayer_Prefix))
                );
            }

            var generatorType = AccessTools.TypeByName("StardewArchipelago.Archipelago.Gifting.GiftGenerator");
            if (generatorType != null)
            {
                var tryCreateGiftItemMethod = AccessTools.GetDeclaredMethods(generatorType)
                    .FirstOrDefault(m => m.Name == "TryCreateGiftItem");
                if (tryCreateGiftItemMethod != null)
                {
                    harmony.Patch(
                        tryCreateGiftItemMethod,
                        postfix: new HarmonyMethod(typeof(GiftSenderPatcher), nameof(TryCreateGiftItem_Postfix))
                    );
                }
            }
        }

        public static bool HandleGiftItemCommand_Prefix(object __instance, string message, ref bool __result)
        {
            try
            {
                var archipelago = GetFieldValue(__instance, "_archipelago");
                if (archipelago == null || !GetPropertyValue<bool>(GetPropertyValue(archipelago, "SlotData")!, "Gifting"))
                {
                    return true;
                }

                var commandPrefix = AccessTools.TypeByName("StardewArchipelago.Archipelago.ChatForwarder")
                    ?.GetField("COMMAND_PREFIX", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
                    ?.GetValue(null)?.ToString() ?? "!!";

                var giftPrefix = $"{commandPrefix}gift";
                var trapPrefix = $"{commandPrefix}trap";
                var giftPrefixWithSpace = $"{giftPrefix} ";
                var trapPrefixWithSpace = $"{trapPrefix} ";
                var isGift = message.StartsWith(giftPrefixWithSpace);
                var isTrap = message.StartsWith(trapPrefixWithSpace);
                if (!isGift && !isTrap)
                {
                    if (message.StartsWith(giftPrefix, StringComparison.Ordinal) || message.StartsWith(trapPrefix, StringComparison.Ordinal))
                    {
                        Game1.chatBox?.addMessage(
                            ModEntry.Translation.Get("gift.usage").ToString(),
                            Color.Gold
                        );
                        __result = true;
                        return false;
                    }

                    return true;
                }

                var receiverSlotName = isTrap ? message[trapPrefixWithSpace.Length..] : message[giftPrefixWithSpace.Length..];
                var slotData = GetPropertyValue(archipelago, "SlotData");
                var currentSlotName = GetPropertyValue<string>(slotData!, "SlotName");
                if (!string.IsNullOrWhiteSpace(currentSlotName) && receiverSlotName == currentSlotName)
                {
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("gift.self_send_blocked").ToString(),
                        Color.Gold
                    );
                    __result = true;
                    return false;
                }

                return true;
            }
            catch
            {
                return true;
            }
        }

        public static bool SendGift_Prefix(object __instance, string slotName, bool isTrap)
        {
            try
            {
                _pendingGiftKind = ModEntry.Translation.Get(isTrap ? "gift.kind.trap" : "gift.kind.gift").ToString();
                var archipelago = GetFieldValue(__instance, "_archipelago");
                if (archipelago == null)
                {
                    return true;
                }

                if (!InvokeBool(archipelago, "PlayerExists", slotName))
                {
                    Game1.chatBox?.addMessage(
                        ModEntry.Translation.Get("gift.player_not_found", new { player = slotName }).ToString(),
                        Color.Gold
                    );
                    return false;
                }

                var giftGenerator = GetPropertyValue(__instance, "GiftGenerator");
                if (giftGenerator == null)
                {
                    return true;
                }

                var giftObject = Game1.player.ActiveObject;
                var tryCreateGiftItemMethod = AccessTools.Method(
                    giftGenerator.GetType(),
                    "TryCreateGiftItem"
                );
                if (tryCreateGiftItemMethod == null)
                {
                    return true;
                }

                var args = new object[] { giftObject, isTrap, null!, null!, string.Empty };
                if (!(tryCreateGiftItemMethod.Invoke(giftGenerator, args) is bool canCreateGift) || !canCreateGift)
                {
                    var errorMessage = args[4]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(errorMessage))
                    {
                        Game1.chatBox?.addMessage(errorMessage, Color.Gold);
                        return false;
                    }

                    return true;
                }

                var giftTraits = args[3] as Array;
                var giftingService = GetPropertyValue(archipelago, "GiftingService");
                if (giftingService == null || giftTraits == null)
                {
                    return true;
                }

                var canGiftMethod = AccessTools.GetDeclaredMethods(giftingService.GetType())
                    .FirstOrDefault(m => m.Name == "CanGiftToPlayer" && m.GetParameters().Length == 2);
                if (canGiftMethod == null)
                {
                    return true;
                }

                var traitNames = giftTraits
                    .Cast<object>()
                    .Select(trait => trait.GetType().GetProperty("Trait")?.GetValue(trait)?.ToString())
                    .Where(name => !string.IsNullOrWhiteSpace(name))
                    .Cast<string>()
                    .ToArray();

                var canGiftResult = canGiftMethod.Invoke(giftingService, new object[] { slotName, traitNames });
                if (canGiftResult == null)
                {
                    return true;
                }

                var canGift = GetPropertyValue<bool>(canGiftResult, "CanGift");
                if (canGift)
                {
                    return true;
                }

                var message = GetPropertyValue<string>(canGiftResult, "Message") ?? string.Empty;
                var translatedMessage = TranslateGiftServiceMessage(message);
                Game1.chatBox?.addMessage(translatedMessage, Color.Gold);
                return false;
            }
            catch
            {
                return true;
            }
        }

        [ThreadStatic]
        private static string? _pendingGiftKind;

        public static void SendGift_Postfix()
        {
            _pendingGiftKind = null;
        }

        public static bool GiveJojaPrimeInformationToPlayer_Prefix(object __instance, string recipient, string giftOrTrap, string giftName, int giftAmount)
        {
            if (TryGetAyeisha(__instance, out var ayeisha))
            {
                var tomorrowSentence = string.IsNullOrWhiteSpace(recipient)
                    ? ModEntry.Translation.Get("gift.ayeisha.tomorrow_generic").ToString()
                    : ModEntry.Translation.Get("gift.ayeisha.tomorrow_recipient", new { recipient }).ToString();
                var dialogueText = ModEntry.Translation.Get(
                    "gift.ayeisha.delivery_cost",
                    new { tomorrow = tomorrowSentence, tax = 0 }
                ).ToString();
                ayeisha.setNewDialogue(dialogueText);
                Game1.drawDialogue(ayeisha);
                return false;
            }

            var localizedKind = giftOrTrap switch
            {
                "gift" => ModEntry.Translation.Get("gift.kind.gift").ToString(),
                "trap" => ModEntry.Translation.Get("gift.kind.trap").ToString(),
                _ => giftOrTrap,
            };

            var localizedItem = GetBestLocalizedGiftItemName(giftName);
            Game1.chatBox?.addMessage(
                ModEntry.Translation.Get(
                    "gift.joja_prime_delivery",
                    new { recipient, kind = localizedKind, amount = giftAmount, item = localizedItem }
                ).ToString(),
                Color.Gold
            );
            return false;
        }

        public static bool GiveTaxFeedbackToPlayer_Prefix(object __instance, string recipient, int tax)
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
                ayeisha.setNewDialogue(dialogueText);
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

        public static bool GiveCantAffordTaxFeedbackToPlayer_Prefix(object __instance, int itemValue, int tax, double taxRate)
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
                ModEntry.Translation.Get(
                    "gift.tax_formula",
                    new { percent = taxRate * 100, value = itemValue, tax }
                ).ToString(),
                Color.Gold
            );
            return false;
        }

        public static void TryCreateGiftItem_Postfix(
            bool isTrap,
            ref bool __result,
            ref string failureMessage)
        {
            if (__result || string.IsNullOrWhiteSpace(failureMessage))
            {
                return;
            }

            var giftOrTrap = ModEntry.Translation.Get(isTrap ? "gift.kind.trap" : "gift.kind.gift").ToString();
            if (failureMessage.StartsWith("You must hold an item in your hand to ", StringComparison.Ordinal))
            {
                failureMessage = ModEntry.Translation.Get("gift.must_hold_item", new { kind = giftOrTrap }).ToString();
                return;
            }

            const string suffix = " cannot be sent to other players";
            if (failureMessage.EndsWith(suffix, StringComparison.Ordinal))
            {
                var item = failureMessage[..^suffix.Length];
                var localizedItem = GetBestLocalizedGiftItemName(item);
                failureMessage = ModEntry.Translation.Get("gift.item_not_sendable", new { item = localizedItem }).ToString();
            }
        }

        private static string TranslateGiftServiceMessage(string message)
        {
            if (string.IsNullOrWhiteSpace(message))
            {
                return message;
            }

            const string suffix = " cannot be sent to other players";
            if (message.EndsWith(suffix, StringComparison.Ordinal))
            {
                var item = message[..^suffix.Length];
                var localizedItem = GetBestLocalizedGiftItemName(item);
                return ModEntry.Translation.Get("gift.item_not_sendable", new { item = localizedItem }).ToString();
            }

            const string giftBoxSuffix = " does not exist in this session or has not set up a gift box";
            if (message.EndsWith(giftBoxSuffix, StringComparison.Ordinal))
            {
                var player = message[..^giftBoxSuffix.Length];
                return ModEntry.Translation.Get("gift.player_no_giftbox", new { player }).ToString();
            }

            if (message.Equals("Player does not exist or does not have a giftbox", StringComparison.Ordinal))
            {
                return ModEntry.Translation.Get("gift.player_no_giftbox_generic").ToString();
            }

            if (message.StartsWith("Unknown Error occurred while sending ", StringComparison.Ordinal)
                && message.EndsWith(".", StringComparison.Ordinal))
            {
                var rawKind = message["Unknown Error occurred while sending ".Length..^1];
                var localizedKind = rawKind switch
                {
                    "gift" => ModEntry.Translation.Get("gift.kind.gift").ToString(),
                    "trap" => ModEntry.Translation.Get("gift.kind.trap").ToString(),
                    _ => _pendingGiftKind ?? rawKind,
                };
                return ModEntry.Translation.Get("gift.send_unknown_error", new { kind = localizedKind }).ToString();
            }

            if (message.Equals("Could not complete gifting operation. Check SMAPI for error details.", StringComparison.Ordinal))
            {
                return ModEntry.Translation.Get("gift.operation_failed").ToString();
            }

            return message;
        }

        private static bool InvokeBool(object target, string methodName, string argument)
        {
            var method = AccessTools.Method(target.GetType(), methodName);
            return method?.Invoke(target, new object[] { argument }) is bool result && result;
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

        private static string GetBestLocalizedGiftItemName(string fallbackEnglishName)
        {
            var activeObject = Game1.player?.ActiveObject;
            var activeDisplayName = activeObject?.DisplayName;
            if (!string.IsNullOrWhiteSpace(activeDisplayName))
            {
                return activeDisplayName;
            }

            var localizedItem = TranslationHelper.GetLocalizedItemName(fallbackEnglishName);
            return string.IsNullOrWhiteSpace(localizedItem) ? fallbackEnglishName : localizedItem;
        }

        private static T GetPropertyValue<T>(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            return property?.GetValue(target) is T value ? value : default!;
        }

        private static object? GetPropertyValue(object target, string propertyName)
        {
            var property = target.GetType().GetProperty(propertyName);
            return property?.GetValue(target);
        }

        private static object? GetFieldValue(object target, string fieldName)
        {
            var field = AccessTools.Field(target.GetType(), fieldName);
            return field?.GetValue(target);
        }
    }
}
