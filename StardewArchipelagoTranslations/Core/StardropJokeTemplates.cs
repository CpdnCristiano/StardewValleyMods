using System;
using System.Collections.Generic;
using System.IO;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal static class StardropJokeTemplates
    {
        private static readonly Random Random = new();
        private static Dictionary<string, List<string>> _templates =
            new(StringComparer.OrdinalIgnoreCase);

        internal static void Load(IModHelper helper)
        {
            _templates = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            var locale = helper.Translation.Locale;
            if (string.IsNullOrWhiteSpace(locale))
            {
                return;
            }

            var relativePath = GetTemplateRelativePath(helper, locale);
            if (string.IsNullOrWhiteSpace(relativePath))
            {
                return;
            }

            try
            {
                var loaded = helper.Data.ReadJsonFile<Dictionary<string, List<string>>>(
                    relativePath
                );
                if (loaded == null)
                {
                    return;
                }

                foreach (var pair in loaded)
                {
                    if (
                        string.IsNullOrWhiteSpace(pair.Key)
                        || pair.Value == null
                        || pair.Value.Count <= 0
                    )
                    {
                        continue;
                    }

                    var validTemplates = pair.Value.FindAll(template =>
                        !string.IsNullOrWhiteSpace(template)
                    );
                    if (validTemplates.Count > 0)
                    {
                        _templates[pair.Key.Trim()] = validTemplates;
                    }
                }
            }
            catch (Exception)
            {
                return;
            }
        }


        internal static bool TryGetJoke(string? favoriteThing, out string joke)
        {
            joke = string.Empty;
            if (string.IsNullOrWhiteSpace(favoriteThing) || _templates.Count <= 0)
            {
                return false;
            }

            var favorite = favoriteThing.Trim();
            if (!TryGetMatchingTemplates(favorite, out var key, out var templates))
            {
                return false;
            }

            var template = templates[Random.Next(templates.Count)];
            joke = template
                .Replace("{player}", Game1.player?.Name ?? string.Empty)
                .Replace("{favorite}", favorite)
                .Replace("{key}", key);
            return true;
        }

        private static bool TryGetMatchingTemplates(
            string favorite,
            out string key,
            out List<string> templates
        )
        {
            foreach (var pair in _templates)
            {
                if (favorite.Equals(pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    key = pair.Key;
                    templates = pair.Value;
                    return true;
                }
            }

            foreach (var pair in _templates)
            {
                if (favorite.Contains(pair.Key, StringComparison.OrdinalIgnoreCase))
                {
                    key = pair.Key;
                    templates = pair.Value;
                    return true;
                }
            }

            key = string.Empty;
            templates = null!;
            return false;
        }

        private static string? GetTemplateRelativePath(IModHelper helper, string locale)
        {
            foreach (var candidate in GetLocaleCandidates(locale))
            {
                var relativePath = $"templates/stardropjokes/{candidate}.json";
                var fullPath = Path.Combine(helper.DirectoryPath, relativePath);

                if (File.Exists(fullPath))
                {
                    return relativePath;
                }
            }

            return null;
        }

        private static IEnumerable<string> GetLocaleCandidates(string locale)
        {
            yield return locale;

            var separatorIndex = locale.IndexOfAny(new[] { '-', '_' });
            if (separatorIndex > 0)
            {
                yield return locale[..separatorIndex];
            }
        }

    }
}
