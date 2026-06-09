using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class StardropJokesPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var method = AccessTools.Method(
                    typeof(DelayedAction),
                    nameof(DelayedAction.showDialogueAfterDelay),
                    new[] { typeof(string), typeof(int) }
                );
                if (method == null)
                {
                    return;
                }

                harmony.Patch(
                    original: method,
                    prefix: new HarmonyMethod(
                        typeof(StardropJokesPatcher),
                        nameof(ShowDialogueAfterDelay_Prefix)
                    )
                );

                ModEntry.Instance.Monitor.Log(
                    "Successfully patched DelayedAction.showDialogueAfterDelay for custom Stardrop jokes!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch custom Stardrop jokes: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void ShowDialogueAfterDelay_Prefix(ref string __0)
        {
            try
            {
                if (
                    IsStardropDialogue(__0)
                    && StardropJokeTemplates.TryGetJoke(
                        Game1.player?.favoriteThing.Value,
                        out var joke
                    )
                )
                {
                    __0 = joke;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error applying custom Stardrop joke: {ex.Message}",
                    LogLevel.Trace
                );
            }
        }

        private static bool IsStardropDialogue(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            var prefix = Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3100");
            var suffix = Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3101");
            var kaitoSuffix = suffix.Length > 3 ? suffix[3..] : suffix;

            return text.StartsWith(prefix, StringComparison.Ordinal)
                && (
                    text.Contains(suffix, StringComparison.Ordinal)
                    || text.Contains(kaitoSuffix, StringComparison.Ordinal)
                );
        }
    }
}
