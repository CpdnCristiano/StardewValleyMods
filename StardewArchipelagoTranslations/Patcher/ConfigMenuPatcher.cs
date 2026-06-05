#pragma warning disable CS8600, CS8601, CS8602, CS8603, CS8604, CS8605, CS8625
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using StardewModdingAPI;
using StardewModdingAPI.Utilities;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public interface IGenericModConfigMenuApi
    {
        void Register(IManifest mod, Action reset, Action save, bool titleScreenOnly = false);
        void AddSectionTitle(IManifest mod, Func<string> text, Func<string> tooltip = null);
        void AddParagraph(IManifest mod, Func<string> text);
        void AddBoolOption(
            IManifest mod,
            Func<bool> getValue,
            Action<bool> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            string fieldId = null
        );
        void AddNumberOption(
            IManifest mod,
            Func<int> getValue,
            Action<int> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            int? min = null,
            int? max = null,
            int? interval = null,
            Func<int, string> formatValue = null,
            string fieldId = null
        );
        void AddKeybindList(
            IManifest mod,
            Func<KeybindList> getValue,
            Action<KeybindList> setValue,
            Func<string> name,
            Func<string> tooltip = null,
            string fieldId = null
        );
        void SetTitleScreenOnlyForNextOptions(IManifest mod, bool titleScreenOnly);
        void OnFieldChanged(IManifest mod, Action<string, object> onChange);
        void Unregister(IManifest mod);
    }

    public static class ConfigMenuPatcher
    {
        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.Integrations.GenericModConfigMenu.GenericModConfig"
                );
                if (targetType != null)
                {
                    var registerMethod = AccessTools.Method(targetType, "RegisterConfig");
                    if (registerMethod != null)
                    {
                        harmony.Patch(
                            registerMethod,
                            prefix: new HarmonyMethod(
                                typeof(ConfigMenuPatcher),
                                nameof(RegisterConfig_Prefix)
                            )
                        );
                        ModEntry.Instance.Monitor.Log(
                            "Successfully patched GenericModConfig.RegisterConfig for GMCM translations!",
                            LogLevel.Info
                        );
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch GenericModConfig: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static bool RegisterConfig_Prefix(object __instance)
        {
            try
            {
                var helperField = __instance
                    .GetType()
                    .GetField("Helper", BindingFlags.Instance | BindingFlags.NonPublic);
                var manifestField = __instance
                    .GetType()
                    .GetField("ModManifest", BindingFlags.Instance | BindingFlags.NonPublic);
                var configField = __instance
                    .GetType()
                    .GetField("Config", BindingFlags.Instance | BindingFlags.NonPublic);

                if (helperField == null || manifestField == null || configField == null)
                    return true;

                var helper = helperField.GetValue(__instance) as IModHelper;
                var manifest = manifestField.GetValue(__instance) as IManifest;
                var config = configField.GetValue(__instance);

                if (helper == null || manifest == null || config == null)
                    return true;

                var configMenu = helper.ModRegistry.GetApi<IGenericModConfigMenuApi>(
                    "spacechase0.GenericModConfigMenu"
                );
                if (configMenu == null)
                {
                    return false; // skip original since it would do nothing anyway
                }

                // Reset and Save actions
                var resetAction = new Action(() =>
                {
                    var newConfig = Activator.CreateInstance(config.GetType());
                    configField.SetValue(__instance, newConfig);
                });

                var saveAction = new Action(() =>
                {
                    var currentConfig = configField.GetValue(__instance);
                    var writeMethod = helper.GetType().GetMethod("WriteConfig");
                    var genericWrite = writeMethod?.MakeGenericMethod(config.GetType());
                    genericWrite?.Invoke(helper, new[] { currentConfig });
                });

                configMenu.Register(mod: manifest, reset: resetAction, save: saveAction);

                // Helper to get translated config property values
                object? GetControls(object cfg) =>
                    cfg.GetType().GetProperty("Controls")?.GetValue(cfg);
                KeybindList GetOpenMail(object? controlsObj) =>
                    (
                        controlsObj?.GetType().GetProperty("OpenMail")?.GetValue(controlsObj)
                        as KeybindList
                    ) ?? new KeybindList();
                void SetOpenMail(object? controlsObj, KeybindList val) =>
                    controlsObj?.GetType().GetProperty("OpenMail")?.SetValue(controlsObj, val);

                configMenu.AddKeybindList(
                    mod: manifest,
                    name: () => ModEntry.Translation.Get("config.open_mail.name").ToString(),
                    tooltip: () => ModEntry.Translation.Get("config.open_mail.tooltip").ToString(),
                    getValue: () =>
                    {
                        var cfg = configField.GetValue(__instance);
                        return cfg != null ? GetOpenMail(GetControls(cfg)) : new KeybindList();
                    },
                    setValue: (value) =>
                    {
                        var cfg = configField.GetValue(__instance);
                        if (cfg != null)
                            SetOpenMail(GetControls(cfg), value);
                    }
                );

                configMenu.SetTitleScreenOnlyForNextOptions(manifest, true);

                // Generic helpers for boolean options
                void AddBool(string key, string propName)
                {
                    configMenu.AddBoolOption(
                        mod: manifest,
                        name: () => ModEntry.Translation.Get($"config.{key}.name").ToString(),
                        tooltip: () => ModEntry.Translation.Get($"config.{key}.tooltip").ToString(),
                        getValue: () =>
                        {
                            var cfg = configField.GetValue(__instance);
                            var val = cfg?.GetType().GetProperty(propName)?.GetValue(cfg);
                            return val is bool b && b;
                        },
                        setValue: (value) =>
                        {
                            var cfg = configField.GetValue(__instance);
                            cfg?.GetType().GetProperty(propName)?.SetValue(cfg, value);
                        }
                    );
                }

                AddBool("remote_cc", "RemoteCommunityCenter");
                AddBool("seed_shop", "EnableSeedShopOverhaul");
                AddBool("hide_empty_letters", "HideEmptyArchipelagoLetters");
                AddBool("disable_letter_templates", "DisableLetterTemplates");
                AddBool("hide_npc_gifts", "HideNpcGiftMail");
                AddBool("custom_icons", "UseCustomArchipelagoIcons");
                AddBool("skip_hold_up", "SkipHoldUpAnimations");
                AddBool("disable_friendship_decay", "DisableFriendshipDecay");

                // Bonus Per Movement Speed
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () => ModEntry.Translation.Get("config.bonus_speed.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.bonus_speed.tooltip").ToString(),
                    min: 0,
                    max: 20,
                    interval: 1,
                    getValue: () =>
                        (int)
                            configField
                                .GetValue(__instance)
                                .GetType()
                                .GetProperty("BonusPerMovementSpeed")
                                .GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        configField
                            .GetValue(__instance)
                            .GetType()
                            .GetProperty("BonusPerMovementSpeed")
                            .SetValue(configField.GetValue(__instance), value),
                    formatValue: (value) => $"{value * 5}%"
                );

                AddBool("multiplayer_vision", "MultiplayerVision");
                AddBool("anonymize_chat", "AnonymizeNamesInChat");

                // Display Items In Chat
                var displayItemsProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("DisplayItemsInChat");
                var chatFilterEnum = displayItemsProp.PropertyType;
                var chatFilterValues = Enum.GetValues(chatFilterEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.display_chat_items.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.display_chat_items.tooltip").ToString(),
                    min: chatFilterValues.Min(),
                    max: chatFilterValues.Max(),
                    interval: 1,
                    getValue: () =>
                        (int)displayItemsProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        displayItemsProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(chatFilterEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(chatFilterEnum, value)
                );

                AddBool("connection_messages", "EnableConnectionMessages");
                AddBool("chat_messages", "EnableChatMessages");
                AddBool("calendar_indicators", "ShowCalendarIndicators");
                AddBool("elevator_indicators", "ShowElevatorIndicators");
                AddBool("strict_logic", "StrictLogic");

                // Show Item Indicators
                var showItemsProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("ShowItemIndicators");
                var itemIndEnum = showItemsProp.PropertyType;
                var itemIndValues = Enum.GetValues(itemIndEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () => ModEntry.Translation.Get("config.item_indicators.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.item_indicators.tooltip").ToString(),
                    min: itemIndValues.Min(),
                    max: itemIndValues.Max(),
                    interval: 1,
                    getValue: () => (int)showItemsProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        showItemsProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(itemIndEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(itemIndEnum, value)
                );

                // MultiSleep Season Behavior
                var sleepSeasonProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("MultiSleepSeasonPreference");
                var seasonEnum = sleepSeasonProp.PropertyType;
                var seasonValues = Enum.GetValues(seasonEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.multisleep_season.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.multisleep_season.tooltip").ToString(),
                    min: seasonValues.Min(),
                    max: seasonValues.Max(),
                    interval: 1,
                    getValue: () => (int)sleepSeasonProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        sleepSeasonProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(seasonEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(seasonEnum, value)
                );

                // Grandpa Shrine Icons
                var grandpaProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("ShowGrandpaShrineIndicators");
                var grandpaEnum = grandpaProp.PropertyType;
                var grandpaValues = Enum.GetValues(grandpaEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () => ModEntry.Translation.Get("config.grandpa_shrine.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.grandpa_shrine.tooltip").ToString(),
                    min: grandpaValues.Min(),
                    max: grandpaValues.Max(),
                    interval: 1,
                    getValue: () => (int)grandpaProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        grandpaProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(grandpaEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(grandpaEnum, value)
                );

                // Sprite Randomizer
                var spriteRandProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("SpriteRandomizer");
                var spriteRandEnum = spriteRandProp.PropertyType;
                var spriteRandValues = Enum.GetValues(spriteRandEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.sprite_randomizer.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.sprite_randomizer.tooltip").ToString(),
                    min: spriteRandValues.Min(),
                    max: spriteRandValues.Max(),
                    interval: 1,
                    getValue: () => (int)spriteRandProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        spriteRandProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(spriteRandEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(spriteRandEnum, value)
                );

                AddBool("start_lotl", "StartWithLivingOffTheLand");
                AddBool("start_gazette", "StartWithGatewayGazette");
                AddBool("allow_hand_breaking", "AllowHandBreaking");

                // Bookseller Price Multiplier
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.bookseller_multiplier.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.bookseller_multiplier.tooltip").ToString(),
                    min: 5,
                    max: 1000,
                    interval: 1,
                    getValue: () =>
                        (int)
                            configField
                                .GetValue(__instance)
                                .GetType()
                                .GetProperty("BooksellerPriceMultiplier")
                                .GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        configField
                            .GetValue(__instance)
                            .GetType()
                            .GetProperty("BooksellerPriceMultiplier")
                            .SetValue(configField.GetValue(__instance), value),
                    formatValue: (value) => $"{value}%"
                );

                // Preference for Scout Hints
                var scoutHintProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("ScoutHintBehavior");
                var scoutEnum = scoutHintProp.PropertyType;
                var scoutValues = Enum.GetValues(scoutEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () => ModEntry.Translation.Get("config.scout_hints.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.scout_hints.tooltip").ToString(),
                    min: scoutValues.Min(),
                    max: scoutValues.Max(),
                    interval: 1,
                    getValue: () => (int)scoutHintProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        scoutHintProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(scoutEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(scoutEnum, value)
                );

                AddBool("custom_assets", "CustomAssets");
                AddBool("custom_assets_flexible", "CustomAssetGameFlexible");
                AddBool("custom_assets_generic", "CustomAssetGenericGame");
                AddBool("lupini_scouted", "ShowLupiniScoutedItem");
                AddBool("limit_tools_level", "LimitHoeWateringCanLevel");
                AddBool("legacy_randomization", "UseLegacyRandomization");

                // Bonus Repeatable Walnuts
                var repeatableWalnutsProp = configField
                    .GetValue(__instance)
                    .GetType()
                    .GetProperty("BonusRepeatableWalnuts");
                var walnutsEnum = repeatableWalnutsProp.PropertyType;
                var walnutsValues = Enum.GetValues(walnutsEnum).Cast<int>().ToArray();
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.repeatable_walnuts.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.repeatable_walnuts.tooltip").ToString(),
                    min: walnutsValues.Min(),
                    max: walnutsValues.Max(),
                    interval: 1,
                    getValue: () =>
                        (int)repeatableWalnutsProp.GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        repeatableWalnutsProp.SetValue(
                            configField.GetValue(__instance),
                            Enum.ToObject(walnutsEnum, value)
                        ),
                    formatValue: (value) => Enum.GetName(walnutsEnum, value)
                );

                AddBool("jojapocalypse_harder_goals", "JojapocalypseHarderGoals");

                // Jojapocalypse Minimum Completion Percent
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.jojapocalypse_percent.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.jojapocalypse_percent.tooltip").ToString(),
                    min: 0,
                    max: 100,
                    interval: 1,
                    getValue: () =>
                        (int)
                            configField
                                .GetValue(__instance)
                                .GetType()
                                .GetProperty("JojapocalypseMinimumCompletionPercentToGoal")
                                .GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        configField
                            .GetValue(__instance)
                            .GetType()
                            .GetProperty("JojapocalypseMinimumCompletionPercentToGoal")
                            .SetValue(configField.GetValue(__instance), value),
                    formatValue: (value) => $"{value}%"
                );

                // Connection Retries before force sleep
                configMenu.AddNumberOption(
                    mod: manifest,
                    name: () =>
                        ModEntry.Translation.Get("config.connection_retries.name").ToString(),
                    tooltip: () =>
                        ModEntry.Translation.Get("config.connection_retries.tooltip").ToString(),
                    min: 0,
                    max: 20,
                    interval: 1,
                    getValue: () =>
                        (int)
                            configField
                                .GetValue(__instance)
                                .GetType()
                                .GetProperty("ConnectionRetriesBeforeForceSleep")
                                .GetValue(configField.GetValue(__instance)),
                    setValue: (value) =>
                        configField
                            .GetValue(__instance)
                            .GetType()
                            .GetProperty("ConnectionRetriesBeforeForceSleep")
                            .SetValue(configField.GetValue(__instance), value),
                    formatValue: (value) => $"{value}"
                );

                return false; // skip original English setup
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in RegisterConfig_Prefix: {ex}",
                    LogLevel.Error
                );
                return true; // fall back to original English setup
            }
        }
    }
}
