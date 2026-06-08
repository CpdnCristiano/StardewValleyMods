using System.Text.RegularExpressions;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    internal static class StardropFavoriteThingTemplate
    {
        private const string FavoriteThingPlaceholder = "{{favoriteThing}}";
        private const string SourceThoughts =
            "Your mind is filled with thoughts of... {{favoriteThing}}? ^";
        private const string SourceMaximumBundles = "Even with these bundles?^";
        private const string SourceNightmareTraps = "Even with these traps??^";
        private const string SourceChaosEr = "Even on Chaos ER?!? You scare me.^";
        private const string SourceTryHarder =
            "Try harder settings, you'll change your mind.^";

        private static readonly Regex ThoughtsPattern = new(
            Regex.Escape(SourceThoughts).Replace(Regex.Escape(FavoriteThingPlaceholder), "(.*?)"),
            RegexOptions.Compiled
        );

        internal static string Localize(string text)
        {
            var localizedText = ThoughtsPattern.Replace(
                text,
                match =>
                {
                    var favoriteThing = match.Groups[1].Value;
                    return ModEntry
                        .Translation.Get(GetThoughtsKey(favoriteThing), new { favoriteThing })
                        .ToString();
                }
            );

            localizedText = localizedText.Replace(
                SourceMaximumBundles,
                ModEntry.Translation.Get("kaito_stardrop.favorite_thing.maximum_bundles").ToString()
            );
            localizedText = localizedText.Replace(
                SourceNightmareTraps,
                ModEntry.Translation.Get("kaito_stardrop.favorite_thing.nightmare_traps").ToString()
            );
            localizedText = localizedText.Replace(
                SourceChaosEr,
                ModEntry.Translation.Get("kaito_stardrop.favorite_thing.chaos_er").ToString()
            );
            localizedText = localizedText.Replace(
                SourceTryHarder,
                ModEntry.Translation.Get("kaito_stardrop.favorite_thing.try_harder").ToString()
            );

            return localizedText;
        }

        private static string GetThoughtsKey(string favoriteThing)
        {
            var specificKey = $"kaito_stardrop.favorite_thing.thoughts.{ResolverText.ToKeySegment(favoriteThing)}";
            return ModEntry.Translation.Get(specificKey).HasValue()
                ? specificKey
                : "kaito_stardrop.favorite_thing.thoughts";
        }
    }
}
