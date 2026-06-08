using System;
using System.Collections.Generic;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class KaitoStardropDialoguePatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.CodeInjections.FarmerInjections"
                );
                if (targetType == null)
                {
                    return;
                }

                var method = AccessTools.Method(targetType, "DoneEatingFavoriteThingKaito");
                if (method == null)
                {
                    return;
                }

                harmony.Patch(
                    original: method,
                    transpiler: new HarmonyMethod(
                        typeof(KaitoStardropDialoguePatcher),
                        nameof(DoneEatingFavoriteThingKaito_Transpiler)
                    )
                );

                ModEntry.Instance.Monitor.Log(
                    "Successfully patched FarmerInjections.DoneEatingFavoriteThingKaito dialogue text!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch Kaito Stardrop dialogue text: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static IEnumerable<CodeInstruction> DoneEatingFavoriteThingKaito_Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var originalMethod = AccessTools.Method(
                typeof(DelayedAction),
                nameof(DelayedAction.showDialogueAfterDelay),
                new[] { typeof(string), typeof(int) }
            );
            var replacementMethod = AccessTools.Method(
                typeof(KaitoStardropDialoguePatcher),
                nameof(ShowLocalizedDialogueAfterDelay)
            );
            if (originalMethod == null || replacementMethod == null)
            {
                foreach (var instruction in instructions)
                {
                    yield return instruction;
                }

                yield break;
            }

            foreach (var instruction in instructions)
            {
                if (instruction.Calls(originalMethod))
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

        public static void ShowLocalizedDialogueAfterDelay(string text, int delay)
        {
            DelayedAction.showDialogueAfterDelay(StardropFavoriteThingTemplate.Localize(text), delay);
        }
    }
}
