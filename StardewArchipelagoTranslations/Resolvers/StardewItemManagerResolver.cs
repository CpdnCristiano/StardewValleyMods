using System;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class StardewItemManagerResolver : IItemResolver
    {
        private static System.Reflection.FieldInfo? _itemManagerField;

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            try
            {
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance != null)
                {
                    _itemManagerField ??= typeof(StardewArchipelago.ModEntry).GetField(
                        "_stardewItemManager",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                    if (_itemManagerField != null)
                    {
                        var stardewItemManager =
                            _itemManagerField.GetValue(saInstance)
                            as StardewArchipelago.Stardew.StardewItemManager;
                        if (stardewItemManager != null)
                        {
                            if (stardewItemManager.ItemExists(englishName))
                            {
                                var stardewItem = stardewItemManager.GetItemByName(englishName);
                                if (stardewItem != null)
                                {
                                    var qualifiedId = stardewItem.GetQualifiedId();
                                    var data = ItemRegistry.GetData(qualifiedId);
                                    if (
                                        data != null
                                        && !string.IsNullOrWhiteSpace(data.DisplayName)
                                    )
                                    {
                                        localizedName = data.DisplayName;
                                        return true;
                                    }
                                }
                            }

                            var recipeCleanName = englishName;
                            var hasRecipeSuffix = false;
                            if (
                                recipeCleanName.EndsWith(
                                    " Recipe",
                                    StringComparison.OrdinalIgnoreCase
                                )
                                || recipeCleanName.EndsWith(
                                    " recipe",
                                    StringComparison.OrdinalIgnoreCase
                                )
                            )
                            {
                                recipeCleanName = recipeCleanName
                                    .Substring(0, recipeCleanName.Length - 7)
                                    .Trim();
                                hasRecipeSuffix = true;
                            }

                            var stardewRecipe = stardewItemManager.GetRecipeByName(
                                recipeCleanName,
                                false
                            );
                            if (stardewRecipe != null)
                            {
                                var isCooking =
                                    stardewRecipe
                                    is StardewArchipelago.Stardew.StardewCookingRecipe;
                                if (VanillaRecipeResolver.RecipeExists(recipeCleanName, isCooking))
                                {
                                    var nativeRecipe = new CraftingRecipe(
                                        recipeCleanName,
                                        isCooking
                                    );
                                    if (!string.IsNullOrWhiteSpace(nativeRecipe.DisplayName))
                                    {
                                        localizedName = hasRecipeSuffix
                                            ? ModEntry
                                                .Translation.Get(
                                                    "hints.recipe_format",
                                                    new { name = nativeRecipe.DisplayName }
                                                )
                                                .ToString()
                                            : nativeRecipe.DisplayName;
                                        return true;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"[StardewItemManagerResolver] Error resolving '{englishName}': {ex.Message}",
                    LogLevel.Trace
                );
            }
            return false;
        }
    }
}
