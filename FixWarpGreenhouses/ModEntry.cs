using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using CpdnCristiano.StardewValleyMod.Common.Patching;
using CpdnCristiano.StardewValleyMod.FixWarpGreenhouses.Patcher;
using StardewModdingAPI;
using StardewModdingAPI.Enums;
using StardewModdingAPI.Events;
using StardewValley;
using SObject = StardewValley.Object;

namespace CpdnCristiano.StardewValleyMod.FixWarpGreenhouses;

public class ModEntry : Mod
{
    public override void Entry(IModHelper helper)
    {
        this.Monitor.Log($"[FixWarpGreenhouses] Mod version: {this.ModManifest.Version}", LogLevel.Info);

        HarmonyPatcher.Apply(this, new LocationRequestPatcher());

        // Build the static BFS graph when a save is loaded or a new day begins.
        helper.Events.GameLoop.SaveLoaded += (_, _) => this.RebuildOwnershipGraph();
        helper.Events.GameLoop.DayStarted += (_, _) => this.RebuildOwnershipGraph();

        // Clear all state when returning to the title screen.
        helper.Events.GameLoop.ReturnedToTitle += (_, _) => this.ClearState();
    }

    /*********
    ** Private methods
    *********/

    /// <summary>
    /// Builds the static BFS-based ownership map used to identify greenhouse sub-locations.
    ///
    /// For each greenhouse (including vanilla and non-vanilla), performs a BFS through the warp graph
    /// (stopping at outdoor locations and other greenhouses) to find all reachable interior sub-locations.
    /// These are recorded in AllGreenhouseSubLocations.
    /// </summary>
    internal void RebuildOwnershipGraph()
    {
        try
        {
            LocationRequestPatcher.IsRebuildingGraph = true;

            LocationRequestPatcher.AllGreenhouseSubLocations.Clear();
            // Preserve PlayerGreenhouseRoot and LocationInstanceOwner across rebuilds
            // (they reflect live player state and should not be reset mid-session).

            // ── Step 1: Build bidirectional warp adjacency list ───────────────────────
            var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

             void AddEdge(string a, string b)
            {
                if (!adjacency.TryGetValue(a, out var sA))
                    adjacency[a] = sA = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!adjacency.TryGetValue(b, out var sB))
                    adjacency[b] = sB = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                sA.Add(b);
                sB.Add(a);
            }

            void ParseAndAddActionWarp(string currentLocName, string propertyValue)
            {
                if (string.IsNullOrWhiteSpace(propertyValue)) return;
                string[] parts = propertyValue.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2)
                {
                    string verb = parts[0];
                    if (string.Equals(verb, "Warp", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(verb, "MagicWarp", StringComparison.OrdinalIgnoreCase))
                    {
                        string target = parts[1];
                        if (!int.TryParse(target, out _))
                        {
                            AddEdge(currentLocName, target);
                        }
                    }
                }
            }

            foreach (GameLocation loc in LocationRequestPatcher.GetAllLocations())
            {
                if (loc.Map == null)
                {
                    try
                    {
                        loc.reloadMap();
                    }
                    catch (Exception ex)
                    {
                        this.Monitor.Log($"[FixWarpGreenhouses] Failed to reload map for \"{loc.NameOrUniqueName}\": {ex.Message}", LogLevel.Trace);
                    }
                }

                if (loc.Map == null)
                    continue;

                foreach (Warp warp in loc.warps)
                    AddEdge(loc.NameOrUniqueName, warp.TargetName);

                // Scan map-level Warp property (just in case)
                if (loc.Map?.Properties != null)
                {
                    if (loc.Map.Properties.TryGetValue("Warp", out var mapWarpVal) && mapWarpVal != null)
                    {
                        string[] parts = mapWarpVal.ToString().Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                        for (int i = 2; i < parts.Length; i += 5)
                        {
                            if (i < parts.Length)
                            {
                                AddEdge(loc.NameOrUniqueName, parts[i]);
                            }
                        }
                    }
                }

                // Scan tile-level properties for action warps
                if (loc.Map != null)
                {
                    foreach (var layer in loc.Map.Layers)
                    {
                        if (layer?.Tiles == null) continue;
                        for (int x = 0; x < layer.LayerWidth; x++)
                        {
                            for (int y = 0; y < layer.LayerHeight; y++)
                            {
                                var tile = layer.Tiles[x, y];
                                if (tile == null) continue;

                                if (tile.Properties.TryGetValue("Action", out var actionVal) && actionVal != null)
                                {
                                    ParseAndAddActionWarp(loc.NameOrUniqueName, actionVal.ToString());
                                }
                                if (tile.Properties.TryGetValue("TouchAction", out var touchVal) && touchVal != null)
                                {
                                    ParseAndAddActionWarp(loc.NameOrUniqueName, touchVal.ToString());
                                }
                            }
                        }
                    }
                }
            }

            // ── Step 2: Identify non-vanilla greenhouses and the vanilla Greenhouse ──────────
            var nonVanillaGreenhouses = LocationRequestPatcher.GetAllLocations()
                .Where(LocationRequestPatcher.IsNonVanillaGreenhouse)
                .ToList();

            var allGreenhousesForBFS = LocationRequestPatcher.GetAllLocations()
                .Where(l => string.Equals(l.NameOrUniqueName, "Greenhouse", StringComparison.OrdinalIgnoreCase) || LocationRequestPatcher.IsNonVanillaGreenhouse(l))
                .ToList();

            this.Monitor.Log(
                $"[FixWarpGreenhouses] Rebuilding BFS graph. Non-vanilla greenhouse(s): {nonVanillaGreenhouses.Count}. BFS starting points: {allGreenhousesForBFS.Count}.",
                LogLevel.Debug
            );

            foreach (GameLocation gh in allGreenhousesForBFS)
                this.Monitor.Log($"[FixWarpGreenhouses]   · \"{gh.NameOrUniqueName}\" (isGreenhouse={gh.isGreenhouse.Value})", LogLevel.Trace);

            if (allGreenhousesForBFS.Count == 0)
                return;

            // ── Step 3: BFS from each greenhouse — count claims per sub-location ──────
            var claimCount = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var claimOwner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (GameLocation greenhouse in allGreenhousesForBFS)
            {
                string ghName = greenhouse.NameOrUniqueName;
                var    visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ghName };
                var    queue   = new Queue<string>();
                queue.Enqueue(ghName);

                while (queue.Count > 0)
                {
                    string current = queue.Dequeue();

                    if (!adjacency.TryGetValue(current, out var neighbors))
                        continue;

                    foreach (string neighbor in neighbors)
                    {
                        if (visited.Contains(neighbor))
                            continue;
                        visited.Add(neighbor);

                        // Stop at outdoor locations — they are not sub-areas.
                        GameLocation? neighborLoc = Game1.getLocationFromName(neighbor)
                            ?? LocationRequestPatcher.GetAllLocations().FirstOrDefault(l => string.Equals(l.NameOrUniqueName, neighbor, StringComparison.OrdinalIgnoreCase));
                        if (neighborLoc?.IsOutdoors == true)
                            continue;

                        // Stop if the neighbor is a greenhouse itself (roots cannot be sub-locations)
                        if (string.Equals(neighbor, "Greenhouse", StringComparison.OrdinalIgnoreCase) || LocationRequestPatcher.IsNonVanillaGreenhouse(neighborLoc))
                            continue;

                        LocationRequestPatcher.AllGreenhouseSubLocations.Add(neighbor);

                        claimCount[neighbor] = claimCount.GetValueOrDefault(neighbor) + 1;
                        claimOwner[neighbor] = ghName;
                        queue.Enqueue(neighbor);
                    }
                }
            }

            this.Monitor.Log(
                $"[FixWarpGreenhouses] BFS graph built — {LocationRequestPatcher.AllGreenhouseSubLocations.Count} greenhouse sub-locations discovered.",
                LogLevel.Debug
            );
        }
        finally
        {
            LocationRequestPatcher.IsRebuildingGraph = false;
        }
    }

    /// <summary>Clears all tracked state when returning to the title screen.</summary>
    private void ClearState()
    {
        LocationRequestPatcher.AllGreenhouseSubLocations.Clear();
        LocationRequestPatcher.InstancedLocations.Clear();
        this.Monitor.Log("[FixWarpGreenhouses] All state cleared (returned to title).", LogLevel.Debug);
    }
}
