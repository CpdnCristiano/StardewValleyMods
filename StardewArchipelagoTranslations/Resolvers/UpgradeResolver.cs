using System;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class UpgradeResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            if (englishName.EndsWith(" Upgrade", StringComparison.OrdinalIgnoreCase))
            {
                var cleanToolName = englishName.Substring(0, englishName.Length - 8).Trim();
                var toolId = cleanToolName.Replace(" ", "");
                if (toolId.StartsWith("Iron", StringComparison.OrdinalIgnoreCase))
                {
                    toolId = "Steel" + toolId.Substring(4);
                }
                var toolData = ItemRegistry.GetData($"(T){toolId}");
                if (toolData != null && !string.IsNullOrWhiteSpace(toolData.DisplayName))
                {
                    localizedName = ModEntry
                        .Translation.Get(
                            "hints.upgrade_format",
                            new { name = toolData.DisplayName }
                        )
                        .ToString();
                    return true;
                }
            }
            return false;
        }
    }
}
