using System;
using System.Reflection;
using HarmonyLib;
using StardewArchipelago.Locations.CodeInjections.Vanilla.Bundles;
using StardewArchipelago.Locations.CodeInjections.Vanilla.Bundles.Remakes;
using StardewModdingAPI;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class BundlesPatcher
    {
        public static void Patch(Harmony harmony)
        {
            // Patch ArchipelagoJunimoNoteMenu constructors dynamically
            try
            {
                var menuType = typeof(ArchipelagoJunimoNoteMenu);
                var constructors = menuType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public
                );
                var postfix = new HarmonyMethod(
                    typeof(JunimoNoteMenuRemake_Constructor_Patch),
                    nameof(JunimoNoteMenuRemake_Constructor_Patch.Postfix)
                );
                foreach (var ctor in constructors)
                {
                    harmony.Patch(ctor, postfix: postfix);
                }
                ModEntry.Instance.Monitor.Log(
                    "Successfully patched ArchipelagoJunimoNoteMenu constructors!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch ArchipelagoJunimoNoteMenu constructors: {ex}",
                    LogLevel.Error
                );
            }

            // Patch JunimoNoteMenuRemake constructors dynamically
            try
            {
                var remakeMenuType = typeof(JunimoNoteMenuRemake);
                var constructors = remakeMenuType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public
                );
                var postfix = new HarmonyMethod(
                    typeof(JunimoNoteMenuRemake_Constructor_Patch),
                    nameof(JunimoNoteMenuRemake_Constructor_Patch.Postfix)
                );
                foreach (var ctor in constructors)
                {
                    harmony.Patch(ctor, postfix: postfix);
                }
                ModEntry.Instance.Monitor.Log(
                    "Successfully patched JunimoNoteMenuRemake constructors!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch JunimoNoteMenuRemake constructors: {ex}",
                    LogLevel.Error
                );
            }

            // Patch ArchipelagoBundle constructors dynamically
            try
            {
                var bundleType = typeof(ArchipelagoBundle);
                var constructors = bundleType.GetConstructors(
                    BindingFlags.Instance | BindingFlags.Public
                );
                var postfix = new HarmonyMethod(
                    typeof(ArchipelagoBundle_Constructor_Patch),
                    nameof(ArchipelagoBundle_Constructor_Patch.Postfix)
                );
                foreach (var ctor in constructors)
                {
                    harmony.Patch(ctor, postfix: postfix);
                }
                ModEntry.Instance.Monitor.Log(
                    "Successfully patched ArchipelagoBundle constructors!",
                    LogLevel.Info
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch ArchipelagoBundle constructors: {ex}",
                    LogLevel.Error
                );
            }
        }
    }

    public static class JunimoNoteMenu_Constructor_Patch
    {
        public static void Postfix(object __instance)
        {
            try
            {
                if (__instance is JunimoNoteMenu menu)
                {
                    if (menu.bundles != null)
                    {
                        foreach (var bundle in menu.bundles)
                        {
                            var localizedLabel = TranslationHelper.GetLocalizedBundleName(
                                bundle.name
                            );
                            if (localizedLabel != bundle.name)
                            {
                                bundle.label = localizedLabel;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in JunimoNoteMenu Constructor Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }

    public static class JunimoNoteMenuRemake_Constructor_Patch
    {
        public static void Postfix(object __instance)
        {
            try
            {
                if (__instance is JunimoNoteMenuRemake menu)
                {
                    if (menu.Bundles != null)
                    {
                        foreach (var bundle in menu.Bundles)
                        {
                            var localizedLabel = TranslationHelper.GetLocalizedBundleName(
                                bundle.name
                            );
                            if (localizedLabel != bundle.name)
                            {
                                bundle.label = localizedLabel;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in JunimoNoteMenuRemake Constructor Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }

    public static class ArchipelagoBundle_Constructor_Patch
    {
        public static void Postfix(object __instance)
        {
            try
            {
                if (__instance is BundleRemake bundleRemake)
                {
                    var localizedLabel = TranslationHelper.GetLocalizedBundleName(
                        bundleRemake.name
                    );
                    if (localizedLabel != bundleRemake.name)
                    {
                        bundleRemake.label = localizedLabel;
                    }
                }
                else if (__instance is StardewValley.Menus.Bundle vanillaBundle)
                {
                    var localizedLabel = TranslationHelper.GetLocalizedBundleName(
                        vanillaBundle.name
                    );
                    if (localizedLabel != vanillaBundle.name)
                    {
                        vanillaBundle.label = localizedLabel;
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in ArchipelagoBundle Constructor Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }

    [HarmonyPatch(typeof(ArchipelagoJunimoNoteMenu), "GetRewardNameForArea")]
    public static class JunimoNoteMenu_GetRewardNameForArea_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref string __result)
        {
            TranslationHelper.TranslateRewardName(ref __result);
        }
    }

    [HarmonyPatch(typeof(ArchipelagoJunimoNoteMenu), "TryGetSpecialRewardName")]
    public static class JunimoNoteMenu_TryGetSpecialRewardName_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref string specialRewardName, ref bool __result)
        {
            if (__result)
            {
                TranslationHelper.TranslateRewardName(ref specialRewardName);
            }
        }
    }
}
