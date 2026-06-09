using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class StardropJokesPatcher
    {
        public static void Patch(Harmony harmony)
        {
            PatchMethod(
                harmony,
                "StardewArchipelago.Locations.CodeInjections.Vanilla.EatInjections",
                "DoneEatingStardrop"
            );
            PatchMethod(
                harmony,
                "StardewArchipelago.GameModifications.CodeInjections.FarmerInjections",
                "DoneEatingFavoriteThingKaito"
            );
        }

        private static void PatchMethod(Harmony harmony, string typeName, string methodName)
        {
            try
            {
                var targetType = AccessTools.TypeByName(typeName);
                if (targetType == null)
                {
                    return;
                }

                var method = AccessTools.Method(targetType, methodName);
                if (method == null)
                {
                    return;
                }

                harmony.Patch(
                    original: method,
                    transpiler: new HarmonyMethod(
                        typeof(StardropJokesPatcher),
                        nameof(ShowDialogueAfterDelay_Transpiler)
                    )
                );

                ModEntry.Instance.Monitor.Log(
                    $"Successfully patched {typeName}.{methodName} for custom Stardrop jokes!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch {typeName}.{methodName} for custom Stardrop jokes: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static IEnumerable<CodeInstruction> ShowDialogueAfterDelay_Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var originalMethod = AccessTools.Method(
                typeof(DelayedAction),
                nameof(DelayedAction.showDialogueAfterDelay),
                new[] { typeof(string), typeof(int) }
            );
            var replacementMethod = AccessTools.Method(
                typeof(StardropJokesPatcher),
                nameof(ShowJokeOrOriginalDialogueAfterDelay)
            );

            foreach (var instruction in instructions)
            {
                if (
                    originalMethod != null
                    && replacementMethod != null
                    && instruction.Calls(originalMethod)
                )
                {
                    yield return new CodeInstruction(instruction)
                    {
                        operand = replacementMethod,
                    };
                    continue;
                }

                yield return instruction;
            }
        }

        public static void ShowJokeOrOriginalDialogueAfterDelay(string text, int delay)
        {
            if (StardropJokeTemplates.TryGetJoke(Game1.player?.favoriteThing.Value, out var joke))
            {
                DelayedAction.showDialogueAfterDelay(joke, delay);
                return;
            }

            DelayedAction.showDialogueAfterDelay(text, delay);
        }
    }
}
