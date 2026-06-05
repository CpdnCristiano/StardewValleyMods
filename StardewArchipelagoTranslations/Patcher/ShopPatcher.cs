using System;
using System.Collections.Generic;
using HarmonyLib;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Buildings;
using StardewValley.Menus;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher
{
    public static class ShopPatcher
    {
        private const string BLUEPRINT_SUFFIX = " Blueprint";

        public static void Patch(Harmony harmony)
        {
            try
            {
                var targetType = AccessTools.TypeByName(
                    "StardewArchipelago.Locations.InGameLocations.ObtainableArchipelagoLocation"
                );
                if (targetType != null)
                {
                    // Patch DisplayName getter
                    var displayNameGetter = AccessTools.PropertyGetter(targetType, "DisplayName");
                    if (displayNameGetter != null)
                    {
                        harmony.Patch(
                            displayNameGetter,
                            postfix: new HarmonyMethod(
                                typeof(ShopPatcher),
                                nameof(DisplayName_Postfix)
                            )
                        );
                    }

                    // Patch getDescription method
                    var getDescriptionMethod = AccessTools.Method(targetType, "getDescription");
                    if (getDescriptionMethod != null)
                    {
                        harmony.Patch(
                            getDescriptionMethod,
                            postfix: new HarmonyMethod(
                                typeof(ShopPatcher),
                                nameof(GetDescription_Postfix)
                            )
                        );
                    }

                    /*// Patch drawInMenu on ObtainableArchipelagoLocation to use vanilla building sprite
                    var drawInMenuMethod = AccessTools.Method(
                        targetType,
                        "drawInMenu",
                        new[]
                        {
                            typeof(SpriteBatch),
                            typeof(Vector2),
                            typeof(float),
                            typeof(float),
                            typeof(float),
                            typeof(StackDrawType),
                            typeof(Color),
                            typeof(bool),
                        }
                    );
                    if (drawInMenuMethod != null)
                    {
                        harmony.Patch(
                            drawInMenuMethod,
                            prefix: new HarmonyMethod(typeof(ShopPatcher), nameof(DrawInMenu_VanillaIcon_Prefix))
                        );
                    }*/

                    ModEntry.Instance.Monitor.Log(
                        "Successfully patched ObtainableArchipelagoLocation for shop translations!",
                        LogLevel.Info
                    );
                }
                else
                {
                    ModEntry.Instance.Monitor.Log(
                        "Could not find ObtainableArchipelagoLocation type in StardewArchipelago assembly.",
                        LogLevel.Warn
                    );
                }

                // Patch IClickableMenu.drawToolTip to support material icons
                var drawToolTipMethod = AccessTools.Method(
                    typeof(IClickableMenu),
                    nameof(IClickableMenu.drawToolTip)
                );
                if (drawToolTipMethod != null)
                {
                    var parameters = drawToolTipMethod.GetParameters();
                    var paramNames = string.Join(
                        ", ",
                        parameters.Select(p => $"{p.ParameterType.Name} {p.Name}")
                    );
                    ModEntry.Instance.Monitor.Log(
                        $"IClickableMenu.drawToolTip parameters: {paramNames}",
                        LogLevel.Info
                    );

                    harmony.Patch(
                        drawToolTipMethod,
                        prefix: new HarmonyMethod(typeof(ShopPatcher), nameof(DrawToolTip_Prefix))
                    );
                    ModEntry.Instance.Monitor.Log(
                        "Successfully patched IClickableMenu.drawToolTip for blueprint ingredient icons!",
                        LogLevel.Info
                    );
                }

                // Translate JunimoShopStockModifier dialogues
                var junimoStockModifierType = AccessTools.TypeByName(
                    "StardewArchipelago.GameModifications.Modded.JunimoShopStockModifier"
                );
                if (junimoStockModifierType != null)
                {
                    var junimoPhraseField = AccessTools.Field(
                        junimoStockModifierType,
                        "_junimoPhrase"
                    );
                    if (junimoPhraseField != null)
                    {
                        var dict = junimoPhraseField.GetValue(null) as Dictionary<string, string>;
                        if (dict != null)
                        {
                            dict["Orange"] = ModEntry.Translation.Get("junimo.orange").ToString();
                            dict["Red"] = ModEntry.Translation.Get("junimo.red").ToString();
                            dict["Grey"] = ModEntry.Translation.Get("junimo.grey").ToString();
                            dict["Yellow"] = ModEntry.Translation.Get("junimo.yellow").ToString();
                            dict["Blue"] = ModEntry.Translation.Get("junimo.blue").ToString();
                            dict["Purple"] = ModEntry.Translation.Get("junimo.purple").ToString();
                            ModEntry.Instance.Monitor.Log(
                                "Successfully translated SVE Junimo Shop stock dialogues!",
                                LogLevel.Info
                            );
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Failed to patch Shop/ToolTip/Junimo: {ex}",
                    LogLevel.Error
                );
            }
        }

        /// <summary>
        /// Tries to extract the building name from a location name.
        /// e.g. "Stable Blueprint" → "Stable"
        /// </summary>
        private static bool TryGetBlueprintBuildingName(Item hoveredItem, out string? buildingName)
        {
            buildingName = null;
            try
            {
                // LocationName is the raw AP location string (e.g. "Stable Blueprint")
                var locationNameProp = hoveredItem.GetType().GetProperty("LocationName");
                if (locationNameProp == null)
                    return false;

                var locationName = locationNameProp.GetValue(hoveredItem) as string;
                if (string.IsNullOrWhiteSpace(locationName))
                    return false;

                if (!locationName.EndsWith(BLUEPRINT_SUFFIX, StringComparison.OrdinalIgnoreCase))
                    return false;

                buildingName = locationName
                    .Substring(0, locationName.Length - BLUEPRINT_SUFFIX.Length)
                    .Trim();
                return !string.IsNullOrWhiteSpace(buildingName);
            }
            catch
            {
                return false;
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // Prefix for IClickableMenu.drawToolTip
        // Injects building materials as CraftingRecipe so the game renders
        // ingredient icons with coloured text (red = missing, green = have).
        // ──────────────────────────────────────────────────────────────────
        public static void DrawToolTip_Prefix(
            ref string hoverText,
            Item hoveredItem,
            ref CraftingRecipe craftingIngredients
        )
        {
            try
            {
                if (hoveredItem == null)
                    return;

                if (!string.IsNullOrWhiteSpace(hoverText))
                {
                    hoverText = TranslationHelper.TranslateDescription(hoverText);
                }

                // --- Materials path: Materials / ArchipelagoMaterials / Extra Materials in modData ---
                string? materialsJson = null;
                bool hasMaterials = false;
                if (hoveredItem.modData != null)
                {
                    if (
                        hoveredItem.modData.TryGetValue("Materials", out materialsJson)
                        || hoveredItem.modData.TryGetValue(
                            "ArchipelagoMaterials",
                            out materialsJson
                        )
                        || hoveredItem.modData.TryGetValue("Extra Materials", out materialsJson)
                    )
                    {
                        hasMaterials = !string.IsNullOrWhiteSpace(materialsJson);
                    }
                }

                if (hasMaterials && materialsJson != null)
                {
                    Dictionary<string, int>? materials = null;
                    var trimmed = materialsJson.Trim();
                    if (trimmed.StartsWith("{"))
                    {
                        try
                        {
                            materials = System.Text.Json.JsonSerializer.Deserialize<
                                Dictionary<string, int>
                            >(materialsJson);
                        }
                        catch
                        {
                            materials = null;
                        }
                    }
                    else
                    {
                        materials = new Dictionary<string, int>();
                        foreach (
                            var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries)
                        )
                        {
                            var subparts = part.Split(':');
                            if (subparts.Length == 2 && int.TryParse(subparts[1], out var amt))
                            {
                                var itemId = subparts[0].Trim();
                                materials[itemId] = amt;
                            }
                        }
                    }

                    if (materials != null && materials.Count > 0)
                    {
                        var recipe = new CraftingRecipe("Torch");
                        recipe.recipeList.Clear();
                        foreach (var pair in materials)
                        {
                            var itemId = pair.Key;
                            if (!string.IsNullOrWhiteSpace(itemId))
                            {
                                // Qualify ID if it's unqualified numeric
                                if (!itemId.StartsWith("(") && int.TryParse(itemId, out _))
                                {
                                    itemId = "(O)" + itemId;
                                }
                                recipe.recipeList[itemId] = pair.Value;
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(hoverText))
                        {
                            var lines = hoverText.Split(
                                new[] { "\r\n", "\n" },
                                StringSplitOptions.None
                            );
                            var cleanLines = new List<string>();
                            foreach (var line in lines)
                            {
                                var trimmedLine = line.Trim();
                                if (
                                    System.Text.RegularExpressions.Regex.IsMatch(
                                        trimmedLine,
                                        @"^\d+\s+"
                                    )
                                )
                                {
                                    continue;
                                }
                                cleanLines.Add(line);
                            }
                            hoverText = string.Join(Environment.NewLine, cleanLines).TrimEnd();
                        }

                        recipe.description = hoverText;
                        craftingIngredients = recipe;
                        hoverText = " ";
                    }
                }
            }
            catch
            {
                // Silently ignore or trace on failure
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // Prefix for ObtainableArchipelagoLocation.drawInMenu
        // When the item is a Blueprint, draw the vanilla building thumbnail
        // instead of the Archipelago coloured-package icon.
        // ──────────────────────────────────────────────────────────────────
        public static bool DrawInMenu_VanillaIcon_Prefix(
            Item __instance,
            SpriteBatch spriteBatch,
            Vector2 location,
            float scaleSize,
            float transparency,
            float layerDepth,
            StackDrawType drawStackNumber,
            Color color,
            bool drawShadow
        )
        {
            try
            {
                if (!TryGetBlueprintBuildingName(__instance, out var buildingKey))
                    return true; // let original draw the AP icon

                var dataKey = buildingKey;
                if (!Game1.buildingData.ContainsKey(dataKey))
                {
                    if (
                        buildingKey != null
                        && buildingKey.StartsWith("Free ", StringComparison.OrdinalIgnoreCase)
                    )
                        dataKey = buildingKey.Substring(5).Trim();
                }

                if (!Game1.buildingData.TryGetValue(dataKey, out var buildingData))
                    return true;

                // Load the building's paint texture (thumbnail)
                Texture2D? buildingTex = null;
                try
                {
                    buildingTex = Game1.content.Load<Texture2D>(buildingData.Texture);
                }
                catch { }

                if (buildingTex == null)
                    return true; // fall back to AP icon

                // Source rect: first 64×64 of the spritesheet (the building thumbnail)
                // Buildings usually have their front-facing sprite at x=0, y=0
                var sourceRect = new Rectangle(
                    0,
                    0,
                    Math.Min(buildingTex.Width, 64),
                    Math.Min(buildingTex.Height, 64)
                );

                // Draw centred in the 64×64 menu slot
                var drawPos = location + new Vector2(32f, 32f);
                var scale = scaleSize * (64f / Math.Max(sourceRect.Width, sourceRect.Height));
                var origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);

                spriteBatch.Draw(
                    buildingTex,
                    drawPos,
                    sourceRect,
                    color * transparency,
                    0f,
                    origin,
                    scale,
                    SpriteEffects.None,
                    layerDepth
                );

                return false; // skip original AP icon draw
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in DrawInMenu_VanillaIcon_Prefix: {ex}",
                    LogLevel.Warn
                );
                return true; // fall back to original on error
            }
        }

        // ──────────────────────────────────────────────────────────────────
        // Postfixes for name / description translation
        // ──────────────────────────────────────────────────────────────────
        public static void DisplayName_Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(__result))
                    return;

                __result = TranslationHelper.GetLocalizedLocationName(__result);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in DisplayName_Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }

        public static void GetDescription_Postfix(ref string __result)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(__result))
                    return;

                __result = TranslationHelper.TranslateDescription(__result);
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error in GetDescription_Postfix: {ex}",
                    LogLevel.Error
                );
            }
        }
    }
}
