using System;
using System.Collections.Generic;
using System.Text;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;
using SObject = StardewValley.Object;


using StardewValley.GameData.Locations;

namespace CpdnCristiano.StardewValleyMod.FixWarpGreenhouses.Patcher
{
    internal class LocationRequestPatcher : BasePatcher
    {
        /*********
        ** Fields
        *********/

        /// <summary>Stored in <see cref="Apply"/> so the static prefix can write to the SMAPI console.</summary>
        private static IMonitor? Monitor;

        /// <summary>
        /// All sub-locations of any non-vanilla greenhouse.
        /// </summary>
        public static readonly HashSet<string> AllGreenhouseSubLocations = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Instanced sub-locations mapped by (originalName, rootId).
        /// </summary>
        public static readonly Dictionary<(string originalName, string rootId), GameLocation> InstancedLocations = new();

        /// <summary>
        /// Flag to temporarily bypass redirection during BFS graph reconstruction.
        /// </summary>
        public static bool IsRebuildingGraph = false;

        /// <summary>
        /// Flag to temporarily bypass redirection during base map resolution to avoid infinite recursion.
        /// </summary>
        public static bool IsResolvingBaseMap = false;

        /*********
        ** Public methods
        *********/

        /// <inheritdoc />
        public override void Apply(Harmony harmony, IMonitor monitor)
        {
            Monitor = monitor;

            harmony.Patch(
                original: this.RequireMethod<Game1>(nameof(Game1.getLocationRequest)),
                prefix: this.GetHarmonyMethod(nameof(getLocationRequest_Prefix))
            );

            // Patch Game1.getLocationFromName to intercept warp target resolution
            harmony.Patch(
                original: AccessTools.Method(typeof(Game1), nameof(Game1.getLocationFromName), new Type[] { typeof(string), typeof(bool) }),
                prefix: this.GetHarmonyMethod(nameof(getLocationFromName_Prefix))
            );

            // Patch GameLocation.GetData to dynamically enable AlwaysActive
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.GetData)),
                postfix: this.GetHarmonyMethod(nameof(GetData_Postfix))
            );

            // Patch GameLocation.loadMap to redirect map file path loading
            harmony.Patch(
                original: AccessTools.Method(typeof(GameLocation), nameof(GameLocation.loadMap), new Type[] { typeof(string), typeof(bool) }),
                prefix: this.GetHarmonyMethod(nameof(loadMap_Prefix))
            );

            // Patch SaveGame.loadDataToLocations to pre-create instanced locations from save data
            harmony.Patch(
                original: AccessTools.Method(typeof(SaveGame), "loadDataToLocations"),
                prefix: this.GetHarmonyMethod(nameof(loadDataToLocations_Prefix))
            );
        }

        /// <summary>
        /// Intercepts SaveGame.loadDataToLocations to pre-create instanced locations from save data.
        /// This ensures that Stardew Valley's deserializer finds the locations in Game1.locations
        /// and does not discard them as "unknown locations".
        /// </summary>
        public static void loadDataToLocations_Prefix(List<GameLocation> fromLocations)
        {
            if (fromLocations == null)
                return;

            foreach (GameLocation loc in fromLocations)
            {
                if (loc == null)
                    continue;

                string locName = loc.NameOrUniqueName;
                if (string.IsNullOrWhiteSpace(locName))
                    continue;

                int index = locName.LastIndexOf("_Greenhouse", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    string originalName = locName.Substring(0, index);
                    string rootId = locName.Substring(index + 1);

                    // Só pré-cria a localização se o mapa base original ainda existir nos dados do jogo (ou seja, o mod está instalado)
                    if (Game1.locationData != null && Game1.locationData.ContainsKey(originalName))
                    {
                        Monitor?.Log($"[FixWarpGreenhouses] loadDataToLocations_Prefix: Pre-creating instanced location \"{locName}\" found in save data.", LogLevel.Info);
                        GetOrCreateInstance(originalName, rootId, Monitor);
                    }
                    else
                    {
                        Monitor?.Log($"[FixWarpGreenhouses] loadDataToLocations_Prefix: Skipping pre-creation of \"{locName}\" because the base mod is not installed.", LogLevel.Info);
                    }
                }
            }
        }

        /// <summary>
        /// Dynamic postfix for GetData to return the correct base LocationData for instanced locations,
        /// and enforce AlwaysActive = true for any greenhouse sub-location so building and Automate works.
        /// </summary>
        public static void GetData_Postfix(GameLocation __instance, ref LocationData __result)
        {
            string locName = __instance.NameOrUniqueName;
            int index = locName.LastIndexOf("_Greenhouse", StringComparison.OrdinalIgnoreCase);

            if (__result == null && index != -1)
            {
                string originalName = locName.Substring(0, index);
                if (Game1.locationData != null && Game1.locationData.TryGetValue(originalName, out var baseData))
                {
                    __result = baseData;
                }
            }

            if (__result != null)
            {
                string baseName = index != -1 ? locName.Substring(0, index) : locName;
                if (AllGreenhouseSubLocations.Contains(baseName))
                {
                    if (__result.CreateOnLoad != null)
                    {
                        __result.CreateOnLoad.AlwaysActive = true;
                    }
                }
            }
        }

        /// <summary>
        /// Intercepts loadMap to redirect instanced map file loading to the original base map file.
        /// </summary>
        public static void loadMap_Prefix(ref string mapPath)
        {
            if (string.IsNullOrWhiteSpace(mapPath))
                return;

            int index = mapPath.LastIndexOf("_Greenhouse", StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                string originalPath = mapPath.Substring(0, index);
                Monitor?.Log($"[FixWarpGreenhouses] loadMap INTERCEPTED \"{mapPath}\". Redirecting map file load to \"{originalPath}\".", LogLevel.Trace);
                mapPath = originalPath;
            }
        }

        /// <summary>
        /// Resolves the active greenhouse root ID based solely on the current location name.
        /// </summary>
        public static string? GetActiveRoot(string currentLocationName)
        {
            if (string.IsNullOrWhiteSpace(currentLocationName))
                return null;

            GameLocation? loc = null;
            bool wasResolving = IsResolvingBaseMap;
            try
            {
                IsResolvingBaseMap = true;
                loc = Game1.getLocationFromName(currentLocationName);
            }
            finally
            {
                IsResolvingBaseMap = wasResolving;
            }

            if (IsNonVanillaGreenhouse(loc))
            {
                return currentLocationName;
            }

            int index = currentLocationName.LastIndexOf("_Greenhouse", StringComparison.OrdinalIgnoreCase);
            if (index != -1)
            {
                return currentLocationName.Substring(index + 1);
            }

            return null;
        }

        /// <summary>
        /// Retrieves or creates a physical clone of the base sub-location for the given root greenhouse.
        /// </summary>
        public static GameLocation? GetOrCreateInstance(string originalName, string rootId, IMonitor? monitor)
        {
            var key = (originalName, rootId);
            if (InstancedLocations.TryGetValue(key, out var existing))
                return existing;

            string instanceName = $"{originalName}_{rootId}";
            
            GameLocation? existingInGame = null;
            bool wasResolving = IsResolvingBaseMap;
            try
            {
                IsResolvingBaseMap = true;
                existingInGame = Game1.getLocationFromName(instanceName);
            }
            finally
            {
                IsResolvingBaseMap = wasResolving;
            }

            if (existingInGame != null)
            {
                InstancedLocations[key] = existingInGame;
                return existingInGame;
            }

            // Instanciar usando o construtor vazio para evitar o carregamento imediato do mapa.
            // O mapa será carregado pelo jogo na thread principal quando for necessário,
            // garantindo compatibilidade com o Content Patcher que ainda não está pronto durante a deserialização em background.
            var instance = new GameLocation();
            instance.mapPath.Value = "Maps/" + originalName;
#pragma warning disable AvoidNetField
            instance.name.Value = instanceName;
#pragma warning restore AvoidNetField
            InstancedLocations[key] = instance;
            Game1.locations.Add(instance);

            monitor?.Log($"[FixWarpGreenhouses] Created instance \"{instanceName}\" for root greenhouse \"{rootId}\" (deferred map load).", LogLevel.Info);
            return instance;
        }

        /// <summary>
        /// Intercepts getLocationFromName to resolve instanced location names for custom greenhouses on the fly.
        /// </summary>
        public static void getLocationFromName_Prefix(ref string name)
        {
            if (IsRebuildingGraph || IsResolvingBaseMap)
                return;

            if (string.IsNullOrWhiteSpace(name))
                return;

            if (name.Contains("_Greenhouse", StringComparison.OrdinalIgnoreCase))
                return;

            if (AllGreenhouseSubLocations.Contains(name))
            {
                string currentLocation = Game1.currentLocation?.NameOrUniqueName ?? "";
                string? activeRoot = GetActiveRoot(currentLocation);
                if (activeRoot != null && IsValidRedirect(activeRoot))
                {
                    Monitor?.Log($"[FixWarpGreenhouses] getLocationFromName INTERCEPTED \"{name}\". Redirecting to clone with root \"{activeRoot}\".", LogLevel.Trace);
                    GetOrCreateInstance(name, activeRoot, Monitor);
                    name = $"{name}_{activeRoot}";
                }
            }
        }

        /*********
        ** Harmony patches
        *********/

        /// <summary>
        /// Intercepts warp requests targeting the vanilla "Greenhouse" and redirects
        /// them to the non-vanilla greenhouse the player is currently associated with.
        /// </summary>
        public static void getLocationRequest_Prefix(ref string locationName)
        {
            if (string.Equals(locationName, "Greenhouse", StringComparison.OrdinalIgnoreCase))
            {
                string currentLocation = Game1.currentLocation?.NameOrUniqueName ?? "Farm";

                // Lógica exata solicitada pelo usuário (quando o jogador já está no mapa raiz da estufa customizada)
                if (
                    currentLocation.StartsWith("Greenhouse", StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(
                        currentLocation,
                        locationName,
                        StringComparison.OrdinalIgnoreCase
                    )
                )
                {
                    locationName = currentLocation;
                    return;
                }

                // Lógica complementar para saída de sub-áreas clonadas (terminando com _GreenhouseRootID)
                int index = currentLocation.LastIndexOf("_Greenhouse", StringComparison.OrdinalIgnoreCase);
                if (index != -1)
                {
                    string rootId = currentLocation.Substring(index + 1);
                    if (!string.Equals(rootId, locationName, StringComparison.OrdinalIgnoreCase))
                    {
                        locationName = rootId;
                    }
                }
            }
        }

        /*********
        ** Public helpers (also used by ModEntry)
        *********/

        /// <summary>
        /// Safely iterates over all locations in the game, recursively including building interiors.
        /// </summary>
        public static IEnumerable<GameLocation> GetAllLocations()
        {
            var visited = new HashSet<GameLocation>();
            var queue = new Queue<GameLocation>();

            foreach (GameLocation location in Game1.locations)
            {
                if (location != null && visited.Add(location))
                {
                    queue.Enqueue(location);
                }
            }

            while (queue.Count > 0)
            {
                GameLocation current = queue.Dequeue();
                yield return current;

                if (current.buildings != null)
                {
                    foreach (var building in current.buildings)
                    {
                        GameLocation? indoors = building.GetIndoors();
                        if (indoors != null && visited.Add(indoors))
                        {
                            queue.Enqueue(indoors);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Returns whether <paramref name="loc"/> is a non-vanilla greenhouse.
        /// Uses the game's <c>isGreenhouse</c> flag (set by any proper greenhouse-type
        /// location regardless of mod or name) and falls back to the "Greenhouse" name
        /// prefix for mods that follow naming conventions but do not set the flag.
        /// </summary>
        public static bool IsNonVanillaGreenhouse(GameLocation? loc)
        {
            if (loc is null) return false;

            string name = loc.NameOrUniqueName;

            // Always exclude the vanilla Greenhouse.
            if (string.Equals(name, "Greenhouse", StringComparison.OrdinalIgnoreCase))
                return false;

            // Root greenhouses created by mods always start with "Greenhouse" (e.g. "Greenhouseda7e6c6b-...")
            if (name.StartsWith("Greenhouse", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /*********
        ** Private helpers
        *********/

        /// <summary>Returns true if <paramref name="name"/> is a meaningful redirect target.</summary>
        private static bool IsValidRedirect(string? name) =>
            !string.IsNullOrWhiteSpace(name)
            && !string.Equals(name, "Greenhouse", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(name, "Farm", StringComparison.OrdinalIgnoreCase);
    }
}
