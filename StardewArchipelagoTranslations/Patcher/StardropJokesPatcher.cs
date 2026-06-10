using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using HarmonyLib;
using StardewArchipelago.Constants.Vanilla;
using StardewModdingAPI;
using StardewValley;
using StardewObject = StardewValley.Object;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class StardropJokesPatcher
    {
        private const int StardropDialogueDelay = 6000;

        public static void Patch(Harmony harmony)
        {
            try
            {
                var eatInjectionsType = AccessTools.TypeByName(
                    "StardewArchipelago.Locations.CodeInjections.Vanilla.EatInjections"
                );
                var doneEatingPatchesPrefixMethod = eatInjectionsType != null
                    ? AccessTools.Method(eatInjectionsType, "DoneEating_EatingPatches_Prefix")
                    : null;

                if (doneEatingPatchesPrefixMethod == null)
                {
                    return;
                }

                harmony.Patch(
                    original: doneEatingPatchesPrefixMethod,
                    transpiler: new HarmonyMethod(
                        typeof(StardropJokesPatcher),
                        nameof(DoneEatingPatchesPrefix_Transpiler)
                    )
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

        public static IEnumerable<CodeInstruction> DoneEatingPatchesPrefix_Transpiler(
            IEnumerable<CodeInstruction> instructions
        )
        {
            var farmerInjectionsType = AccessTools.TypeByName(
                "StardewArchipelago.GameModifications.CodeInjections.FarmerInjections"
            );
            var originalMethod = farmerInjectionsType != null
                ? AccessTools.Method(farmerInjectionsType, "DoneEatingFavoriteThingKaito")
                : null;
            var replacementMethod = AccessTools.Method(
                typeof(StardropJokesPatcher),
                nameof(DoneEatingFavoriteThingCustomJoke)
            );

            foreach (var instruction in instructions)
            {
                if (
                    originalMethod != null
                    && replacementMethod != null
                    && instruction.Calls(originalMethod)
                )
                {
                    yield return instruction;
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, replacementMethod);
                    continue;
                }

                yield return instruction;
            }
        }

        public static void DoneEatingFavoriteThingCustomJoke(Farmer __instance)
        {
            try
            {
                var itemToEat = __instance.itemToEat as StardewObject;
                if (itemToEat?.QualifiedItemId != QualifiedItemIds.STARDROP)
                {
                    return;
                }

                if (!StardropJokeTemplates.TryGetJoke(__instance.favoriteThing.Value, out var joke))
                {
                    return;
                }

                if (Game1.delayedActions.Any())
                {
                    Game1.delayedActions.Clear();
                }

                DelayedAction.showDialogueAfterDelay(
                    Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3100")
                        + joke
                        + Game1.content.LoadString("Strings\\StringsFromCSFiles:Game1.cs.3101").Substring(3),
                    StardropDialogueDelay
                );
                DelayedAction.stopFarmerGlowing(StardropDialogueDelay);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed in {nameof(DoneEatingFavoriteThingCustomJoke)}:{Environment.NewLine}{ex}",
                    LogLevel.Error
                );
            }
        }
    }
}
