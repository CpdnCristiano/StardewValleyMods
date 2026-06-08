using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class WearResolver : ILocationResolver
    {
        private static Dictionary<string, string>? _vanillaHatsNameMap;
        private static readonly object _hatsLock = new object();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string prefix = "Wear ";
            if (!englishName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var englishHatName = englishName.Substring(prefix.Length).Trim();
            var localizedHat = GetLocalizedHatName(englishHatName);
            localizedName = ModEntry
                .Translation.Get("wear.format", new { hat = localizedHat })
                .ToString();
            return true;
        }

        internal static void WarmUp() => EnsureHatsMap();

        internal static void ClearCache()
        {
            lock (_hatsLock)
            {
                _vanillaHatsNameMap = null;
            }
        }

        private static void EnsureHatsMap()
        {
            if (_vanillaHatsNameMap != null)
            {
                return;
            }

            lock (_hatsLock)
            {
                if (_vanillaHatsNameMap != null)
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
                Dictionary<string, string>? hats = null;
                try
                {
                    hats = engManager.Load<Dictionary<string, string>>("Data\\Hats");
                }
                finally
                {
                    LocalizedContentManager.CurrentLanguageCode = savedLang;
                }

                if (hats != null)
                {
                    foreach (var pair in hats)
                    {
                        if (pair.Value == null)
                        {
                            continue;
                        }

                        var parts = pair.Value.Split('/');
                        if (parts.Length <= 0)
                        {
                            continue;
                        }

                        var internalName = parts[0];
                        var qualifiedId = $"(H){pair.Key}";

                        map.TryAdd(internalName, qualifiedId);

                        var cleanName = ResolverText.ToCompactKeySegment(internalName);
                        map.TryAdd(cleanName, qualifiedId);
                    }
                }

                _vanillaHatsNameMap = map;
            }
        }

        private string GetLocalizedHatName(string englishHatName)
        {
            try
            {
                EnsureHatsMap();

                var cleanLookupKey = ResolverText.ToCompactKeySegment(englishHatName);
                var underscoreLookupKey = ResolverText.ToKeySegment(englishHatName);

                string? qualId = null;
                _vanillaHatsNameMap!.TryGetValue(englishHatName, out qualId);
                if (qualId == null)
                {
                    _vanillaHatsNameMap.TryGetValue(underscoreLookupKey, out qualId);
                }
                if (qualId == null)
                {
                    _vanillaHatsNameMap.TryGetValue(cleanLookupKey, out qualId);
                }

                if (qualId != null)
                {
                    var data = ItemRegistry.GetData(qualId);
                    if (data != null && !string.IsNullOrWhiteSpace(data.DisplayName))
                    {
                        return data.DisplayName;
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error resolving hat name for '{englishHatName}': {ex}",
                    LogLevel.Error
                );
            }

            return TranslationHelper.GetLocalizedItemName(englishHatName);
        }
    }
}
