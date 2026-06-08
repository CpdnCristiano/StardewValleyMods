using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using Archipelago.MultiClient.Net.MessageLog.Parts;
using Archipelago.MultiClient.Net.Models;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewArchipelago.Archipelago;
using StardewArchipelago.Archipelago.SlotData.SlotEnums;
using StardewArchipelago.Extensions;
using StardewModdingAPI;
using StardewValley;
using Color = Microsoft.Xna.Framework.Color;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    [HarmonyPatch(typeof(StardewArchipelagoClient), "OnMessageReceived")]
    public static class ChatPatcher
    {
        [HarmonyPrefix]
        public static bool Prefix(StardewArchipelagoClient __instance, LogMessage message)
        {
            try
            {
                var parts = new List<string>();
                foreach (var part in message.Parts)
                {
                    var text = part.Text;
                    if (part is ItemMessagePart)
                    {
                        text = TranslationHelper.GetLocalizedItemName(text);
                    }
                    else if (part is LocationMessagePart)
                    {
                        text = TranslationHelper.GetLocalizedLocationName(text);
                    }
                    else if (part is PlayerMessagePart)
                    {
                        text = TranslationHelper.GetLocalizedPlayerName(text);
                    }
                    parts.Add(text);
                }
                var fullMessage = string.Join(" ", parts);
                var stardewFullMessage = fullMessage.TurnHeartsIntoStardewHearts();

                var messagesToIgnoreField = typeof(StardewArchipelagoClient).GetField(
                    "_messagesToIgnore",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                var messagesToIgnore = (List<string>?)messagesToIgnoreField?.GetValue(__instance);

                if (
                    messagesToIgnore != null
                    && (
                        messagesToIgnore.Any(x =>
                            x.Contains(fullMessage) || fullMessage.Contains(x)
                        )
                        || messagesToIgnore.Any(x =>
                            x.Contains(stardewFullMessage) || fullMessage.Contains(x)
                        )
                    )
                )
                {
                    var loggerField = typeof(StardewArchipelagoClient).GetField(
                        "Logger",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    );
                    if (loggerField == null)
                    {
                        loggerField = typeof(StardewArchipelagoClient).BaseType?.GetField(
                            "Logger",
                            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                        );
                    }
                    var logger = (KaitoKid.Utilities.Interfaces.ILogger?)
                        loggerField?.GetValue(__instance);
                    logger?.LogDebug($"Ignoring Chat Message: {fullMessage}");
                    return false;
                }

                var activeLoggerField = typeof(StardewArchipelagoClient).GetField(
                    "Logger",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (activeLoggerField == null)
                {
                    activeLoggerField = typeof(StardewArchipelagoClient).BaseType?.GetField(
                        "Logger",
                        BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                    );
                }
                var activeLogger = (KaitoKid.Utilities.Interfaces.ILogger?)
                    activeLoggerField?.GetValue(__instance);
                activeLogger?.LogInfo(fullMessage);

                stardewFullMessage = stardewFullMessage.AnonymizePlayerNames(
                    __instance.GetSession().Players
                );
                if (
                    stardewFullMessage
                        .Trim()
                        .StartsWith("[Hint]:", StringComparison.OrdinalIgnoreCase)
                )
                {
                    stardewFullMessage = TranslationHelper.TranslateHintMessage(
                        stardewFullMessage.Trim()
                    );
                }

                // Protocol-generated logs may contain connector words in English (sent/to/found/their).
                // Apply connector localization to non-player chat only.
                if (message is not ChatLogMessage)
                {
                    stardewFullMessage = LocalizePlainCommandItems(stardewFullMessage);
                    stardewFullMessage = LocalizeProtocolConnectors(stardewFullMessage);
                }

                switch (message)
                {
                    case ChatLogMessage chatMessage:
                    {
                        if (!StardewArchipelago.ModEntry.Instance.Config.EnableChatMessages)
                        {
                            return false;
                        }

                        var color = chatMessage.Player.Name.GetAsBrightColor();
                        Game1.chatBox?.addMessage(stardewFullMessage, color);
                        return false;
                    }
                    case ItemSendLogMessage itemSendLogMessage:
                    {
                        if (
                            StardewArchipelago.ModEntry.Instance.Config.DisplayItemsInChat
                            == ChatItemsFilter.None
                        )
                        {
                            return false;
                        }

                        if (
                            StardewArchipelago.ModEntry.Instance.Config.DisplayItemsInChat
                                == ChatItemsFilter.RelatedToMe
                            && !itemSendLogMessage.IsRelatedToActivePlayer
                        )
                        {
                            return false;
                        }

                        var color = Color.Gold;
                        Game1.chatBox?.addMessage(stardewFullMessage, color);
                        return false;
                    }
                    case GoalLogMessage:
                    {
                        var color = Color.Green;
                        Game1.chatBox?.addMessage(stardewFullMessage, color);
                        return false;
                    }
                    case JoinLogMessage:
                    case LeaveLogMessage:
                    case TagsChangedLogMessage:
                    {
                        if (!StardewArchipelago.ModEntry.Instance.Config.EnableConnectionMessages)
                        {
                            return false;
                        }

                        var color = Color.Gray;
                        Game1.chatBox?.addMessage(stardewFullMessage, color);
                        return false;
                    }
                    case CommandResultLogMessage:
                    case not null:
                    {
                        var color = Color.Gray;
                        Game1.chatBox?.addMessage(stardewFullMessage, color);
                        return false;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in OnMessageReceived Prefix, falling back to original: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }

        private static string LocalizeProtocolConnectors(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            // Use word boundaries so only standalone tokens are replaced (e.g. "to" won't affect "Tomato").
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\bsending\b",
                ModEntry.Translation.Get("chat.sending").ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\bsent\b",
                ModEntry.Translation.Get("chat.sent").ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\bto\b",
                ModEntry.Translation.Get("chat.to").ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\bfound\b",
                ModEntry.Translation.Get("chat.found").ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );
            text = System.Text.RegularExpressions.Regex.Replace(
                text,
                @"\btheir\b",
                ModEntry.Translation.Get("chat.their").ToString(),
                System.Text.RegularExpressions.RegexOptions.IgnoreCase
            );

            return text;
        }

        private static string LocalizePlainCommandItems(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            return Regex.Replace(
                text,
                @"\bsending\s+""([^""]+)""",
                match =>
                {
                    var localizedItem = TranslationHelper.GetLocalizedItemName(
                        match.Groups[1].Value
                    );
                    var itemStartOffset = match.Groups[1].Index - match.Index;
                    return $"{match.Value.Substring(0, itemStartOffset)}{localizedItem}\"";
                },
                RegexOptions.IgnoreCase
            );
        }
    }
}
