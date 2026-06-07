using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        public static string GetLocalizedLocationName(string englishLocationName)
        {
            if (string.IsNullOrWhiteSpace(englishLocationName))
                return englishLocationName;

            EnsureCachesValid();

            lock (_cachesLock)
            {
                if (_resolvedLocationNamesCache.TryGetValue(englishLocationName, out var cached))
                {
                    return cached;
                }
            }

            var result = englishLocationName;

            foreach (var resolver in _locationResolvers)
            {
                if (
                    resolver.TryResolve(englishLocationName, out var localized)
                    && localized != null
                )
                {
                    result = localized;
                    break;
                }
            }

            lock (_cachesLock)
            {
                _resolvedLocationNamesCache[englishLocationName] = result;
            }

            return result;
        }

        // NOVO: Cache direto! Mapeia Título em Inglês -> Título Traduzido.
        // Extremamente rápido, pois a leitura dos dados só ocorre na primeira vez.
        private static Dictionary<string, string>? _englishQuestTitleToLocalizedTitleCache;

        internal static bool TryResolveLocalizedQuestLocation(
            string englishLocationName,
            out string localizedLocation
        )
        {
            localizedLocation = englishLocationName;

            const string questPrefix = "Quest: ";
            if (!englishLocationName.StartsWith(questPrefix, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var englishQuestTitle = englishLocationName.Substring(questPrefix.Length).Trim();
            if (string.IsNullOrWhiteSpace(englishQuestTitle))
            {
                return false;
            }

            try
            {
                // 1. Inicializa o cache direto de Inglês para o Idioma Atual (roda apenas na primeira vez)
                if (_englishQuestTitleToLocalizedTitleCache == null)
                {
                    _englishQuestTitleToLocalizedTitleCache = new Dictionary<string, string>(
                        StringComparer.OrdinalIgnoreCase
                    );

                    // Cria um gerenciador de conteúdo temporário apenas para ler a base em inglês
                    using var engManager = new Microsoft.Xna.Framework.Content.ContentManager(
                        Game1.content.ServiceProvider,
                        Game1.content.RootDirectory
                    );

                    // Carrega os dois idiomas simultaneamente para construir a ponte
                    var engQuests = engManager.Load<Dictionary<string, string>>("Data\\Quests");
                    var locQuests = Game1.content.Load<Dictionary<string, string>>("Data\\Quests");

                    foreach (var kvp in engQuests)
                    {
                        var engParts = kvp.Value.Split('/');

                        // Garante que o nome em inglês existe (Índice 1)
                        if (engParts.Length >= 2 && !string.IsNullOrWhiteSpace(engParts[1]))
                        {
                            var engTitle = engParts[1].Trim();

                            // Procura o mesmo ID na base traduzida (idioma que o jogador está a usar)
                            if (locQuests.TryGetValue(kvp.Key, out var locQuestData))
                            {
                                var locParts = locQuestData.Split('/');
                                if (locParts.Length >= 2)
                                {
                                    var locTitle = locParts[1].Trim();

                                    // Salva a relação final: "Nome em Inglês" -> "Nome Traduzido"
                                    _englishQuestTitleToLocalizedTitleCache.TryAdd(
                                        engTitle,
                                        locTitle
                                    );
                                }
                            }
                        }
                    }
                }

                // 2. Busca ultra-rápida diretamente no dicionário construído
                if (
                    _englishQuestTitleToLocalizedTitleCache.TryGetValue(
                        englishQuestTitle,
                        out var localizedTitle
                    )
                )
                {
                    localizedLocation = $"{questPrefix}{localizedTitle}";
                    return true; // Sucesso! Retornou o nome traduzido.
                }
            }
            catch (Exception ex)
            {
                // Se o ModEntry estiver configurado como singleton (ModEntry.Instance)
                ModEntry.Instance.Monitor.Log(
                    $"Falha ao buscar a tradução da quest '{englishQuestTitle}': {ex.Message}",
                    LogLevel.Trace
                );
            }

            return false;
        }

        internal static string GetLocalizedAreaName(string englishAreaName)
        {
            if (string.IsNullOrWhiteSpace(englishAreaName))
                return englishAreaName;

            var clean = englishAreaName.Replace(" ", "");

            try
            {
                var key = $"Strings\\Locations:CommunityCenter_AreaName_{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            try
            {
                var key = $"Strings\\UI:CommunityCenter_AreaName_{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            try
            {
                var key = $"Strings\\Locations:{clean}";
                var localized = Game1.content.LoadString(key);
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }
            catch { }

            var sanitized = englishAreaName.Replace(" ", "_").Replace("'", "").ToLower();
            var itemKey = $"item.{sanitized}";

            // Assumindo que ModEntry tem acesso ao ITranslationHelper
            if (ModEntry.Translation.ContainsKey(itemKey))
            {
                return ModEntry.Translation.Get(itemKey).ToString();
            }
            var locKey = $"location.{sanitized}";
            if (ModEntry.Translation.ContainsKey(locKey))
            {
                return ModEntry.Translation.Get(locKey).ToString();
            }

            return englishAreaName;
        }

        public static string GetLocalizedBuildingName(string englishBuildingName)
        {
            if (string.IsNullOrWhiteSpace(englishBuildingName))
                return englishBuildingName;

            foreach (var resolver in _buildingResolvers)
            {
                if (
                    resolver.TryResolve(englishBuildingName, out var localized)
                    && localized != null
                )
                {
                    return localized;
                }
            }

            return englishBuildingName;
        }
    }
}
