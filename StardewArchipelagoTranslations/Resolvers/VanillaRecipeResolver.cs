using System;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class VanillaRecipeResolver : IItemResolver
    {
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

            if (TranslationHelper.RecipeExists(genericRecipeName, false))
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

            if (TranslationHelper.RecipeExists(genericRecipeName, true))
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
    }
}
