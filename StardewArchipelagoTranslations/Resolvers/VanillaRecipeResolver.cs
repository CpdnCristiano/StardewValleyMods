using System;
using System.Collections.Generic;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class VanillaRecipeResolver : IItemResolver
    {
        private static readonly Dictionary<string, string> _recipeNameCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly object _recipeCacheLock = new();

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var genericRecipeName = englishName;
            var genericRecipeSuffix = false;
            if (
                genericRecipeName.EndsWith(" Recipe", StringComparison.OrdinalIgnoreCase)
                || genericRecipeName.EndsWith(" recipe", StringComparison.OrdinalIgnoreCase)
            )
            {
                genericRecipeName = genericRecipeName
                    .Substring(0, genericRecipeName.Length - 7)
                    .Trim();
                genericRecipeSuffix = true;
            }

            if (RecipeExists(genericRecipeName, false))
            {
                try
                {
                    var recipeObj = new CraftingRecipe(genericRecipeName, false);
                    if (!string.IsNullOrWhiteSpace(recipeObj.DisplayName))
                    {
                        localizedName = genericRecipeSuffix
                            ? ModEntry
                                .Translation.Get(
                                    "hints.recipe_format",
                                    new { name = recipeObj.DisplayName }
                                )
                                .ToString()
                            : recipeObj.DisplayName;
                        return true;
                    }
                }
                catch { }
            }

            if (RecipeExists(genericRecipeName, true))
            {
                try
                {
                    var recipeObj = new CraftingRecipe(genericRecipeName, true);
                    if (!string.IsNullOrWhiteSpace(recipeObj.DisplayName))
                    {
                        localizedName = genericRecipeSuffix
                            ? ModEntry
                                .Translation.Get(
                                    "hints.recipe_format",
                                    new { name = recipeObj.DisplayName }
                                )
                                .ToString()
                            : recipeObj.DisplayName;
                        return true;
                    }
                }
                catch { }
            }

            return false;
        }

        internal static bool RecipeExists(string recipeName, bool isCooking)
        {
            try
            {
                var recipes = isCooking
                    ? DataLoader.CookingRecipes(Game1.content)
                    : DataLoader.CraftingRecipes(Game1.content);
                return recipes != null && recipes.ContainsKey(recipeName);
            }
            catch
            {
                return false;
            }
        }

        internal static bool TryGetLocalizedRecipeName(string recipeName, out string localizedName)
        {
            lock (_recipeCacheLock)
            {
                if (_recipeNameCache.TryGetValue(recipeName, out localizedName!))
                {
                    return true;
                }
            }

            localizedName = recipeName;
            try
            {
                string? foundName = null;

                var cookingRecipes = DataLoader.CookingRecipes(Game1.content);
                if (cookingRecipes != null && cookingRecipes.ContainsKey(recipeName))
                {
                    var recipe = new CraftingRecipe(recipeName, isCookingRecipe: true);
                    foundName = recipe.DisplayName;
                }
                else
                {
                    var craftingRecipes = DataLoader.CraftingRecipes(Game1.content);
                    if (craftingRecipes != null && craftingRecipes.ContainsKey(recipeName))
                    {
                        var recipe = new CraftingRecipe(recipeName, isCookingRecipe: false);
                        foundName = recipe.DisplayName;
                    }
                }

                if (foundName != null)
                {
                    localizedName = foundName;
                    lock (_recipeCacheLock)
                    {
                        _recipeNameCache[recipeName] = localizedName;
                    }
                    return true;
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[VanillaRecipeResolver] Error resolving recipe '{recipeName}': {ex.Message}",
                    LogLevel.Trace
                );
            }

            return false;
        }

        internal static void ClearCache()
        {
            lock (_recipeCacheLock)
            {
                _recipeNameCache.Clear();
            }
        }
    }
}
