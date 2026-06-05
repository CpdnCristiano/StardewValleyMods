using System;
using System.Linq;
using HarmonyLib;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class TelevisionPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var stardewArchipelagoAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (stardewArchipelagoAssembly != null)
                {
                    var lotlInjectionsType = stardewArchipelagoAssembly.GetType(
                        "StardewArchipelago.GameModifications.CodeInjections.Television.LivinOffTheLandInjections"
                    );
                    if (lotlInjectionsType != null)
                    {
                        var initializeMethod = lotlInjectionsType.GetMethod("Initialize");
                        if (initializeMethod != null)
                        {
                            var postfixPatch = new HarmonyMethod(
                                typeof(TelevisionPatcher),
                                nameof(Initialize_Postfix)
                            );
                            harmony.Patch(initializeMethod, postfix: postfixPatch);
                            ModEntry.Instance.Monitor.Log(
                                "Successfully patched LivinOffTheLandInjections!",
                                StardewModdingAPI.LogLevel.Info
                            );
                        }
                    }
                }

                // Patch Game1.drawObjectDialogue to translate Gateway Gazette
                var drawObjectDialogueMethod = AccessTools.Method(
                    typeof(StardewValley.Game1),
                    nameof(StardewValley.Game1.drawObjectDialogue),
                    new[] { typeof(string) }
                );
                if (drawObjectDialogueMethod != null)
                {
                    harmony.Patch(
                        drawObjectDialogueMethod,
                        prefix: new HarmonyMethod(
                            typeof(TelevisionPatcher),
                            nameof(DrawObjectDialogue_Prefix)
                        )
                    );
                    ModEntry.Instance.Monitor.Log(
                        "Successfully patched Game1.drawObjectDialogue for Gateway Gazette translations!",
                        StardewModdingAPI.LogLevel.Info
                    );
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch Television (LOTL/Gazette): {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        public static void Initialize_Postfix(object logger, object archipelago)
        {
            try
            {
                var stardewArchipelagoAssembly = AppDomain
                    .CurrentDomain.GetAssemblies()
                    .FirstOrDefault(a => a.GetName().Name == "StardewArchipelago");
                if (stardewArchipelagoAssembly != null)
                {
                    var lotlInjectionsType = stardewArchipelagoAssembly.GetType(
                        "StardewArchipelago.GameModifications.CodeInjections.Television.LivinOffTheLandInjections"
                    );
                    if (lotlInjectionsType != null)
                    {
                        var orderedTipsField = lotlInjectionsType.GetField(
                            "_orderedTips",
                            System.Reflection.BindingFlags.NonPublic
                                | System.Reflection.BindingFlags.Static
                        );
                        if (orderedTipsField != null)
                        {
                            var orderedTips =
                                orderedTipsField.GetValue(null)
                                as System.Collections.Generic.List<string>;
                            if (orderedTips != null)
                            {
                                for (int i = 0; i < orderedTips.Count; i++)
                                {
                                    var originalTip = orderedTips[i];
                                    var localized = TranslationHelper.GetLocalizedTVTip(
                                        originalTip
                                    );
                                    if (localized != originalTip)
                                    {
                                        orderedTips[i] = localized;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in LivinOffTheLandInjections.Initialize_Postfix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        public static bool DrawObjectDialogue_Prefix(ref string dialogue)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(dialogue))
                    return true;

                // Normalize white space/newlines
                var normalized = dialogue
                    .Replace("\r", "")
                    .Replace("\n", " ")
                    .Replace("  ", " ")
                    .Trim();

                // 1. Gazette Intro
                if (
                    normalized.Equals(
                        "Welcome back to the Gateway Gazette! The bi-weekly show where brave adventurers explore the strange topology of the world around us!",
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    var localizedIntro = ModEntry.Translation.Get("tv.gazette.intro").ToString();
                    dialogue = StardewValley.Game1.parseText(localizedIntro);
                    return true;
                }

                // 2. Gazette Episode
                var match = System.Text.RegularExpressions.Regex.Match(
                    normalized,
                    @"^On today's episode, our agent (.*) has traversed from (.*) and discovered\.\.\. (.*)! They came back safe and sound to share this wonderful knowledge with us!$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (match.Success)
                {
                    var agent = match.Groups[1].Value;
                    var fromMap = match.Groups[2].Value;
                    var toMap = match.Groups[3].Value;

                    var localizedFrom = TranslationHelper.GetLocalizedLocationName(fromMap);
                    var localizedTo = TranslationHelper.GetLocalizedLocationName(toMap);

                    var localizedEpisode = ModEntry
                        .Translation.Get(
                            "tv.gazette.episode",
                            new
                            {
                                agent = agent,
                                from = localizedFrom,
                                to = localizedTo,
                            }
                        )
                        .ToString();

                    dialogue = StardewValley.Game1.parseText(localizedEpisode);
                    return true;
                }

                // 3. Gazette Chaos Episode
                var chaosMatch = System.Text.RegularExpressions.Regex.Match(
                    normalized,
                    @"^On today's episode, our agent (.*) was sent to explore\.\.\. but we haven't heard back from them\. Let's send them thoughts and prayers! Don't walk outside unprepared, kids!$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
                if (chaosMatch.Success)
                {
                    var agent = chaosMatch.Groups[1].Value;
                    var localizedChaos = ModEntry
                        .Translation.Get("tv.gazette.chaos_episode", new { agent = agent })
                        .ToString();

                    dialogue = StardewValley.Game1.parseText(localizedChaos);
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in DrawObjectDialogue_Prefix: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
            return true;
        }
    }
}
