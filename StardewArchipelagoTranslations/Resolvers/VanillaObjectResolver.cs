using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class VanillaObjectResolver : IItemResolver
    {
        private static Dictionary<string, string>? _vanillaObjectsNameMap;
        private static readonly object _objectsLock = new();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            try
            {
                EnsureObjectMap();

                var cleanLookupKey = ResolverText.ToCompactKeySegment(englishName);
                var underscoreLookupKey = ResolverText.ToKeySegment(englishName);

                string? qualId = null;
                _vanillaObjectsNameMap!.TryGetValue(englishName, out qualId);
                if (qualId == null)
                {
                    _vanillaObjectsNameMap.TryGetValue(underscoreLookupKey, out qualId);
                }
                if (qualId == null)
                {
                    _vanillaObjectsNameMap.TryGetValue(cleanLookupKey, out qualId);
                }

                if (qualId != null)
                {
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

        internal static void WarmUp() => EnsureObjectMap();

        private static void EnsureObjectMap()
        {
            if (_vanillaObjectsNameMap != null)
            {
                return;
            }

            lock (_objectsLock)
            {
                if (_vanillaObjectsNameMap != null)
                {
                    return;
                }

                var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                using var engManager = new Microsoft.Xna.Framework.Content.ContentManager(
                    Game1.game1.Content.ServiceProvider,
                    Game1.game1.Content.RootDirectory
                );
                var savedLang = LocalizedContentManager.CurrentLanguageCode;
                LocalizedContentManager.CurrentLanguageCode = LocalizedContentManager
                    .LanguageCode
                    .en;
                Dictionary<string, StardewValley.GameData.Objects.ObjectData>? objects = null;
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
                        if (pair.Value == null || string.IsNullOrWhiteSpace(pair.Value.Name))
                        {
                            continue;
                        }

                        var qualifiedId = $"(O){pair.Key}";

                        map.TryAdd(pair.Value.Name, qualifiedId);
                        map.TryAdd(pair.Key, qualifiedId);

                        var cleanName = ResolverText.ToCompactKeySegment(pair.Value.Name);
                        map.TryAdd(cleanName, qualifiedId);

                        var cleanKey = ResolverText.ToCompactKeySegment(pair.Key);
                        map.TryAdd(cleanKey, qualifiedId);
                    }

                    try
                    {
                        var objectIdsType = typeof(StardewArchipelago.Constants.Vanilla.ObjectIds);
                        var fields = objectIdsType.GetFields(
                            System.Reflection.BindingFlags.Public
                                | System.Reflection.BindingFlags.Static
                                | System.Reflection.BindingFlags.FlattenHierarchy
                        );
                        foreach (var field in fields)
                        {
                            if (
                                !field.IsLiteral
                                || field.IsInitOnly
                                || field.FieldType != typeof(string)
                            )
                            {
                                continue;
                            }

                            var objectId = field.GetValue(null) as string;
                            if (string.IsNullOrWhiteSpace(objectId))
                            {
                                continue;
                            }

                            var qualifiedId = $"(O){objectId}";
                            var cleanFieldName = ResolverText.ToCompactKeySegment(field.Name);
                            map.TryAdd(cleanFieldName, qualifiedId);

                            var fieldNameWithUnderscores = ResolverText.ToKeySegment(field.Name);
                            map.TryAdd(fieldNameWithUnderscores, qualifiedId);
                        }
                    }
                    catch (Exception ex)
                    {
                        ModEntry.Instance.Monitor.Log(
                            $"[VanillaObjectResolver] Error building ObjectIds map: {ex.Message}",
                            LogLevel.Trace
                        );
                    }
                }

                _vanillaObjectsNameMap = map;
            }
        }

        internal static void ClearCache()
        {
            lock (_objectsLock)
            {
                _vanillaObjectsNameMap = null;
            }
        }
    }
}
