using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using StardewArchipelago.Archipelago;
using StardewArchipelago.Constants;
using StardewArchipelago.Extensions;
using StardewArchipelago.Items.Mail;
using StardewArchipelago.Serialization;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public class MailCategoryData
    {
        public List<string> ItemMails { get; set; } = new();
        public List<string> GiftMails { get; set; } = new();
    }

    [HarmonyPatch(typeof(Mailman))]
    public static class MailPatcher
    {
        private static readonly Random _random = new();

        // Caches for the templates loaded dynamically from templates/mail/{locale}/{category}.json
        public static Dictionary<string, List<string>> ItemTemplates { get; private set; } =
            new(StringComparer.OrdinalIgnoreCase);
        public static Dictionary<string, List<string>> GiftTemplates { get; private set; } =
            new(StringComparer.OrdinalIgnoreCase);

        public static void LoadTemplates(IModHelper helper)
        {
            try
            {
                ItemTemplates.Clear();
                GiftTemplates.Clear();

                string locale = helper.Translation.Locale;
                string dirPath = System.IO.Path.Combine(
                    helper.DirectoryPath,
                    "templates",
                    "mail",
                    locale
                );

                if (!System.IO.Directory.Exists(dirPath))
                {
                    // Fall back to default English templates directory
                    dirPath = System.IO.Path.Combine(
                        helper.DirectoryPath,
                        "templates",
                        "mail",
                        "default"
                    );
                    locale = "default";
                }

                if (System.IO.Directory.Exists(dirPath))
                {
                    var files = System.IO.Directory.GetFiles(dirPath, "*.json");
                    foreach (var file in files)
                    {
                        var category = System.IO.Path.GetFileNameWithoutExtension(file);
                        var loaded = helper.Data.ReadJsonFile<MailCategoryData>(
                            $"templates/mail/{locale}/{category}.json"
                        );

                        if (loaded != null)
                        {
                            if (loaded.ItemMails != null && loaded.ItemMails.Count > 0)
                            {
                                ItemTemplates[category] = loaded.ItemMails;
                            }
                            if (loaded.GiftMails != null && loaded.GiftMails.Count > 0)
                            {
                                GiftTemplates[category] = loaded.GiftMails;
                            }
                        }
                    }
                    ModEntry.Instance.Monitor.Log(
                        $"Successfully loaded custom mail categories from {dirPath}",
                        LogLevel.Info
                    );
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to load mail templates: {ex}",
                    LogLevel.Error
                );
            }
        }

        [HarmonyPatch("SendArchipelagoMail")]
        [HarmonyPrefix]
        public static void SendArchipelagoMail_Prefix(
            ref string apItemName,
            ref string locationName
        )
        {
            apItemName = TranslationHelper.GetLocalizedItemName(apItemName);
            locationName = TranslationHelper.GetLocalizedLocationName(locationName);
        }

        [HarmonyPatch("SendArchipelagoGiftMail")]
        [HarmonyPrefix]
        public static bool SendArchipelagoGiftMail_Prefix(
            Mailman __instance,
            string mailKey,
            string itemName,
            string senderName,
            string senderGame,
            string attachmentEmbedString
        )
        {
            try
            {
                var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);
                string category = GetItemCategory(itemName);
                var localizedSender = TranslationHelper.GetLocalizedPlayerName(senderName);

                // 1. Load all original translated gift templates from pt.json
                var allTemplates = new List<string>();
                for (int i = 0; i < 100; i++)
                {
                    var key = $"mail.gift.{i}";
                    if (ModEntry.Translation.ContainsKey(key))
                    {
                        allTemplates.Add(ModEntry.Translation.Get(key).ToString());
                    }
                }

                // 2. Load custom gift templates for this item category
                var customCategory = GetCustomTemplates(category, isGift: true);
                allTemplates.AddRange(customCategory);

                // 3. Load custom default gift templates
                if (category != "default")
                {
                    var customDefault = GetCustomTemplates("default", isGift: true);
                    allTemplates.AddRange(customDefault);
                }

                if (allTemplates.Count > 0)
                {
                    var template = allTemplates[_random.Next(0, allTemplates.Count)];

                    int energy = 0;
                    int health = 0;
                    if (category == "food")
                    {
                        GetFoodStats(itemName, out energy, out health);
                    }

                    var mailContent = template
                        .Replace("{{item}}", localizedItem)
                        .Replace("{{sender}}", localizedSender)
                        .Replace("{{game}}", senderGame)
                        .Replace("{{farm}}", Game1.player.farmName.Value)
                        .Replace("{{energy}}", energy.ToString())
                        .Replace("{{health}}", health.ToString());

                    if (
                        mailContent.Contains("{0}")
                        || mailContent.Contains("{1}")
                        || mailContent.Contains("{2}")
                        || mailContent.Contains("{3}")
                    )
                    {
                        try
                        {
                            mailContent = string.Format(
                                mailContent,
                                localizedItem,
                                localizedSender,
                                senderGame,
                                attachmentEmbedString
                            );
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log(
                                $"Error formatting legacy gift template '{template}': {ex.Message}",
                                LogLevel.Trace
                            );
                        }
                    }

                    mailContent += attachmentEmbedString + "[#]Archipelago Gift";
                    __instance.GenerateMail(mailKey, mailContent);
                    __instance.SendMail(mailKey);
                    return false; // Skip original
                }

                return true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in SendArchipelagoGiftMail prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }

        [HarmonyPatch(
            "GenerateMail",
            new Type[]
            {
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
                typeof(string),
            }
        )]
        [HarmonyPrefix]
        public static bool GenerateMail_Prefix(
            Mailman __instance,
            string mailKey,
            string apItemName,
            string findingPlayer,
            string locationName,
            string embedString
        )
        {
            try
            {
                var localizedItem = TranslationHelper.GetLocalizedItemName(apItemName);
                var localizedLocation = TranslationHelper.GetLocalizedLocationName(locationName);
                var localizedSender = TranslationHelper.GetLocalizedPlayerName(findingPlayer);
                var anonPlayer = localizedSender.ToAnonymousName();

                // Determine category
                string category = GetItemCategory(apItemName);

                // 1. Load all original translated item templates from pt.json
                var allTemplates = new List<string>();
                for (int i = 0; i < 200; i++)
                {
                    var key = $"mail.item.{i}";
                    if (ModEntry.Translation.ContainsKey(key))
                    {
                        allTemplates.Add(ModEntry.Translation.Get(key).ToString());
                    }
                }

                // 2. Load custom templates for this category
                var customCategory = GetCustomTemplates(category, isGift: false);
                allTemplates.AddRange(customCategory);

                // 3. Load custom default templates
                if (category != "default")
                {
                    var customDefault = GetCustomTemplates("default", isGift: false);
                    allTemplates.AddRange(customDefault);
                }

                if (allTemplates.Count > 0)
                {
                    var template = allTemplates[_random.Next(0, allTemplates.Count)];

                    int energy = 0;
                    int health = 0;
                    if (category == "food")
                    {
                        GetFoodStats(apItemName, out energy, out health);
                    }

                    var mailContent = template
                        .Replace("{{item}}", localizedItem)
                        .Replace("{{sender}}", anonPlayer)
                        .Replace("{{location}}", localizedLocation)
                        .Replace("{{farm}}", Game1.player.farmName.Value)
                        .Replace("{{energy}}", energy.ToString())
                        .Replace("{{health}}", health.ToString());

                    if (
                        mailContent.Contains("{0}")
                        || mailContent.Contains("{1}")
                        || mailContent.Contains("{2}")
                        || mailContent.Contains("{3}")
                        || mailContent.Contains("{4}")
                    )
                    {
                        try
                        {
                            mailContent = string.Format(
                                mailContent,
                                localizedItem,
                                anonPlayer,
                                localizedLocation,
                                embedString,
                                Game1.player.farmName.Value
                            );
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log(
                                $"Error formatting legacy item template '{template}': {ex.Message}",
                                LogLevel.Trace
                            );
                        }
                    }

                    if (mailContent.Contains(Community.NAME_TOKEN))
                    {
                        var randomName = Community.AllNames[
                            _random.Next(0, Community.AllNames.Length)
                        ];
                        mailContent = mailContent.Replace(Community.NAME_TOKEN, randomName);
                    }

                    mailContent += embedString + "[#]Archipelago Item";
                    __instance.GenerateMail(mailKey, mailContent);
                    return false; // Skip original GenerateMail
                }

                return true;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in dynamic GenerateMail prefix: {ex}",
                    LogLevel.Error
                );
                return true;
            }
        }

        [HarmonyPatch("GetRandomApMailString")]
        [HarmonyPostfix]
        public static void GetRandomApMailString_Postfix(Mailman __instance, ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result))
                    return;

                var suffix = "{3}[#]Archipelago Item";
                var cleanTemplate = __result;
                bool hasSuffix = false;
                if (__result.EndsWith(suffix))
                {
                    cleanTemplate = __result.Substring(0, __result.Length - suffix.Length);
                    hasSuffix = true;
                }

                if (
                    cleanTemplate.Equals("{0} from {1} at {2}.", StringComparison.OrdinalIgnoreCase)
                )
                {
                    if (ModEntry.Translation.ContainsKey("mail.concise"))
                    {
                        __result =
                            ModEntry.Translation.Get("mail.concise").ToString()
                            + (hasSuffix ? suffix : "");
                    }
                    return;
                }

                var apMailStringsField = typeof(Mailman).GetField(
                    "ApMailStrings",
                    BindingFlags.Instance | BindingFlags.NonPublic
                );
                var apMailStrings = (string[]?)apMailStringsField?.GetValue(__instance);
                if (apMailStrings != null)
                {
                    int index = Array.IndexOf(apMailStrings, cleanTemplate);
                    if (index >= 0)
                    {
                        var key = $"mail.item.{index}";
                        if (ModEntry.Translation.ContainsKey(key))
                        {
                            var translation = ModEntry.Translation.Get(key).ToString();
                            __result = translation + (hasSuffix ? suffix : "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in GetRandomApMailString Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }

        [HarmonyPatch("GetRandomApMailGiftString")]
        [HarmonyPostfix]
        public static void GetRandomApMailGiftString_Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrEmpty(__result))
                    return;

                var suffix = "{3}[#]Archipelago Gift";
                var cleanTemplate = __result;
                bool hasSuffix = false;
                if (__result.EndsWith(suffix))
                {
                    cleanTemplate = __result.Substring(0, __result.Length - suffix.Length);
                    hasSuffix = true;
                }

                var apGiftStringsField = typeof(Mailman).GetField(
                    "ApGiftStrings",
                    BindingFlags.Static | BindingFlags.NonPublic
                );
                var apGiftStrings = (string[]?)apGiftStringsField?.GetValue(null);
                if (apGiftStrings != null)
                {
                    int index = Array.IndexOf(apGiftStrings, cleanTemplate);
                    if (index >= 0)
                    {
                        var key = $"mail.gift.{index}";
                        if (ModEntry.Translation.ContainsKey(key))
                        {
                            var translation = ModEntry.Translation.Get(key).ToString();
                            __result = translation + (hasSuffix ? suffix : "");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in GetRandomApMailGiftString Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }

        // Helper to retrieve category templates
        public static List<string> GetCustomTemplates(string category, bool isGift)
        {
            var list = new List<string>();
            if (isGift)
            {
                if (GiftTemplates.TryGetValue(category, out var templates))
                {
                    list.AddRange(templates);
                }
            }
            else
            {
                if (ItemTemplates.TryGetValue(category, out var templates))
                {
                    list.AddRange(templates);
                }
            }
            return list;
        }

        // Get native food statistics dynamically
        public static void GetFoodStats(string itemName, out int energy, out int health)
        {
            energy = 0;
            health = 0;
            try
            {
                var objects = Game1.content.Load<
                    Dictionary<string, StardewValley.GameData.Objects.ObjectData>
                >("Data\\Objects");
                if (objects != null)
                {
                    var matchObj = System.Linq.Enumerable.FirstOrDefault(
                        objects.Values,
                        o => o.Name.Equals(itemName, StringComparison.OrdinalIgnoreCase)
                    );
                    if (matchObj != null)
                    {
                        int edibility = matchObj.Edibility;
                        if (edibility > 0)
                        {
                            energy = (int)(edibility * 2.5);
                            health = (int)(energy * 0.45);
                        }
                    }
                }
            }
            catch { }
        }

        // Classify the item name into beautiful granular categories (e.g. food, weapon, tool, ring, boots, book, fish, default)
        public static string GetItemCategory(string itemName)
        {
            if (string.IsNullOrEmpty(itemName))
                return "default";
            var lower = itemName.ToLowerInvariant();

            // Weapons
            if (
                lower.Contains("sword")
                || lower.Contains("blade")
                || lower.Contains("dagger")
                || lower.Contains("hammer")
                || lower.Contains("club")
                || lower.Contains("gavel")
                || lower.Contains("slingshot")
                || lower.Contains("scythe")
                || lower.Contains("mallet")
                || lower.Contains("falchion")
                || lower.Contains("cutlass")
                || lower.Contains("claymore")
                || lower.Contains("saber")
                || lower.Contains("rapier")
                || lower.Contains("kris")
                || lower.Contains("shank")
                || lower.Contains("dirk")
                || lower.Contains("mace")
                || lower.Contains("katana")
                || lower.Contains("glaive")
                || lower.Contains("spear")
                || lower.Contains("trident")
                || lower.Contains("cleaver")
            )
            {
                return "weapon";
            }

            // Tools
            if (
                lower.Contains("axe")
                || lower.Contains("pickaxe")
                || lower.Contains("hoe")
                || lower.Contains("watering can")
                || lower.Contains("shears")
                || lower.Contains("pail")
                || lower.Contains("rod")
                || lower.Contains("pan")
                || lower.Contains("flute")
                || lower.Contains("drum")
                || lower.Contains("bell")
                || lower.Contains("wand")
                || lower.Contains("scepter")
            )
            {
                return "tool";
            }

            // Rings
            if (
                lower.Contains("ring")
                || lower.Contains("band")
                || lower.Contains("amulet")
                || lower.Contains("talisman")
            )
            {
                return "ring";
            }

            // Boots
            if (
                lower.Contains("boots")
                || lower.Contains("shoes")
                || lower.Contains("sneakers")
                || lower.Contains("heels")
                || lower.Contains("sandals")
            )
            {
                return "boots";
            }

            // Books
            if (
                lower.Contains("book")
                || lower.Contains("guide")
                || lower.Contains("manual")
                || lower.Contains("journal")
                || lower.Contains("almanac")
                || lower.Contains("catalogue")
            )
            {
                return "book";
            }

            // Fish & Sea creatures
            if (
                lower.Contains("fish")
                || lower.Contains("carp")
                || lower.Contains("catfish")
                || lower.Contains("shad")
                || lower.Contains("bream")
                || lower.Contains("chub")
                || lower.Contains("trout")
                || lower.Contains("bass")
                || lower.Contains("walleye")
                || lower.Contains("salmon")
                || lower.Contains("eel")
                || lower.Contains("squid")
                || lower.Contains("octopus")
                || lower.Contains("pufferfish")
                || lower.Contains("tuna")
                || lower.Contains("sardine")
                || lower.Contains("anchovy")
                || lower.Contains("herring")
                || lower.Contains("halibut")
                || lower.Contains("sturgeon")
                || lower.Contains("tilapia")
                || lower.Contains("cucumber")
                || lower.Contains("lobster")
                || lower.Contains("crab")
                || lower.Contains("crayfish")
                || lower.Contains("shrimp")
                || lower.Contains("snail")
                || lower.Contains("periwinkle")
                || lower.Contains("mussel")
                || lower.Contains("cockle")
                || lower.Contains("oyster")
                || lower.Contains("clam")
            )
            {
                return "fish";
            }

            // Foods (Eatables)
            if (
                lower.Contains("cookie")
                || lower.Contains("cake")
                || lower.Contains("pie")
                || lower.Contains("soup")
                || lower.Contains("salad")
                || lower.Contains("dinner")
                || lower.Contains("wine")
                || lower.Contains("beer")
                || lower.Contains("juice")
                || lower.Contains("coffee")
                || lower.Contains("tea")
                || lower.Contains("milk")
                || lower.Contains("egg")
                || lower.Contains("cheese")
                || lower.Contains("honey")
                || lower.Contains("bread")
                || lower.Contains("candy")
                || lower.Contains("apple")
                || lower.Contains("peach")
                || lower.Contains("orange")
                || lower.Contains("cherry")
                || lower.Contains("pomegranate")
                || lower.Contains("apricot")
                || lower.Contains("banana")
                || lower.Contains("mango")
                || lower.Contains("melon")
                || lower.Contains("berry")
                || lower.Contains("grape")
                || lower.Contains("coconut")
                || lower.Contains("fruit")
                || lower.Contains("chocolate")
                || lower.Contains("stew")
                || lower.Contains("dish")
                || lower.Contains("taco")
                || lower.Contains("burger")
                || lower.Contains("pizza")
                || lower.Contains("roll")
                || lower.Contains("pasta")
                || lower.Contains("noodle")
                || lower.Contains("jelly")
                || lower.Contains("jam")
                || lower.Contains("butter")
                || lower.Contains("sauce")
                || lower.Contains("oil")
                || lower.Contains("sugar")
                || lower.Contains("salt")
                || lower.Contains("pepper")
                || lower.Contains("syrup")
                || lower.Contains("toast")
                || lower.Contains("pancake")
                || lower.Contains("waffle")
                || lower.Contains("omelet")
                || lower.Contains("scramble")
            )
            {
                return "food";
            }

            return "default";
        }
    }
}
