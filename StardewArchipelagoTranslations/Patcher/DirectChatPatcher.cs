using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using Microsoft.Xna.Framework;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    /// <summary>
    /// Intercepts direct Game1.chatBox.addMessage() calls from the core StardewArchipelago mod
    /// and translates hardcoded English strings through i18n rules.
    /// </summary>
    [HarmonyPatch(typeof(ChatBox), nameof(ChatBox.addMessage))]
    public static class DirectChatPatcher
    {
        // Each entry: (Regex pattern, i18n key, named group -> token mapping)
        // Patterns are matched case-insensitively against the raw English message.
        private static readonly List<(Regex Pattern, Func<Match, string> Translate)> _rules = new()
        {
            // "Connected to Archipelago as {slot}. Type !!help for client commands"
            (
                new Regex(
                    @"^Connected to Archipelago as (.+?)\.\s*Type !!help for client commands$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                m =>
                    ModEntry
                        .Translation.Get("chat.connected", new { slot = m.Groups[1].Value })
                        .ToString()
            ),
            // "Connected to Archipelago server as Cristiano (Team 0)."
            (
                new Regex(
                    @"^Connected to Archipelago server as (.+?)\.$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                m =>
                    ModEntry
                        .Translation.Get("chat.connected_server", new { slot = m.Groups[1].Value })
                        .ToString()
            ),
            // "Now that you are connected, you can use !help to list commands to run via the server. If your client supports it, you may have additional local commands you can list with /help."
            (
                new Regex(
                    @"^Now that you are connected,",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                _ => ModEntry.Translation.Get("chat.help_server").ToString()
            ),
            // "A Fatal error has occurred while initializing Archipelago. Check SMAPI for details to report the problem"
            (
                new Regex(
                    @"^A Fatal error has occurred while initializing Archipelago\.",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                _ => ModEntry.Translation.Get("chat.fatal_error").ToString()
            ),
            (
                new Regex(
                    @"^Reconnection attempt failed$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                _ => ModEntry.Translation.Get("client.reconnect_failed").ToString()
            ),
            (
                new Regex(
                    @"^Reconnection attempt successful!$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                _ => ModEntry.Translation.Get("client.reconnect_success").ToString()
            ),
            (
                new Regex(
                    @"^Trap Bundle sent (.+?) to (.+?) \((.+?)\)$",
                    RegexOptions.IgnoreCase | RegexOptions.Compiled
                ),
                m => ModEntry.Translation.Get(
                    "client.trap_bundle_sent",
                    new
                    {
                        itemName = TranslationHelper.GetLocalizedItemName(m.Groups[1].Value),
                        player = m.Groups[2].Value,
                        locationName = m.Groups[3].Value,
                    }
                ).ToString()
            ),
        };

        [HarmonyPrefix]
        public static bool Prefix(ref string message, Color color)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(message))
                    return true;

                foreach (var (pattern, translate) in _rules)
                {
                    var match = pattern.Match(message);
                    if (match.Success)
                    {
                        var translated = translate(match);
                        if (!string.IsNullOrWhiteSpace(translated))
                        {
                            message = translated;
                        }
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in DirectChatPatcher: {ex.Message}",
                    StardewModdingAPI.LogLevel.Trace
                );
            }
            return true; // Always run original — we only mutate 'message' in place
        }
    }
}
