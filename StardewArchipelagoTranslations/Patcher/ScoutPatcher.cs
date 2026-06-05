using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using KaitoKid.ArchipelagoUtilities.Net.Client;
using StardewModdingAPI;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ScoutPatcher
    {
        private static readonly Dictionary<string, ScoutedLocation> _scoutedLocationsCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly object _cacheLock = new();

        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.Archipelago.StardewArchipelagoClient"
                );
                if (targetType != null)
                {
                    // Patch ScoutStardewLocation
                    var scoutMethod = AccessTools.Method(
                        targetType,
                        "ScoutStardewLocation",
                        new[] { typeof(string), typeof(bool) }
                    );
                    if (scoutMethod != null)
                    {
                        harmony.Patch(
                            scoutMethod,
                            prefix: new HarmonyMethod(
                                typeof(ScoutPatcher),
                                nameof(ScoutStardewLocation_Prefix)
                            ),
                            postfix: new HarmonyMethod(
                                typeof(ScoutPatcher),
                                nameof(ScoutStardewLocation_Postfix)
                            )
                        );
                    }

                    // Patch ScoutStardewLocations
                    var scoutManyMethod = AccessTools.Method(
                        targetType,
                        "ScoutStardewLocations",
                        new[] { typeof(IEnumerable<string>), typeof(bool) }
                    );
                    if (scoutManyMethod != null)
                    {
                        harmony.Patch(
                            scoutManyMethod,
                            prefix: new HarmonyMethod(
                                typeof(ScoutPatcher),
                                nameof(ScoutStardewLocations_Prefix)
                            ),
                            postfix: new HarmonyMethod(
                                typeof(ScoutPatcher),
                                nameof(ScoutStardewLocations_Postfix)
                            )
                        );
                    }

                    ModEntry.Instance.Monitor.Log(
                        "Successfully patched StardewArchipelagoClient scouting for caching!",
                        LogLevel.Info
                    );
                }
                else
                {
                    ModEntry.Instance.Monitor.Log(
                        "Could not find StardewArchipelagoClient type to patch scouting.",
                        LogLevel.Warn
                    );
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch StardewArchipelagoClient scouting: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _scoutedLocationsCache.Clear();
            }
            ModEntry.Instance.Monitor.Log("Scout cache cleared.", LogLevel.Info);
        }

        // Prefix for ScoutStardewLocation
        public static bool ScoutStardewLocation_Prefix(
            string locationName,
            bool createAsHint,
            ref ScoutedLocation __result
        )
        {
            try
            {
                if (createAsHint || string.IsNullOrWhiteSpace(locationName))
                {
                    return true;
                }

                lock (_cacheLock)
                {
                    if (_scoutedLocationsCache.TryGetValue(locationName, out var cached))
                    {
                        __result = cached;
                        return false; // skip original
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in ScoutStardewLocation_Prefix: {ex}",
                    LogLevel.Error
                );
            }
            return true;
        }

        // Postfix for ScoutStardewLocation
        public static void ScoutStardewLocation_Postfix(
            string locationName,
            bool createAsHint,
            ScoutedLocation __result
        )
        {
            try
            {
                if (__result == null || string.IsNullOrWhiteSpace(locationName))
                {
                    return;
                }

                lock (_cacheLock)
                {
                    _scoutedLocationsCache[locationName] = __result;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in ScoutStardewLocation_Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }

        // Prefix for ScoutStardewLocations
        public static bool ScoutStardewLocations_Prefix(
            ref IEnumerable<string> locationNames,
            bool createAsHint,
            ref Dictionary<string, ScoutedLocation> __result,
            out List<string>? __state
        )
        {
            __state = null;
            try
            {
                if (createAsHint || locationNames == null)
                {
                    return true;
                }

                var originalList = locationNames.ToList();
                var results = new Dictionary<string, ScoutedLocation>(
                    StringComparer.OrdinalIgnoreCase
                );
                var locationsToQuery = new List<string>();

                lock (_cacheLock)
                {
                    foreach (var name in originalList)
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            continue;
                        if (_scoutedLocationsCache.TryGetValue(name, out var cached))
                        {
                            results[name] = cached;
                        }
                        else
                        {
                            locationsToQuery.Add(name);
                        }
                    }
                }

                if (locationsToQuery.Count == 0)
                {
                    __result = results;
                    return false; // skip original as all are cached
                }

                // Rewrite parameters to only contain uncached items
                locationNames = locationsToQuery;
                __state = originalList;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in ScoutStardewLocations_Prefix: {ex}",
                    LogLevel.Error
                );
            }
            return true;
        }

        // Postfix for ScoutStardewLocations
        public static void ScoutStardewLocations_Postfix(
            List<string>? __state,
            bool createAsHint,
            ref Dictionary<string, ScoutedLocation> __result
        )
        {
            try
            {
                if (createAsHint || __state == null || __result == null)
                {
                    return;
                }

                // 1. Cache the newly queried results
                lock (_cacheLock)
                {
                    foreach (var pair in __result)
                    {
                        if (pair.Value != null)
                        {
                            _scoutedLocationsCache[pair.Key] = pair.Value;
                        }
                    }
                }

                // 2. Reconstruct the full dictionary representing the original request list
                var combined = new Dictionary<string, ScoutedLocation>(
                    StringComparer.OrdinalIgnoreCase
                );
                lock (_cacheLock)
                {
                    foreach (var name in __state)
                    {
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        if (_scoutedLocationsCache.TryGetValue(name, out var cached))
                        {
                            combined[name] = cached;
                        }
                    }
                }

                __result = combined;
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in ScoutStardewLocations_Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }
}
