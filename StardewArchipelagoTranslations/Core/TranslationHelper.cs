using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using StardewModdingAPI;
using StardewValley;
using StardewValley.TokenizableStrings;

namespace CpdnCristiano.StardewValleyMod.StardewArchipelagoTranslations
{
    public static partial class TranslationHelper
    {
        private static Dictionary<string, string>? _vanillaBundlesMap;
        private static readonly object _bundlesLock = new object();
        private static LocalizedContentManager.LanguageCode _vanillaBundlesLang =
            (LocalizedContentManager.LanguageCode)(-1);

        internal static Dictionary<string, string>? _vanillaObjectsNameMap;
        internal static readonly object _objectsLock = new object();
        internal static Dictionary<string, string>? _vanillaPowersNameMap;
        internal static readonly object _powersLock = new object();

        // Maps Archipelago item names for TV channels to the game's own content string keys
        internal static readonly Dictionary<string, string> _tvChannelGameStringKeys = new(
            StringComparer.OrdinalIgnoreCase
        )
        {
            { "Weather Report", "Strings\\StringsFromCSFiles:TV.cs.13105" },
            { "Fortune Teller", "Strings\\StringsFromCSFiles:TV.cs.13107" },
            { "Livin' Off The Land", "Strings\\StringsFromCSFiles:TV.cs.13111" },
            { "The Queen of Sauce", "Strings\\StringsFromCSFiles:TV.cs.13114" },
            { "The Queen of Sauce (Re-run)", "Strings\\StringsFromCSFiles:TV.cs.13117" },
            {
                "Fishing Information Broadcasting Service",
                "Strings\\StringsFromCSFiles:TV_Fishing_Channel"
            },
        };

        // Performance Caches
        private static readonly Dictionary<string, string> _resolvedItemNamesCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly Dictionary<string, string> _resolvedLocationNamesCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly Dictionary<string, string> _translatedDescriptionsCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly Dictionary<string, string> _localizedBundleNamesCache = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static LocalizedContentManager.LanguageCode _cachesLang =
            (LocalizedContentManager.LanguageCode)(-1);
        private static readonly object _cachesLock = new object();

        // Cache O(1) para Dias da Semana (Lazy Initialization)
        private static Dictionary<string, string>? _weekdayCache;
        private static readonly object _weekdayCacheLock = new object();

        // Mapa global EN → idioma do jogo, construído uma vez ao abrir o save
        // Chave: string em inglês (ex: "Saturday")  Valor: string localizada (ex: "Sábado")
        internal static readonly Dictionary<string, string> _gameStringMap = new(
            StringComparer.OrdinalIgnoreCase
        );
        private static readonly object _gameStringMapLock = new object();

        private static readonly string[] _stringAssets = new[]
        {
            "Strings/StringsFromCSFiles",
            "Strings/StringsFromMaps",
            "Strings/Locations",
            "Strings/UI",
            "Strings/Events",
            "Strings/Notes",
            "Strings/Speech",
        };

        /// <summary>
        /// Carrega todas as strings do jogo em EN e no idioma atual e constrói
        /// um dicionário EN→localizado. Deve ser chamado no SaveLoaded.
        /// </summary>
        public static void BuildGameStringMap()
        {
            lock (_gameStringMapLock)
            {
                _gameStringMap.Clear();

                // Content manager puro (sem localização) para pegar o EN
                using var rawManager = new Microsoft.Xna.Framework.Content.ContentManager(
                    Game1.game1.Content.ServiceProvider,
                    Game1.game1.Content.RootDirectory
                );

                var gameContent = ModEntry.Instance.Helper.GameContent;
                var loaded = 0;

                foreach (var asset in _stringAssets)
                {
                    try
                    {
                        // Carrega EN com barra invertida (formato XNB raw)
                        var rawAsset = asset.Replace('/', '\\');
                        var enStrings = rawManager.Load<Dictionary<string, string>>(rawAsset);

                        // Carrega localizado via SMAPI (barra normal)
                        var ptStrings = gameContent.Load<Dictionary<string, string>>(asset);

                        foreach (var pair in enStrings)
                        {
                            if (string.IsNullOrWhiteSpace(pair.Value))
                                continue;
                            if (
                                !ptStrings.TryGetValue(pair.Key, out var ptVal)
                                || string.IsNullOrWhiteSpace(ptVal)
                            )
                                continue;

                            // Só adiciona se a tradução for diferente do inglês
                            // (evita sobrescrever com strings iguais)
                            _gameStringMap.TryAdd(pair.Value.Trim(), ptVal.Trim());
                            loaded++;
                        }
                    }
                    catch (Exception)
                    {
                        // Asset não encontrado — ignora
                    }
                }

                ModEntry.Instance.Monitor.Log(
                    $"[GameStringMap] {loaded} strings EN→PT carregadas.",
                    LogLevel.Debug
                );
            }
        }

        /// <summary>
        /// Tenta traduzir qualquer string do jogo usando o mapa global EN→PT.
        /// </summary>
        public static bool TryGetLocalizedGameString(string english, out string localized)
        {
            lock (_gameStringMapLock)
            {
                return _gameStringMap.TryGetValue(english.Trim(), out localized!);
            }
        }

        /// <summary>
        /// Obtém o nome localizado do dia da semana de forma otimizada O(1) e Case Insensitive,
        /// aproveitando as respostas da pergunta do Scholar (1.6).
        /// </summary>
        internal static string GetLocalizedWeekday(string englishWeekday)
        {
            if (string.IsNullOrWhiteSpace(englishWeekday))
                return englishWeekday;

            // Inicialização "Lazy" - Cria o cache apenas na primeira vez que for necessário
            if (_weekdayCache == null)
            {
                lock (_weekdayCacheLock)
                {
                    if (_weekdayCache == null)
                    {
                        var newCache = new Dictionary<string, string>(
                            StringComparer.OrdinalIgnoreCase
                        );
                        try
                        {
                            // Ordem exata da resposta no asset Scholar_Question_1_2_Answers
                            string[] engDays =
                            {
                                "Wednesday",
                                "Thursday",
                                "Tuesday",
                                "Monday",
                                "Saturday",
                                "Sunday",
                                "Friday",
                            };

                            // Carrega a string localizada do idioma em uso
                            string locString = Game1.content.LoadString(
                                "Strings\\1_6_Strings:Scholar_Question_1_2_Answers"
                            );
                            string[] locDays = locString.Split(',');

                            // Verifica consistência antes de parear
                            if (engDays.Length == locDays.Length)
                            {
                                for (int i = 0; i < engDays.Length; i++)
                                {
                                    newCache[engDays[i]] = locDays[i].Trim();
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log(
                                $"[WeekdayCache] Error building cache: {ex.Message}",
                                LogLevel.Trace
                            );
                        }

                        _weekdayCache = newCache;
                    }
                }
            }

            // Busca instantânea O(1) ignorando maiúsculas/minúsculas
            if (_weekdayCache.TryGetValue(englishWeekday.Trim(), out var localized))
            {
                return localized;
            }

            // Fallback para o mapa global caso por algum motivo a busca falhe
            if (TryGetLocalizedGameString(englishWeekday, out var globalLocalized))
                return globalLocalized;

            return englishWeekday;
        }

        private static readonly List<IItemResolver> _itemResolvers = new();
        private static readonly List<ILocationResolver> _locationResolvers = new();
        private static readonly List<IBuildingResolver> _buildingResolvers = new();

        static TranslationHelper()
        {
            InitializeResolvers();
        }

        public static void InitializeResolvers()
        {
            lock (_cachesLock)
            {
                if (_itemResolvers.Count > 0)
                    return;

                // Register building resolvers
                _buildingResolvers.Add(new BuildingKeyResolver());
                _buildingResolvers.Add(new VanillaBuildingResolver());
                _buildingResolvers.Add(new StringsBuildingsResolver());

                // Register item resolvers
                _itemResolvers.Add(new PowerPrefixResolver());
                _itemResolvers.Add(new MoneyResolver()); // Registered dynamically resolved money resolver
                _itemResolvers.Add(new ResourcePackResolver());
                _itemResolvers.Add(new TvChannelResolver());
                _itemResolvers.Add(new SkillLevelResolver());
                _itemResolvers.Add(new TravelingMerchantItemResolver());
                _itemResolvers.Add(new TrapResolver());
                _itemResolvers.Add(new LevelResolver());
                _itemResolvers.Add(new ProgressiveResolver());
                _itemResolvers.Add(new ItemKeyResolver());
                _itemResolvers.Add(new PowerKeyResolver());
                _itemResolvers.Add(new VanillaObjectResolver());
                _itemResolvers.Add(new SeasonResolver());
                _itemResolvers.Add(new ToolResolver());
                _itemResolvers.Add(new BuildingItemResolver());
                _itemResolvers.Add(new StardewItemManagerResolver());
                _itemResolvers.Add(new VanillaRecipeResolver());

                // Register location resolvers
                _locationResolvers.Add(new QuestLocationResolver());
                _locationResolvers.Add(new TravelingMerchantLocationResolver());
                _locationResolvers.Add(new TravelingMerchantScamResolver());
                _locationResolvers.Add(new UpgradeResolver());
                _locationResolvers.Add(new PurchaseResolver());
                _locationResolvers.Add(new HarvestResolver());
                _locationResolvers.Add(new BlueprintResolver());
                _locationResolvers.Add(new CommunityCenterAreaResolver());
                _locationResolvers.Add(new BundleResolver());
                _locationResolvers.Add(new LocationKeyResolver());
                _locationResolvers.Add(new TvChannelLocationResolver());
                _locationResolvers.Add(new WalnutsanityResolver());
                _locationResolvers.Add(new ShipsanityResolver());
                _locationResolvers.Add(new MuseumsanityResolver());
                _locationResolvers.Add(new FishsanityResolver());
                _locationResolvers.Add(new WearResolver());
                _locationResolvers.Add(new MoviesanityResolver());
                _locationResolvers.Add(new EatsanityResolver());
                _locationResolvers.Add(new FriendsanityResolver());
                _locationResolvers.Add(new MonsterEradicationResolver());
                _locationResolvers.Add(new DefaultLocationResolver());
            }
        }

        // Cache for slow reflection field info
        internal static System.Reflection.FieldInfo? _itemManagerField;
        private static bool _hasPreScouted = false;
        private static readonly object _preScoutLock = new object();

        public static void ResetPreScout()
        {
            lock (_preScoutLock)
            {
                _hasPreScouted = false;
            }
        }

        public static void CheckAndTriggerPreScout()
        {
            if (_hasPreScouted)
                return;
            if (!Context.IsWorldReady || Game1.player == null)
                return;

            try
            {
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance == null)
                    return;

                var archipelagoField = typeof(StardewArchipelago.ModEntry).GetField(
                    "_archipelago",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );
                var locationCheckerField = typeof(StardewArchipelago.ModEntry).GetField(
                    "_locationChecker",
                    System.Reflection.BindingFlags.NonPublic
                        | System.Reflection.BindingFlags.Instance
                );

                if (archipelagoField == null || locationCheckerField == null)
                    return;

                var archipelago =
                    archipelagoField.GetValue(saInstance)
                    as StardewArchipelago.Archipelago.StardewArchipelagoClient;
                var locationChecker =
                    locationCheckerField.GetValue(saInstance)
                    as StardewArchipelago.Locations.StardewLocationChecker;

                if (archipelago != null && locationChecker != null && archipelago.IsConnected)
                {
                    lock (_preScoutLock)
                    {
                        if (_hasPreScouted)
                            return;
                        _hasPreScouted = true;
                    }

                    System.Threading.Tasks.Task.Run(() =>
                    {
                        try
                        {
                            ModEntry.Instance.Monitor.Log(
                                "Starting background pre-scouting of all missing locations to prevent shop open lag...",
                                LogLevel.Info
                            );
                            var missingLocations = locationChecker
                                .GetAllMissingLocationNames()
                                ?.ToList();
                            if (missingLocations != null && missingLocations.Any())
                            {
                                ModEntry.Instance.Monitor.Log(
                                    $"Found {missingLocations.Count} missing locations to scout. Querying Archipelago server in bulk...",
                                    LogLevel.Info
                                );
                                var scouted = archipelago.ScoutStardewLocations(missingLocations);
                                ModEntry.Instance.Monitor.Log(
                                    $"Successfully pre-scouted {scouted?.Count ?? 0} locations in the background! Shop menus will open instantly.",
                                    LogLevel.Info
                                );
                            }
                            else
                            {
                                ModEntry.Instance.Monitor.Log(
                                    "No missing locations found to scout.",
                                    LogLevel.Info
                                );
                            }
                        }
                        catch (Exception ex)
                        {
                            ModEntry.Instance.Monitor.Log(
                                $"Error during background pre-scouting: {ex}",
                                LogLevel.Error
                            );
                            lock (_preScoutLock)
                            {
                                _hasPreScouted = false;
                            }
                        }
                    });
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error checking pre-scout: {ex.Message}",
                    LogLevel.Trace
                );
            }
        }

        private static void EnsureCachesValid()
        {
            var currentLang = LocalizedContentManager.CurrentLanguageCode;
            if (_cachesLang != currentLang)
            {
                lock (_cachesLock)
                {
                    if (_cachesLang != currentLang)
                    {
                        _resolvedItemNamesCache.Clear();
                        _resolvedLocationNamesCache.Clear();
                        _translatedDescriptionsCache.Clear();
                        _localizedBundleNamesCache.Clear();

                        // Limpa os dicionários de cache quando o idioma muda
                        _vanillaObjectsNameMap = null;
                        _vanillaPowersNameMap = null;

                        lock (_weekdayCacheLock)
                        {
                            _weekdayCache = null; // Força recarregamento dos dias da semana
                        }

                        _cachesLang = currentLang;
                        ModEntry.Instance.Monitor.Log(
                            $"Cleared translation caches due to language change to {currentLang}",
                            LogLevel.Trace
                        );
                    }
                }
            }
        }

        public static void PrepopulateCaches()
        {
            try
            {
                EnsureCachesValid();

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
                            var allItems = stardewItemManager.GetAllItems();
                            if (allItems != null)
                            {
                                int count = 0;
                                lock (_cachesLock)
                                {
                                    foreach (var item in allItems)
                                    {
                                        if (item != null && !string.IsNullOrWhiteSpace(item.Name))
                                        {
                                            if (!_resolvedItemNamesCache.ContainsKey(item.Name))
                                            {
                                                _resolvedItemNamesCache[item.Name] =
                                                    ResolveLocalizedItemName(item.Name);
                                                count++;
                                            }
                                        }
                                    }
                                }

                                // Pre-cache tool upgrades
                                var tools = new[]
                                {
                                    "Axe",
                                    "Pickaxe",
                                    "Hoe",
                                    "Watering Can",
                                    "Trash Can",
                                };
                                var tiers = new[] { "Copper", "Iron", "Gold", "Iridium" };
                                foreach (var tool in tools)
                                {
                                    foreach (var tier in tiers)
                                    {
                                        GetLocalizedLocationName($"{tier} {tool} Upgrade");
                                    }
                                }

                                // Pre-cache Community Center complete locations
                                var ccLocs = new[]
                                {
                                    "Complete Pantry",
                                    "Complete Crafts Room",
                                    "Complete Fish Tank",
                                    "Complete Boiler Room",
                                    "Complete Vault",
                                    "Complete Bulletin Board",
                                    "The Missing Bundle",
                                    "Complete Community Center",
                                };
                                foreach (var ccLoc in ccLocs)
                                {
                                    GetLocalizedLocationName(ccLoc);
                                }

                                // Pre-cache Robin blueprints
                                if (Game1.buildingData != null)
                                {
                                    foreach (var buildingKey in Game1.buildingData.Keys)
                                    {
                                        GetLocalizedLocationName($"{buildingKey} Blueprint");
                                    }
                                }
                                GetLocalizedLocationName("Kitchen Blueprint");

                                ModEntry.Instance.Monitor.Log(
                                    $"Pre-populated {count} items and all shop locations in translation cache!",
                                    LogLevel.Info
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                ModEntry.Instance.Monitor.Log(
                    $"Error pre-populating translation caches: {ex.Message}",
                    LogLevel.Trace
                );
            }
        }

        public static double GetMemoryUsageBytes(out int cacheEntries, out int indexEntries)
        {
            long bytes = 0;
            cacheEntries = 0;
            indexEntries = 0;

            long CalculateExactStringSize(string? s)
            {
                if (s == null)
                    return 0;
                long byteSize = 8 + 8 + 4 + 2 + (s.Length * 2);
                long padding = (8 - (byteSize % 8)) % 8;
                return byteSize + padding;
            }

            long CalculateExactDictSize(Dictionary<string, string>? dict, ref int count)
            {
                if (dict == null)
                    return 0;
                long size = 0;
                lock (dict)
                {
                    int itemsCount = dict.Count;
                    count += itemsCount;
                    size += 48;
                    int buckets = GetNextPrime(itemsCount);
                    size += 24 + (buckets * 4);
                    size += 24 + (itemsCount * 24);
                    foreach (var pair in dict)
                    {
                        size += CalculateExactStringSize(pair.Key);
                        size += CalculateExactStringSize(pair.Value);
                    }
                }
                return size;
            }

            int cachesCount = 0;
            lock (_cachesLock)
            {
                bytes += CalculateExactDictSize(_resolvedItemNamesCache, ref cachesCount);
                bytes += CalculateExactDictSize(_resolvedLocationNamesCache, ref cachesCount);
                bytes += CalculateExactDictSize(_translatedDescriptionsCache, ref cachesCount);
                bytes += CalculateExactDictSize(_localizedBundleNamesCache, ref cachesCount);
            }

            lock (_weekdayCacheLock)
            {
                bytes += CalculateExactDictSize(_weekdayCache, ref cachesCount);
            }

            cacheEntries = cachesCount;

            int indexesCount = 0;
            bytes += CalculateExactDictSize(_vanillaBundlesMap, ref indexesCount);
            bytes += CalculateExactDictSize(_vanillaObjectsNameMap, ref indexesCount);
            bytes += CalculateExactDictSize(_vanillaPowersNameMap, ref indexesCount);
            indexEntries = indexesCount;

            bytes += 85 * 1024;
            return bytes;
        }

        private static bool IsPrime(int number)
        {
            if (number <= 1)
                return false;
            if (number == 2)
                return true;
            if (number % 2 == 0)
                return false;
            var boundary = (int)Math.Floor(Math.Sqrt(number));
            for (int i = 3; i <= boundary; i += 2)
            {
                if (number % i == 0)
                    return false;
            }
            return true;
        }

        private static int GetNextPrime(int min)
        {
            for (int i = min; i < int.MaxValue; i++)
            {
                if (IsPrime(i))
                    return i;
            }
            return min;
        }

        public static void DumpMapToFile()
        {
            var outputPath = Path.Combine(
                ModEntry.Instance.Helper.DirectoryPath,
                "mapped_items.txt"
            );
            var lines = new List<string>();

            try
            {
                lines.Add("=== Translation Properties ===");
                foreach (
                    var prop in typeof(Translation).GetProperties(
                        System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.Static
                    )
                )
                {
                    lines.Add($"Prop: {prop.Name} ({prop.PropertyType})");
                }
                foreach (
                    var field in typeof(Translation).GetFields(
                        System.Reflection.BindingFlags.Public
                            | System.Reflection.BindingFlags.Instance
                            | System.Reflection.BindingFlags.Static
                    )
                )
                {
                    lines.Add($"Field: {field.Name} ({field.FieldType})");
                }
                lines.Add("=============================");
            }
            catch (Exception ex)
            {
                lines.Add($"Error inspecting Translation: {ex.Message}");
            }

            try
            {
                var saInstance = StardewArchipelago.ModEntry.Instance;
                if (saInstance != null)
                {
                    var itemManagerField = typeof(StardewArchipelago.ModEntry).GetField(
                        "_stardewItemManager",
                        System.Reflection.BindingFlags.NonPublic
                            | System.Reflection.BindingFlags.Instance
                    );
                    if (itemManagerField != null)
                    {
                        var stardewItemManager =
                            itemManagerField.GetValue(saInstance)
                            as StardewArchipelago.Stardew.StardewItemManager;
                        if (stardewItemManager != null)
                        {
                            var allItems = stardewItemManager.GetAllItems();
                            foreach (var item in allItems.OrderBy(i => i.Name))
                            {
                                var localizedName = ResolveLocalizedItemName(item.Name);
                                var langCode =
                                    LocalizedContentManager.CurrentLanguageCode.ToString();
                                var category = Patcher.MailPatcher.GetItemCategory(item.Name);
                                lines.Add(
                                    $"EN: '{item.Name}' -> {langCode}: '{localizedName}' [ID: {item.GetQualifiedId()}] [Category: {category}]"
                                );
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                lines.Add($"Error dumping map: {ex.Message}");
            }

            File.WriteAllLines(outputPath, lines);
            ModEntry.Instance.Monitor.Log(
                $"Dumped dynamic translations to: {outputPath}",
                LogLevel.Info
            );
        }
    }
}
