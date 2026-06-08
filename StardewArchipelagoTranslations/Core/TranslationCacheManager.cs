using System;
using System.Collections.Generic;
using System.Diagnostics;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal static class TranslationCacheManager
    {
        private static readonly ITranslationCacheSource[] Sources =
        {
            new CacheSource("GameStringMap", WarmGameStringMap, ClearGameStringMap),
            new CacheSource(
                "QuestLocations",
                QuestLocationResolver.WarmUp,
                QuestLocationResolver.ClearCache
            ),
            new CacheSource("Books", ReadBookResolver.WarmUp, ReadBookResolver.ClearCache),
            new CacheSource(
                "VanillaObjects",
                VanillaObjectResolver.WarmUp,
                VanillaObjectResolver.ClearCache
            ),
            new CacheSource("Powers", PowerKeyResolver.WarmUp, PowerKeyResolver.ClearCache),
            new CacheSource("Bundles", BundleResolver.WarmUp, BundleResolver.ClearCache),
            new CacheSource("Weekdays", WeekdayResolver.WarmUp, WeekdayResolver.ClearCache),
            new CacheSource("Hats", WearResolver.WarmUp, WearResolver.ClearCache),
            new CacheSource(
                "Monsters",
                MonsterEradicationResolver.WarmUp,
                MonsterEradicationResolver.ClearCache
            ),
        };

        internal static void WarmAll()
        {
            var total = Stopwatch.StartNew();
            ModEntry.Instance.Monitor.Log(
                "[TranslationCacheManager] Aquecendo caches de tradução...",
                LogLevel.Debug
            );

            foreach (var source in Sources)
            {
                WarmSource(source);
            }

            TranslationHelper.PrepopulateCaches();

            ModEntry.Instance.Monitor.Log(
                $"[TranslationCacheManager] Caches prontos em {total.ElapsedMilliseconds}ms.",
                LogLevel.Info
            );
        }

        internal static void ClearAll()
        {
            foreach (var source in Sources)
            {
                try
                {
                    source.Clear();
                }
                catch (Exception ex)
                {
                    ModEntry.Instance.Monitor.Log(
                        $"[TranslationCacheManager] Erro ao limpar '{source.Name}': {ex.Message}",
                        LogLevel.Trace
                    );
                }
            }

            VanillaRecipeResolver.ClearCache();
            TranslationHelper.ClearResultCaches();
        }

        private static void WarmSource(ITranslationCacheSource source)
        {
            var sw = Stopwatch.StartNew();
            try
            {
                source.WarmUp();
                ModEntry.Instance.Monitor.Log(
                    $"[TranslationCacheManager] {source.Name}: {sw.ElapsedMilliseconds}ms",
                    LogLevel.Trace
                );
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[TranslationCacheManager] Falha ao aquecer '{source.Name}': {ex}",
                    LogLevel.Warn
                );
            }
        }

        private static void WarmGameStringMap() => TranslationHelper.BuildGameStringMap();

        private static void ClearGameStringMap() => TranslationHelper.ClearGameStringMap();

        private sealed class CacheSource : ITranslationCacheSource
        {
            private readonly Action _warmUp;
            private readonly Action _clear;

            public CacheSource(string name, Action warmUp, Action clear)
            {
                Name = name;
                _warmUp = warmUp;
                _clear = clear;
            }

            public string Name { get; }

            public void WarmUp() => _warmUp();

            public void Clear() => _clear();
        }
    }
}
