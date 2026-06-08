using System.Text.RegularExpressions;
using System.Text.Json;

namespace StardewArchipelagoTranslations.Tests;

/// <summary>
/// Lógica pura copiada dos resolvers — sem dependências do jogo.
/// Serve como "especificação executável" das regras de tradução.
/// </summary>
public static class TranslationLogic
{
    // ── Regex (mesmos dos resolvers) ───────────────────────────────────────
    public static readonly Regex TravelingMerchantItemPattern = new(
        @"^Traveling Merchant\s+(.+?)\s+Item\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled
    );

    private const string DayPrefix = "Traveling Merchant: ";

    // ── Mapeamento de chave do jogo (copiado de GetLocalizedWeekday) ───────
    public static readonly IReadOnlyDictionary<string, string> WeekdayToGameKey =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["monday"]    = "Strings\\StringsFromCSFiles:Utility.cs.11068",
            ["tuesday"]   = "Strings\\StringsFromCSFiles:Utility.cs.11069",
            ["wednesday"] = "Strings\\StringsFromCSFiles:Utility.cs.11070",
            ["thursday"]  = "Strings\\StringsFromCSFiles:Utility.cs.11071",
            ["friday"]    = "Strings\\StringsFromCSFiles:Utility.cs.11072",
            ["saturday"]  = "Strings\\StringsFromCSFiles:Utility.cs.11073",
            ["sunday"]    = "Strings\\StringsFromCSFiles:Utility.cs.11074",
        };

    // ── Simula a tradução em PT usando os i18n direto ─────────────────────
    private static readonly Lazy<Dictionary<string, string>> _ptJson = new(() =>
    {
        var path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "StardewArchipelagoTranslations", "i18n", "pt.json"
        );
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<Dictionary<string, string>>(json)!;
    });

    // Dias em PT (simulados, pois o jogo não está presente)
    public static readonly IReadOnlyDictionary<string, string> WeekdayPt =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["monday"]    = "Segunda-feira",
            ["tuesday"]   = "Terça-feira",
            ["wednesday"] = "Quarta-feira",
            ["thursday"]  = "Quinta-feira",
            ["friday"]    = "Sexta-feira",
            ["saturday"]  = "Sábado",
            ["sunday"]    = "Domingo",
        };

    public static bool TryGetI18n(string key, out string value)
        => _ptJson.Value.TryGetValue(key, out value!);

    public static string ApplyDayTemplate(string template, string localizedDay, string? itemLabel = null)
    {
        var result = template
            .Replace("{{day}}", localizedDay)
            .Replace("{day}", localizedDay);
        if (itemLabel != null)
        {
            result = result
                .Replace("{{item}}", itemLabel)
                .Replace("{item}", itemLabel);
        }
        return result;
    }
}
