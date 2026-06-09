using System;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class StardropJokesPatcher
    {
        private const string StardropQualifiedItemId = "(O)434";
        private static string? _pendingJoke;

        public static void Patch(Harmony harmony)
        {
            try
            {
                var doneEatingMethod = AccessTools.Method(typeof(Farmer), nameof(Farmer.doneEating));
                if (doneEatingMethod != null)
                {
                    harmony.Patch(
                        original: doneEatingMethod,
                        prefix: new HarmonyMethod(
                            typeof(StardropJokesPatcher),
                            nameof(DoneEating_Prefix)
                        )
                    );
                }

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
                if (_pendingJoke != null && IsStardropDialogue(__0))
                {
                    __0 = _pendingJoke;
                    _pendingJoke = null;
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

        public static void DoneEating_Prefix(Farmer __instance)
        {
            try
            {
                _pendingJoke = null;

                if (__instance.itemToEat is not StardewObject itemToEat)
                {
                    return;
                }

                if (itemToEat.QualifiedItemId != StardropQualifiedItemId)
                {
                    return;
                }

                if (StardropJokeTemplates.TryGetJoke(__instance.favoriteThing.Value, out var joke))
                {
                    _pendingJoke = joke;
                }
            }
            catch (Exception ex)
            {
                _pendingJoke = null;
                ModEntry.Instance.Monitor.Log(
                    $"Error preparing custom Stardrop joke: {ex.Message}",
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
