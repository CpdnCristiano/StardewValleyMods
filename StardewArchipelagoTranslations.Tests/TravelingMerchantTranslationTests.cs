using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace StardewArchipelagoTranslations.Tests;

// ═══════════════════════════════════════════════════════════════════════════
// 1. MAPEAMENTO DE DIAS DA SEMANA
// ═══════════════════════════════════════════════════════════════════════════
public class WeekdayMappingTests
{
    [Theory]
    [InlineData("monday",    "Utility.cs.11068")]
    [InlineData("tuesday",   "Utility.cs.11069")]
    [InlineData("wednesday", "Utility.cs.11070")]
    [InlineData("thursday",  "Utility.cs.11071")]
    [InlineData("friday",    "Utility.cs.11072")]
    [InlineData("saturday",  "Utility.cs.11073")]
    [InlineData("sunday",    "Utility.cs.11074")]
    public void WeekdayMapsToCorrectGameKey(string day, string expectedKeySuffix)
    {
        Assert.True(TranslationLogic.WeekdayToGameKey.TryGetValue(day, out var key));
        Assert.EndsWith(expectedKeySuffix, key);
    }

    [Theory]
    [InlineData("Monday")]
    [InlineData("FRIDAY")]
    [InlineData("Saturday")]
    public void WeekdayLookupIsCaseInsensitive(string day)
    {
        Assert.True(TranslationLogic.WeekdayToGameKey.ContainsKey(day));
    }

    [Fact]
    public void AllSevenDaysAreMapped()
    {
        Assert.Equal(7, TranslationLogic.WeekdayToGameKey.Count);
    }

    [Fact]
    public void AllGameKeysAreDistinct()
    {
        var keys = TranslationLogic.WeekdayToGameKey.Values.ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Theory]
    [InlineData("sunday")]
    [InlineData("notaday")]
    [InlineData("")]
    public void UnknownDayNotMappedToMonday(string day)
    {
        // Garante que o bug original (sunday→11068=Monday) não volta
        if (TranslationLogic.WeekdayToGameKey.TryGetValue(day, out var key))
            Assert.NotEqual("Strings\\StringsFromCSFiles:Utility.cs.11068", key);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 2. REGEX DOS RESOLVERS
// ═══════════════════════════════════════════════════════════════════════════
public class TravelingMerchantRegexTests
{
    [Theory]
    [InlineData("Traveling Merchant Friday Item 1",    "Friday",    "1")]
    [InlineData("Traveling Merchant Monday Item 2",    "Monday",    "2")]
    [InlineData("Traveling Merchant Saturday Item 10", "Saturday",  "10")]
    [InlineData("Traveling Merchant Sunday Item 3",    "Sunday",    "3")]
    [InlineData("traveling merchant tuesday item 5",   "tuesday",   "5")]
    public void ItemPatternMatchesTravelingMerchantItemLocation(
        string input, string expectedDay, string expectedItem)
    {
        var match = TranslationLogic.TravelingMerchantItemPattern.Match(input);
        Assert.True(match.Success, $"Pattern deveria dar match em: '{input}'");
        Assert.Equal(expectedDay, match.Groups[1].Value.Trim());
        Assert.Equal(expectedItem, match.Groups[2].Value.Trim());
    }

    [Theory]
    [InlineData("Traveling Merchant: Monday")]
    [InlineData("Traveling Merchant Stock Size")]
    [InlineData("Traveling Merchant Discount")]
    [InlineData("Traveling Merchant Metal Detector")]
    [InlineData("")]
    [InlineData("Some random string")]
    public void ItemPatternDoesNotMatchNonItemLocations(string input)
    {
        var match = TranslationLogic.TravelingMerchantItemPattern.Match(input);
        Assert.False(match.Success, $"Pattern NÃO deveria dar match em: '{input}'");
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 3. TEMPLATE DE TRADUÇÃO
// ═══════════════════════════════════════════════════════════════════════════
public class TemplateSubstitutionTests
{
    [Theory]
    [InlineData("Traveling Merchant: {{day}}", "Sexta-feira", null,
                "Traveling Merchant: Sexta-feira")]
    [InlineData("Mercadora Viajante: {{day}}", "Sexta-feira", null,
                "Mercadora Viajante: Sexta-feira")]
    [InlineData("Traveling Merchant {{day}} Item {{item}}", "Monday", "1",
                "Traveling Merchant Monday Item 1")]
    [InlineData("Mercadora Viajante {{day}} Item {{item}}", "Segunda-feira", "2",
                "Mercadora Viajante Segunda-feira Item 2")]
    public void ApplyDayTemplateSubstitutesCorrectly(
        string template, string day, string? item, string expected)
    {
        var result = TranslationLogic.ApplyDayTemplate(template, day, item);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ApplyDayTemplateHandlesBothBraceStyles()
    {
        // O código suporta {{day}} e {day} para compatibilidade
        var resultDouble = TranslationLogic.ApplyDayTemplate("{{day}}", "Segunda-feira");
        var resultSingle = TranslationLogic.ApplyDayTemplate("{day}", "Segunda-feira");
        Assert.Equal("Segunda-feira", resultDouble);
        Assert.Equal("Segunda-feira", resultSingle);
    }
}

// ═══════════════════════════════════════════════════════════════════════════
// 4. INTEGRIDADE DOS ARQUIVOS i18n
// ═══════════════════════════════════════════════════════════════════════════
public class I18nIntegrityTests
{
    private static readonly string I18nDir = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "StardewArchipelagoTranslations", "i18n")
    );

    private Dictionary<string, string> LoadJson(string filename)
    {
        var path = Path.Combine(I18nDir, filename);
        Assert.True(File.Exists(path), $"Arquivo não encontrado: {path}");
        return JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(path))!;
    }

    // Keys que o código usa — devem existir em todos os i18n
    public static IEnumerable<object[]> RequiredKeys => new object[][]
    {
        new object[] { "traveling_merchant_day_format" },
        new object[] { "traveling_merchant_item_format" },
        new object[] { "traveling_merchant_stock_size" },
        new object[] { "traveling_merchant_discount" },
        new object[] { "traveling_merchant_metal_detector" },
    };

    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void DefaultJsonContainsRequiredKey(string key)
    {
        var json = LoadJson("default.json");
        Assert.True(json.ContainsKey(key), $"default.json está faltando a key: '{key}'");
    }

    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void PtJsonContainsRequiredKey(string key)
    {
        var json = LoadJson("pt.json");
        Assert.True(json.ContainsKey(key), $"pt.json está faltando a key: '{key}'");
    }

    [Theory]
    [MemberData(nameof(RequiredKeys))]
    public void PtJsonKeyIsNotEmpty(string key)
    {
        var json = LoadJson("pt.json");
        Assert.True(json.TryGetValue(key, out var value));
        Assert.False(string.IsNullOrWhiteSpace(value),
            $"pt.json: key '{key}' está vazia");
    }

    [Fact]
    public void DefaultAndPtJsonAreValidJson()
    {
        // Se não lançar exceção, o JSON está válido
        LoadJson("default.json");
        LoadJson("pt.json");
    }

    [Theory]
    [InlineData("default.json")]
    [InlineData("pt.json")]
    public void NoOldPrefixedTravelingMerchantKeys(string filename)
    {
        var json = LoadJson(filename);
        var badKeys = json.Keys
            .Where(k => k.StartsWith("location.traveling_merchant")
                     || k.StartsWith("item.traveling_merchant"))
            .ToList();

        Assert.Empty(badKeys);
    }

    [Theory]
    [InlineData("default.json")]
    [InlineData("pt.json")]
    public void TravelingMerchantDayFormatContainsPlaceholder(string filename)
    {
        var json = LoadJson(filename);
        Assert.True(json.TryGetValue("traveling_merchant_day_format", out var val));
        Assert.Contains("{{day}}", val);
    }

    [Theory]
    [InlineData("default.json")]
    [InlineData("pt.json")]
    public void TravelingMerchantItemFormatContainsBothPlaceholders(string filename)
    {
        var json = LoadJson(filename);
        Assert.True(json.TryGetValue("traveling_merchant_item_format", out var val));
        Assert.Contains("{{day}}", val);
        Assert.Contains("{{item}}", val);
    }

    [Theory]
    [InlineData("default.json")]
    [InlineData("pt.json")]
    public void NoDuplicateKeys(string filename)
    {
        var path = Path.Combine(I18nDir, filename);
        var raw = File.ReadAllText(path);

        // Conta quantas vezes cada key aparece no texto cru
        var keyPattern = new Regex(@"""([^""]+)""\s*:", RegexOptions.Compiled);
        var keys = keyPattern.Matches(raw).Select(m => m.Groups[1].Value).ToList();
        var duplicates = keys.GroupBy(k => k).Where(g => g.Count() > 1).Select(g => g.Key).ToList();

        Assert.Empty(duplicates);
    }
}
