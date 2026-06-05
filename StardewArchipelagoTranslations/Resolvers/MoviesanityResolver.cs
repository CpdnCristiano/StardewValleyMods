using System;
using System.Collections.Generic;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class MoviesanityResolver : ILocationResolver
    {
        private static readonly Dictionary<string, string> _movieTitlesEngToPt = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "The Brave Little Sapling", "O Pequeno Broto Valente" },
            { "Mysterium", "Mysterium" },
            {
                "Journey Of The Prairie King: The Motion Picture",
                "A Jornada do Rei da Pradaria: O Filme"
            },
            { "Wumbus", "Wumbus" },
            { "The Zuzu City Express", "O Expresso da Cidade de Zuzu" },
            { "The Miracle At Coldstar Ranch", "O Milagre no Rancho Estrela Polar" },
            {
                "Natural Wonders: Exploring Our Vibrant World",
                "Maravilhas Naturais: Explorando Nosso Mundo Vibrante"
            },
            { "It Howls In The Rain", "Ele Uiva na Chuva" },
        };

        public bool TryResolve(string englishName, out string? localizedName)
        {
            localizedName = null;

            if (englishName.Equals("Watch A Movie", StringComparison.OrdinalIgnoreCase))
            {
                localizedName = ModEntry
                    .Translation.Get("location.moviesanity.watch_a_movie")
                    .ToString();
                return true;
            }

            const string watchPrefix = "Watch ";
            if (englishName.StartsWith(watchPrefix, StringComparison.OrdinalIgnoreCase))
            {
                var movieTitle = englishName.Substring(watchPrefix.Length).Trim();
                if (_movieTitlesEngToPt.TryGetValue(movieTitle, out var ptTitle))
                {
                    movieTitle = ptTitle;
                }
                localizedName = ModEntry
                    .Translation.Get(
                        "location.moviesanity.watch_format",
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
                        "location.moviesanity.share_format",
                        new { snack = localizedSnack }
                    )
                    .ToString();
                return true;
            }

            return false;
        }
    }
}
