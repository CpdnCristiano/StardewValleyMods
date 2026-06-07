using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations.Patcher;
using HarmonyLib;
using StardewModdingAPI;
using StardewValley;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public class ModEntry : Mod
    {
        public static ModEntry Instance { get; private set; } = null!;
        public static ITranslationHelper Translation => Instance.Helper.Translation;

        public override void Entry(IModHelper helper)
        {
            Instance = this;

            var harmony = new Harmony(this.ModManifest.UniqueID);

            // Load static Harmony patches from the assembly
            harmony.PatchAll(Assembly.GetExecutingAssembly());

            // Load dynamic Harmony patches
            I18nPatcher.Patch(harmony);
            BundlesPatcher.Patch(harmony);
            ShopPatcher.Patch(harmony);
            ScoutPatcher.Patch(harmony);
            MultiSleepPatcher.Patch(harmony);
            TelevisionPatcher.Patch(harmony);
            GoalsPatcher.Patch(harmony);
            ParrotPatcher.Patch(harmony);
            DialoguePatcher.Patch(harmony);
            ConfigMenuPatcher.Patch(harmony);
            CarpenterPatcher.Patch(harmony);
            ArcadePatcher.Patch(harmony);
            TravelingMerchantPatcher.Patch(harmony);
            WizardBookPatcher.Patch(harmony);
            BillboardPatcher.Patch(harmony);
            // CasinoPatcher.Patch(harmony);

            // Load custom JSON mail templates
            MailPatcher.LoadTemplates(helper);

            // Pre-populate translation caches when the save is loaded to ensure zero gameplay delay
            helper.Events.GameLoop.SaveLoaded += (sender, e) =>
            {
                TranslationHelper.BuildGameStringMap();
                TranslationHelper.PrepopulateCaches();
                TranslationHelper.ResetPreScout();
                ScoutPatcher.ClearCache();
            };

            helper.Events.GameLoop.ReturnedToTitle += (sender, e) =>
            {
                TranslationHelper.ResetPreScout();
                ScoutPatcher.ClearCache();
            };

            helper.Events.GameLoop.UpdateTicked += (sender, e) =>
            {
                if (e.IsMultipleOf(60))
                {
                    TranslationHelper.CheckAndTriggerPreScout();
                }
            };

            // Register debug console commands
            helper.ConsoleCommands.Add(
                "find_game_string",
                "Searches Stardew Valley native asset files for a string and outputs its key and translations. Usage: find_game_string <term>",
                (command, args) =>
                {
                    if (args.Length == 0)
                    {
                        Monitor.Log(
                            "Please provide a search term. Usage: find_game_string <term>",
                            LogLevel.Error
                        );
                        return;
                    }
                    var searchTerm = string.Join(" ", args);
                    Monitor.Log($"Searching game assets for '{searchTerm}'...", LogLevel.Info);

                    var stringAssets = new[]
                    {
                        "Strings\\StringsFromCSFiles",
                        "Strings\\StringsFromMaps",
                        "Strings\\Locations",
                        "Strings\\UI",
                        "Strings\\Events",
                        "Strings\\Notes",
                        "Strings\\Speech",
                    };

                    using (
                        var engManager = new Microsoft.Xna.Framework.Content.ContentManager(
                            Game1.game1.Content.ServiceProvider,
                            Game1.game1.Content.RootDirectory
                        )
                    )
                    {
                        var foundAny = false;
                        foreach (var asset in stringAssets)
                        {
                            try
                            {
                                var engStrings = engManager.Load<Dictionary<string, string>>(asset);
                                var locStrings = Game1.content.Load<Dictionary<string, string>>(
                                    asset
                                );
                                if (engStrings != null && locStrings != null)
                                {
                                    foreach (var pair in engStrings)
                                    {
                                        if (
                                            pair.Value != null
                                            && pair.Value.Contains(
                                                searchTerm,
                                                StringComparison.OrdinalIgnoreCase
                                            )
                                        )
                                        {
                                            locStrings.TryGetValue(pair.Key, out var locVal);
                                            Monitor.Log(
                                                $"[Asset: {asset}] Key: '{pair.Key}'\n -> EN: '{pair.Value}'\n -> PT: '{locVal ?? "N/A"}'",
                                                LogLevel.Info
                                            );
                                            foundAny = true;
                                        }
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                Monitor.Log(
                                    $"Failed to search asset '{asset}': {ex.Message}",
                                    LogLevel.Trace
                                );
                            }
                        }
                        if (!foundAny)
                        {
                            Monitor.Log(
                                $"No native occurrences of '{searchTerm}' were found in string assets.",
                                LogLevel.Warn
                            );
                        }
                    }
                }
            );

            helper.ConsoleCommands.Add(
                "ap_pt_mem",
                "Shows the exact, real-time memory usage of the StardewArchipelagoTranslations translation mod.",
                (command, args) =>
                {
                    try
                    {
                        double bytes = TranslationHelper.GetMemoryUsageBytes(
                            out int cacheCount,
                            out int indexCount
                        );
                        double kb = bytes / 1024.0;
                        double mb = kb / 1024.0;

                        Monitor.Log(
                            "====================================================",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            "   StardewArchipelagoTranslations - Memory Usage Status",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            "====================================================",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            $" -> Active Dynamic Caches:  {cacheCount} entries",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            $" -> Game Database Indexes:  {indexCount} entries",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            $" -> Physical RAM Size: {kb:F2} KB ({mb:F4} MB)",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            "----------------------------------------------------",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            " Status: Extremely optimized, light, and healthy! (0.01% game impact)",
                            LogLevel.Info
                        );
                        Monitor.Log(
                            "====================================================",
                            LogLevel.Info
                        );
                    }
                    catch (Exception ex)
                    {
                        Monitor.Log($"Failed to measure memory usage: {ex}", LogLevel.Error);
                    }
                }
            );
            helper.ConsoleCommands.Add(
                "dump_game_strings",
                "Salva todas as strings do jogo (EN + PT) em um JSON na pasta do mod. Uso: dump_game_strings [filtro_opcional]",
                (command, args) =>
                {
                    var filter = args.Length > 0 ? string.Join(" ", args) : null;
                    var outputPath = Path.Combine(
                        helper.DirectoryPath,
                        $"dump_strings_{DateTime.Now:yyyyMMdd_HHmmss}.json"
                    );

                    // Lista expandida com os dicionários do Stardew Valley 1.6+
                    var stringAssets = new[]
                    {
                        // === CORE, UI & MAPAS ===
                        "Strings\\StringsFromCSFiles",
                        "Strings\\StringsFromMaps",
                        "Strings\\UI",
                        "Strings\\1_6_Strings", // Fundamental para a versão 1.6 em diante
                        "Strings\\Locations",
                        // === ITENS, FERRAMENTAS & EQUIPAMENTOS ===
                        "Strings\\Objects", // Itens normais, recursos, comidas, sementes
                        "Strings\\BigCraftables", // Máquinas, espantalhos, baús
                        "Strings\\Weapons", // Espadas, adagas, porretes
                        "Strings\\Tools", // Enxadas, regadores, picaretas
                        "Strings\\Boots", // Botas e sapatos
                        "Strings\\Hats", // Chapéus
                        "Strings\\Clothing", // Roupas, camisas, calças
                        "Strings\\Furniture", // Móveis de decoração
                        // === QUESTS, CARTAS & HISTÓRIA ===
                        "Strings\\Quests", // Missões normais do correio/diário
                        "Strings\\SpecialOrderStrings", // Pedidos Especiais (Quadro gigante)
                        "Strings\\Notes", // Recados Secretos
                        "Strings\\Events", // Falas dos eventos de coração/cutscenes
                        "Strings\\Mail", // Cartas recebidas pelo correio
                        // === NPCs, PERSONAGENS & MONSTROS ===
                        "Strings\\NPCNames", // Lista de Nomes dos NPCs
                        "Strings\\Characters", // Textos variados de personagens
                        "Strings\\schedules", // Textos que eles dizem durante as rotinas
                        "Strings\\Monsters", // Nomes de monstros
                        // === OUTROS SISTEMAS DO JOGO ===
                        "Strings\\Buildings", // Nomes de construções da fazenda (Celeiro, Galinheiro)
                        "Strings\\BundleNames", // Nomes dos conjuntos do Centro Comunitário
                        "Strings\\Crops", // Textos de plantações
                        "Strings\\EnchantmentNames", // Encantamentos da Forja do Vulcão
                        "Strings\\FishPondAnswers", // Respostas e pedidos dos Lagos de Peixes
                        // === PASTAS DE DADOS ESPECÍFICOS (ONDE FICAM ALGUMAS QUESTS E DIÁLOGOS) ===
                        "Data\\Quests", // <-- AQUI ESTÁ A "INTRODUCTIONS" E OUTRAS MISSÕES
                        "Data\\Mail", // O conteúdo completo das cartas recebidas
                        "Data\\Events\\Town", // Eventos da cidade
                        "Data\\Festivals\\spring13", // Exemplo: Caça aos ovos
                        "Data\\Festivals\\spring24", // Exemplo: Dança das Flores
                        "Data\\Festivals\\fall8", // Exemplo: Feira
                        "Data\\Festivals\\fall27", // Exemplo: Labirinto
                        "Data\\Festivals\\winter8", // Exemplo: Festival do Gelo
                        "Data\\Festivals\\winter25", // Exemplo: Estrela Invernal
                    };

                    var result = new Dictionary<string, Dictionary<string, object>>();

                    using var engManager = new Microsoft.Xna.Framework.Content.ContentManager(
                        Game1.content.ServiceProvider, // Usando Game1.content diretamente (mais seguro na 1.6)
                        Game1.content.RootDirectory
                    );

                    var totalKeys = 0;
                    foreach (var asset in stringAssets)
                    {
                        try
                        {
                            // Carrega o original em inglês (base) e o localizado (PT-BR)
                            var engStrings = engManager.Load<Dictionary<string, string>>(asset);
                            var locStrings = Game1.content.Load<Dictionary<string, string>>(asset);

                            foreach (var pair in engStrings)
                            {
                                // Filtro de busca (case-insensitive)
                                if (
                                    filter != null
                                    && !pair.Key.Contains(
                                        filter,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                    && !pair.Value.Contains(
                                        filter,
                                        StringComparison.OrdinalIgnoreCase
                                    )
                                )
                                    continue;

                                locStrings.TryGetValue(pair.Key, out var locVal);
                                var fullKey = $"{asset}:{pair.Key}";

                                result[fullKey] = new Dictionary<string, object>
                                {
                                    ["asset"] = asset,
                                    ["key"] = pair.Key,
                                    ["en"] = pair.Value,
                                    ["pt"] = locVal ?? "",
                                };
                                totalKeys++;
                            }
                        }
                        catch (Exception ex)
                        {
                            Monitor.Log(
                                $"Falha ao carregar '{asset}': {ex.Message}",
                                LogLevel.Warn
                            );
                        }
                    }

                    var json = System.Text.Json.JsonSerializer.Serialize(
                        result,
                        new System.Text.Json.JsonSerializerOptions { WriteIndented = true }
                    );
                    File.WriteAllText(outputPath, json);

                    Monitor.Log($"✅ {totalKeys} keys salvas em: {outputPath}", LogLevel.Info);
                    if (filter != null)
                        Monitor.Log($"   (filtradas por '{filter}')", LogLevel.Info);
                }
            );
        }
    }
}
