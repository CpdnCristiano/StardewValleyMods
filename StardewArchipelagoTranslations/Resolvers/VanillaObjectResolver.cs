using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class VanillaObjectResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            try
            {
                if (TranslationHelper._vanillaObjectsNameMap == null)
                {
                    lock (TranslationHelper._objectsLock)
                    {
                        if (TranslationHelper._vanillaObjectsNameMap == null)
                        {
                            // Map: English internal name / ID → qualified item ID
                            // We store qualified IDs, NOT DisplayName, so we can always
                            // call ItemRegistry.GetData() at lookup time and get the
                            // fully-resolved display name for the current locale.
                            TranslationHelper._vanillaObjectsNameMap = new Dictionary<
                                string,
                                string
                            >(StringComparer.OrdinalIgnoreCase);

                            // Load Data\Objects in English so names are always canonical
                            using var engManager =
                                new Microsoft.Xna.Framework.Content.ContentManager(
                                    Game1.game1.Content.ServiceProvider,
                                    Game1.game1.Content.RootDirectory
                                );
                            var savedLang = LocalizedContentManager.CurrentLanguageCode;
                            LocalizedContentManager.CurrentLanguageCode = LocalizedContentManager
                                .LanguageCode
                                .en;
                            Dictionary<string, StardewValley.GameData.Objects.ObjectData>? objects =
                                null;
                            try
                            {
                                objects = engManager.Load<
                                    Dictionary<string, StardewValley.GameData.Objects.ObjectData>
                                >("Data\\Objects");
                            }
                            finally
                            {
                                LocalizedContentManager.CurrentLanguageCode = savedLang;
                            }

                            if (objects != null)
                            {
                                foreach (var pair in objects)
                                {
                                    if (
                                        pair.Value != null
                                        && !string.IsNullOrWhiteSpace(pair.Value.Name)
                                    )
                                    {
                                        var qualifiedId = $"(O){pair.Key}";

                                        // Index by internal Name (e.g. "Ancient Doll")
                                        TranslationHelper._vanillaObjectsNameMap.TryAdd(
                                            pair.Value.Name,
                                            qualifiedId
                                        );

                                        // Index by numeric/string ID (e.g. "39")
                                        TranslationHelper._vanillaObjectsNameMap.TryAdd(
                                            pair.Key,
                                            qualifiedId
                                        );

                                        // Index by name without spaces/apostrophes
                                        var cleanName = pair
                                            .Value.Name.Replace(" ", "")
                                            .Replace("'", "")
                                            .Replace("_", "");
                                        TranslationHelper._vanillaObjectsNameMap.TryAdd(
                                            cleanName,
                                            qualifiedId
                                        );

                                        // Index by ID without spaces/apostrophes
                                        var cleanKey = pair
                                            .Key.Replace(" ", "")
                                            .Replace("'", "")
                                            .Replace("_", "");
                                        TranslationHelper._vanillaObjectsNameMap.TryAdd(
                                            cleanKey,
                                            qualifiedId
                                        );
                                    }
                                }

                                try
                                {
                                    var objectIdsType =
                                        typeof(StardewArchipelago.Constants.Vanilla.ObjectIds);
                                    var fields = objectIdsType.GetFields(
                                        System.Reflection.BindingFlags.Public
                                            | System.Reflection.BindingFlags.Static
                                            | System.Reflection.BindingFlags.FlattenHierarchy
                                    );
                                    foreach (var field in fields)
                                    {
                                        if (
                                            field.IsLiteral
                                            && !field.IsInitOnly
                                            && field.FieldType == typeof(string)
                                        )
                                        {
                                            var objectId = field.GetValue(null) as string;
                                            if (!string.IsNullOrWhiteSpace(objectId))
                                            {
                                                var qualifiedId = $"(O){objectId}";
                                                var cleanFieldName = field
                                                    .Name.Replace("_", "")
                                                    .Replace("'", "")
                                                    .ToLower();
                                                TranslationHelper._vanillaObjectsNameMap.TryAdd(
                                                    cleanFieldName,
                                                    qualifiedId
                                                );

                                                var fieldNameWithUnderscores = field
                                                    .Name.Replace("'", "")
                                                    .ToLower();
                                                TranslationHelper._vanillaObjectsNameMap.TryAdd(
                                                    fieldNameWithUnderscores,
                                                    qualifiedId
                                                );
                                            }
                                        }
                                    }
                                }
                                catch (Exception ex)
                                {
                                    ModEntry.Instance.Monitor.Log(
                                        $"Error building ObjectIds reflection map: {ex.Message}",
                                        LogLevel.Trace
                                    );
                                }
                            }
                        }
                    }
                }

                var cleanLookupKey = englishName.Replace(" ", "").Replace("'", "").Replace("_", "");
                var underscoreLookupKey = englishName.Replace(" ", "_").Replace("'", "");

                string? qualId = null;
                TranslationHelper._vanillaObjectsNameMap.TryGetValue(englishName, out qualId);
                if (qualId == null)
                    TranslationHelper._vanillaObjectsNameMap.TryGetValue(
                        underscoreLookupKey,
                        out qualId
                    );
                if (qualId == null)
                    TranslationHelper._vanillaObjectsNameMap.TryGetValue(
                        cleanLookupKey,
                        out qualId
                    );

                if (qualId != null)
                {
                    // Always resolve through ItemRegistry so we get the current-locale
                    // display name, fully parsed (no raw token strings like
                    // "Strings\StringsFromCSFiles:Utility.cs.5627").
                    var data = ItemRegistry.GetData(qualId);
                    if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
                    {
                        localizedName = data.DisplayName;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }
    }
}
