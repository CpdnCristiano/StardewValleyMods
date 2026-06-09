using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class KaitoStardropDialoguePatcher
    {
        private const string FavoriteThingPlaceholder = "{{favoriteThing}}";
        private const string SourceThoughtsTemplate =
            "Your mind is filled with thoughts of... {{favoriteThing}}? ^";
        private const string SourceMaximumBundles = "Even with these bundles?^";
        private const string SourceNightmareTraps = "Even with these traps??^";
        private const string SourceChaosEr = "Even on Chaos ER?!? You scare me.^";
        private const string SourceTryHarder =
            "Try harder settings, you'll change your mind.^";

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
            var codes = new List<CodeInstruction>(instructions);
            ReplaceTextTemplate(
                codes,
                SourceThoughtsTemplate,
                nameof(GetThoughtsPrefixText),
                nameof(GetThoughtsSuffixText)
            );
            ReplaceTextConstant(
                codes,
                SourceMaximumBundles,
                typeof(KaitoStardropDialoguePatcher),
                nameof(GetMaximumBundlesText)
            );
            ReplaceTextConstant(
                codes,
                SourceNightmareTraps,
                typeof(KaitoStardropDialoguePatcher),
                nameof(GetNightmareTrapsText)
            );
            ReplaceTextConstant(
                codes,
                SourceChaosEr,
                typeof(KaitoStardropDialoguePatcher),
                nameof(GetChaosErText)
            );
            ReplaceTextConstant(
                codes,
                SourceTryHarder,
                typeof(KaitoStardropDialoguePatcher),
                nameof(GetTryHarderText)
            );

            return codes;
        }

        private static void ReplaceTextConstant(
            List<CodeInstruction> codes,
            string sourceText,
            Type patcherType,
            string replacementMethodName
        )
        {
            var method = AccessTools.Method(patcherType, replacementMethodName);
            if (method == null)
            {
                return;
            }

            for (var index = 0; index < codes.Count; index++)
            {
                if (codes[index].opcode != OpCodes.Ldstr || !Equals(codes[index].operand, sourceText))
                {
                    continue;
                }

                codes[index] = new CodeInstruction(OpCodes.Call, method)
                {
                    labels = codes[index].labels,
                    blocks = codes[index].blocks,
                };
            }
        }

        private static void ReplaceTextTemplate(
            List<CodeInstruction> codes,
            string sourceTemplate,
            string prefixReplacementMethodName,
            string suffixReplacementMethodName
        )
        {
            ReplaceTextConstant(
                codes,
                GetTemplatePart(sourceTemplate, beforeFavoriteThing: true),
                typeof(KaitoStardropDialoguePatcher),
                prefixReplacementMethodName
            );
            ReplaceTextConstant(
                codes,
                GetTemplatePart(sourceTemplate, beforeFavoriteThing: false),
                typeof(KaitoStardropDialoguePatcher),
                suffixReplacementMethodName
            );
        }

        public static string GetThoughtsPrefixText()
        {
            return GetThoughtsTemplatePart(beforeFavoriteThing: true);
        }

        public static string GetThoughtsSuffixText()
        {
            return GetThoughtsTemplatePart(beforeFavoriteThing: false);
        }

        private static string GetThoughtsTemplatePart(bool beforeFavoriteThing)
        {
            var template = ModEntry
                .Translation.Get("kaito_stardrop.favorite_thing.thoughts")
                .ToString();
            return GetTemplatePart(template, beforeFavoriteThing);
        }

        private static string GetTemplatePart(string template, bool beforeFavoriteThing)
        {
            var placeholderIndex = template.IndexOf(FavoriteThingPlaceholder, StringComparison.Ordinal);
            if (placeholderIndex < 0)
            {
                return beforeFavoriteThing ? template : string.Empty;
            }

            return beforeFavoriteThing
                ? template[..placeholderIndex]
                : template[(placeholderIndex + FavoriteThingPlaceholder.Length)..];
        }

        public static string GetMaximumBundlesText()
        {
            return ModEntry
                .Translation.Get("kaito_stardrop.favorite_thing.maximum_bundles")
                .ToString();
        }

        public static string GetNightmareTrapsText()
        {
            return ModEntry
                .Translation.Get("kaito_stardrop.favorite_thing.nightmare_traps")
                .ToString();
        }

        public static string GetChaosErText()
        {
            return ModEntry
                .Translation.Get("kaito_stardrop.favorite_thing.chaos_er")
                .ToString();
        }

        public static string GetTryHarderText()
        {
            return ModEntry
                .Translation.Get("kaito_stardrop.favorite_thing.try_harder")
                .ToString();
        }
    }
}
