using System;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class MoviesanityResolver : ILocationResolver
    {
        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            if (englishName.Equals("Watch A Movie", StringComparison.OrdinalIgnoreCase))
            {
                localizedName = ModEntry
                    .Translation.Get("moviesanity.watch_a_movie")
                    .ToString();
                return true;
            }

            const string watchPrefix = "Watch ";
            if (englishName.StartsWith(watchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var movieTitle = englishName.Substring(watchPrefix.Length).Trim();
                movieTitle = ResolveLocalizedMovieTitle(movieTitle);

                localizedName = ModEntry
                    .Translation.Get(
                        "moviesanity.watch_format",
                        new { movie = movieTitle }
                    )
                    .ToString();
                return true;
            }

            const string sharePrefix = "Share ";
            if (englishName.StartsWith(sharePrefix, StringComparison.OrdinalIgnoreCase))
            {
                var snackName = englishName.Substring(sharePrefix.Length).Trim();
                var localizedSnack = TranslationHelper.GetLocalizedItemName(snackName);
                localizedName = ModEntry
                    .Translation.Get(
                        "moviesanity.share_format",
                        new { snack = localizedSnack }
                    )
                    .ToString();
                return true;
            }

            return false;
        }

        private static string ResolveLocalizedMovieTitle(string englishTitle)
        {
            var key = $"moviesanity.movie.{ResolverText.ToKeySegment(englishTitle)}";
            return ResolverText.TryGetTranslation(key, out var localizedTitle)
                ? localizedTitle
                : englishTitle;
        }
    }
}
