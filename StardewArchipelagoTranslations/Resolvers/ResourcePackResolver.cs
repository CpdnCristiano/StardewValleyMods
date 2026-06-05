using System;
using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ResourcePackResolver : IItemResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;
            const string prefix = "Resource Pack: ";
            if (englishName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var rest = englishName.Substring(prefix.Length).Trim();
                var match = Regex.Match(rest, @"^(\d+)\s+(.+)$");
                if (match.Success)
                {
                    var amount = match.Groups[1].Value;
                    var itemName = match.Groups[2].Value;
                    var localizedItem = TranslationHelper.GetLocalizedItemName(itemName);

                    var templateKey = "hints.resource_pack_amount_format";
                    if (ModEntry.Translation.ContainsKey(templateKey))
                    {
                        localizedName = ModEntry
                            .Translation.Get(
                                templateKey,
                                new { amount = amount, item = localizedItem }
                            )
                            .ToString();
                    }
                    else
                    {
                        localizedName = $"Pacote de Recursos: {amount} {localizedItem}";
                    }
                    return true;
                }
                else
                {
                    var localizedItem = TranslationHelper.GetLocalizedItemName(rest);
                    var templateKey = "hints.resource_pack_format";
                    if (ModEntry.Translation.ContainsKey(templateKey))
                    {
                        localizedName = ModEntry
                            .Translation.Get(templateKey, new { item = localizedItem })
                            .ToString();
                    }
                    else
                    {
                        localizedName = $"Pacote de Recursos: {localizedItem}";
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
