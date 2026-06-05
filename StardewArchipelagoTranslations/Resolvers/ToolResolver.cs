using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ToolResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            var toolNameNoSpaces = englishName.Replace(" ", "");
            var toolData =
                ItemRegistry.GetData($"(T){englishName}")
                ?? ItemRegistry.GetData($"(T){toolNameNoSpaces}");
            if (toolData != null && !string.IsNullOrWhiteSpace(toolData.DisplayName))
            {
                localizedName = toolData.DisplayName;
                return true;
            }
            return false;
        }
    }
}
