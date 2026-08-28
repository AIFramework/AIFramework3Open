using AI.Script.Binding;
using AI.Script.Docs;
using AI.Script.Hosting;
using System.Text.Json;

namespace AI.Script.UnitTests;

/// <summary>
/// Манифест возможностей: то, по чему модель узнаёт, что язык умеет.
/// </summary>
/// <remarks>
/// Манифест выводится из тех же объектов, что и вызов, — эти тесты и закрепляют, что он не
/// может разойтись с реальностью: сигнатуры в нём совпадают с сигнатурами реестра.
/// </remarks>
public sealed class ManifestTests
{
    private static ScriptHost Host() => Script.Host();

    [Fact]
    public void Manifest_Index_ListsEveryNamespaceAndNoFunctions()
    {
        string text = Host().DescribeCapabilities(ManifestOptions.Index);

        foreach (IScriptModule module in Host().Registry.Modules)
            Assert.Contains($"**{module.Name}**", text, StringComparison.Ordinal);

        Assert.DoesNotContain("math.sqrt(", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// Индекс обязан помещаться в системный промпт — на полном хосте, а не на урезанном.
    /// </summary>
    /// <remarks>
    /// Мерится хост со всеми пространствами: именно он уходит модели. Проверка на неполном
    /// хосте показывала бы запас, которого в жизни нет, и бюджет ломался бы ровно в тот
    /// момент, когда подключают последний модуль.
    /// <para>
    /// Потолок — примерно 700 токенов. Он не круглое число ради круглого числа: индекс
    /// соседствует в промпте с карточкой языка на полторы-две тысячи токенов, и пока он
    /// заметно меньше её, двухуровневая схема работает. Полный манифест на четыре сотни
    /// функций — это десятки тысяч токенов, то есть на два порядка больше; ровно поэтому он
    /// и запрашивается отдельно.
    /// </para>
    /// </remarks>
    [Fact]
    public void Manifest_Index_IsSmallEnoughForAPrompt()
    {
        string text = Script.FullHost().DescribeCapabilities(ManifestOptions.Index);

        Assert.True(text.Length < 2800, $"индекс разросся до {text.Length} символов");
    }

    [Fact]
    public void Manifest_Full_ContainsSignaturesAndExamples()
    {
        string text = Host().DescribeCapabilities(new ManifestOptions
        {
            Namespaces = ["math"],
            IncludeExamples = true,
        });

        Assert.Contains("math.clamp(x: num, low: num = 0, high: num = 1) -> num", text, StringComparison.Ordinal);
        Assert.Contains("math.clamp(x, low: 0, high: 1)", text, StringComparison.Ordinal);
        Assert.DoesNotContain("table.select", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_Compact_IsOneSignaturePerLine()
    {
        string text = Host().DescribeCapabilities(new ManifestOptions
        {
            Format = ManifestFormat.Compact,
            Namespaces = ["re"],
        });

        string[] lines = text.Split('\n');

        Assert.Equal(5, lines.Length);

        foreach (string line in lines) Assert.StartsWith("re.", line, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_Json_IsValidAndCarriesParameters()
    {
        string json = Host().DescribeCapabilities(new ManifestOptions
        {
            Format = ManifestFormat.Json,
            Namespaces = ["math"],
        });

        using JsonDocument document = JsonDocument.Parse(json);

        JsonElement namespaces = document.RootElement.GetProperty("namespaces");
        Assert.Equal(1, namespaces.GetArrayLength());

        JsonElement module = namespaces[0];
        Assert.Equal("math", module.GetProperty("name").GetString());
        Assert.True(module.GetProperty("count").GetInt32() > 10);

        JsonElement functions = module.GetProperty("functions");
        bool found = false;

        foreach (JsonElement function in functions.EnumerateArray())
        {
            if (function.GetProperty("name").GetString() != "math.clamp") continue;

            found = true;
            Assert.Equal("num", function.GetProperty("returns").GetString());

            JsonElement parameters = function.GetProperty("parameters");
            Assert.Equal(3, parameters.GetArrayLength());
            Assert.True(parameters[0].GetProperty("required").GetBoolean());
            Assert.False(parameters[1].GetProperty("required").GetBoolean());
        }

        Assert.True(found, "функция math.clamp не найдена в манифесте");
    }

    [Fact]
    public void Manifest_Json_EscapesQuotesInExamples()
    {
        string json = Host().DescribeCapabilities(new ManifestOptions
        {
            Format = ManifestFormat.Json,
            Namespaces = ["str"],
            IncludeExamples = true,
        });

        using JsonDocument document = JsonDocument.Parse(json);

        Assert.Equal("str", document.RootElement.GetProperty("namespaces")[0].GetProperty("name").GetString());
    }

    [Fact]
    public void Manifest_Truncation_IsAnnounced()
    {
        // Молчаливое усечение читается как исчерпывающий список: модель «узнаёт», что нужной
        // функции не существует.
        string text = Host().DescribeCapabilities(new ManifestOptions
        {
            Format = ManifestFormat.Compact,
            Namespaces = ["math"],
            MaxFunctions = 3,
        });

        Assert.Contains("ещё", text, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_SignaturesMatchRegistry()
    {
        ScriptHost host = Host();
        string text = host.DescribeCapabilities(new ManifestOptions { Format = ManifestFormat.Compact });

        foreach (IScriptModule module in host.Registry.Modules)
        {
            foreach (ScriptFunction function in module.Functions)
                Assert.Contains(function.Signature, text, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("kmeans", 0)]
    [InlineData("sqrt", 1)]
    [InlineData("корреляц", 1)]
    [InlineData("группир", 1)]
    [InlineData("матриц", 1)]
    public void Manifest_Search_FindsByNameAndDescription(string query, int minimum)
    {
        IReadOnlyList<ManifestMatch> matches = Host().Search(query);

        Assert.True(matches.Count >= minimum, $"по запросу '{query}' найдено {matches.Count}");
    }

    [Fact]
    public void Manifest_Search_RanksExactNameFirst()
    {
        IReadOnlyList<ManifestMatch> matches = Host().Search("mean");

        Assert.NotEmpty(matches);
        Assert.Contains("mean", matches[0].Function.Name, StringComparison.Ordinal);
    }

    [Fact]
    public void Manifest_Search_ToleratesTypo()
    {
        IReadOnlyList<ManifestMatch> matches = Host().Search("sqr");

        Assert.Contains(matches, match => match.Function.FullName == "math.sqrt");
    }

    [Fact]
    public void Manifest_Search_RespectsLimit()
    {
        Assert.True(Host().Search("a", limit: 3).Count <= 3);
    }

    [Fact]
    public void Script_FindFn_IsAvailableFromScript()
    {
        RunResult result = Script.RunOk("emit r = find_fn(\"корреляция\", limit: 3)");
        var matches = Assert.IsType<List<object?>>(result.Emitted["r"]);

        Assert.NotEmpty(matches);

        var first = Assert.IsType<Dictionary<string, object?>>(matches[0]);
        Assert.Contains("name", first.Keys);
        Assert.Contains("signature", first.Keys);
    }

    [Fact]
    public void Host_Describe_MatchesHelpFunction()
    {
        ScriptHost host = Host();

        Assert.Equal(host.Describe("math.sqrt"), Script.Text("help(\"math.sqrt\")"));
    }
}
