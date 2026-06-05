using System;
using System.Linq;
using HarmonyLib;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class GoalsPatcher
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
                    var goalsType = stardewArchipelagoAssembly.GetType(
                        "StardewArchipelago.Goals.GoalCodeInjection"
                    );
                    if (goalsType != null)
                    {
                        var getGoalStringMethod = goalsType.GetMethod("GetGoalString");
                        var getGoalStringGrandpaMethod = goalsType.GetMethod(
                            "GetGoalStringGrandpa"
                        );

                        var postfixPatch = new HarmonyMethod(
                            typeof(GoalsPatcher),
                            nameof(GetGoalString_Postfix)
                        );

                        if (getGoalStringMethod != null)
                        {
                            harmony.Patch(getGoalStringMethod, postfix: postfixPatch);
                        }
                        if (getGoalStringGrandpaMethod != null)
                        {
                            harmony.Patch(getGoalStringGrandpaMethod, postfix: postfixPatch);
                        }

                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched GoalCodeInjection methods!",
                            StardewModdingAPI.LogLevel.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch GoalCodeInjection: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        public static void GetGoalString_Postfix(ref string __result)
        {
            if (string.IsNullOrWhiteSpace(__result))
                return;

            var localized = TranslationHelper.GetLocalizedGoalString(__result);
            if (localized != __result)
            {
                __result = localized;
            }
        }
    }
}
