using System;
using System.Linq;
using System.Reflection;
using HarmonyLib;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class I18nPatcher
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
                    var i18nType = stardewArchipelagoAssembly.GetType("StardewArchipelago.I18n");
                    if (i18nType != null)
                    {
                        var getMethod1 = i18nType.GetMethod(
                            "Get",
                            new Type[] { typeof(string), typeof(object) }
                        );
                        var getMethod2 = i18nType.GetMethod(
                            "Get",
                            new Type[] { typeof(string), typeof(string), typeof(object) }
                        );

                        var postfixPatch = new HarmonyMethod(
                            typeof(I18nPatcher),
                            nameof(I18nGet_Postfix)
                        );

                        if (getMethod1 != null)
                            harmony.Patch(getMethod1, postfix: postfixPatch);
                        if (getMethod2 != null)
                            harmony.Patch(getMethod2, postfix: postfixPatch);

                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched StardewArchipelago I18n class!",
                            StardewModdingAPI.LogLevel.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch StardewArchipelago I18n: {ex}",
                    StardewModdingAPI.LogLevel.Error
                );
            }
        }

        public static void I18nGet_Postfix(string key, object tokens, ref string __result)
        {
            if (ModEntry.Translation.ContainsKey(key))
            {
                __result = ModEntry.Translation.Get(key, tokens).ToString();
            }
        }
    }
}
