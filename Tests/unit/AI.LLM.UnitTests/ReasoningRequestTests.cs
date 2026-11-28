using System.Text.Json;
using AI.LLM.API.LLMAPI;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Requests;
using Xunit;

namespace AI.LLM.UnitTests;

/// <summary>
/// Поля рассуждения в запросе: взаимоисключающие (<c>reasoning.effort</c>, <c>reasoning.max_tokens</c>,
/// <c>reasoning_effort</c>) не уходят вместе ни при каком сочетании входа и возможностей модели.
/// </summary>
public sealed class ReasoningRequestTests
{
    private static readonly string[] Exclusive = ["reasoning.effort", "reasoning.max_tokens", "reasoning_effort"];

    /// <summary>Объект рассуждения на входе: нет, уровень, бюджет, только exclude/enabled.</summary>
    private static readonly Func<ReasoningSettings?>[] Objects =
    [
        () => null,
        () => new ReasoningSettings { Effort = "high" },
        () => new ReasoningSettings { Effort = "none" },
        () => new ReasoningSettings { MaxTokens = 2000 },
        () => new ReasoningSettings(maxTokens: 1500, exclude: true),
        () => new ReasoningSettings { Exclude = true, Enabled = true },
    ];

    private static readonly string?[] TopLevels = [null, "", "low"];

    /// <summary>null — возможности неизвестны; дальше все восемь сочетаний флагов.</summary>
    private static readonly ReasoningSupport?[] Supports =
        [null, .. Enumerable.Range(0, 8).Select(flags => (ReasoningSupport?)(ReasoningSupport)flags)];

    public static TheoryData<int, int, int> All()
    {
        var data = new TheoryData<int, int, int>();
        for (int o = 0; o < Objects.Length; o++)
            for (int t = 0; t < TopLevels.Length; t++)
                for (int s = 0; s < Supports.Length; s++)
                    data.Add(o, t, s);
        return data;
    }

    [Theory]
    [MemberData(nameof(All))]
    public void Request_NeverCarriesExclusiveFieldsTogether(int objectCase, int topLevelCase, int supportCase)
    {
        ReasoningSettings? source = Objects[objectCase]();
        string? topLevel = TopLevels[topLevelCase];
        ReasoningSupport? supported = Supports[supportCase];
        var settings = new GenerateSettings { ReasoningSettings = source, ReasoningEffort = topLevel, ReasoningSupport = supported };

        JsonElement body = Body(settings);
        string[] sent = [.. Exclusive.Where(field => Has(body, field))];

        Assert.True(sent.Length <= 1, "Ушли вместе: " + string.Join(", ", sent));

        // Известные возможности: уходит только то, что модель принимает.
        if (supported is { } known)
        {
            if (Has(body, "reasoning.effort")) Assert.True(known.HasFlag(ReasoningSupport.Effort));
            if (Has(body, "reasoning.max_tokens")) Assert.True(known.HasFlag(ReasoningSupport.MaxTokens));
            if (Has(body, "reasoning_effort")) Assert.True(known.HasFlag(ReasoningSupport.TopLevelEffort));
            if (body.TryGetProperty("reasoning", out _))
                Assert.True((known & (ReasoningSupport.Effort | ReasoningSupport.MaxTokens)) != 0);
        }

        // Заданное не теряется молча, если модель может его принять.
        bool asked = !string.IsNullOrEmpty(source?.Effort) || source?.MaxTokens != null || !string.IsNullOrEmpty(topLevel);
        bool levelAccepted = supported is not { } s || (s & (ReasoningSupport.Effort | ReasoningSupport.TopLevelEffort)) != 0;
        bool levelAsked = !string.IsNullOrEmpty(source?.Effort) || !string.IsNullOrEmpty(topLevel);
        if (asked && levelAsked && levelAccepted)
            Assert.Single(sent);

        // max_reasoning_tokens (vLLM) — повтор выбранного бюджета и только его.
        if (body.TryGetProperty("max_reasoning_tokens", out JsonElement mirror))
            Assert.Equal(body.GetProperty("reasoning").GetProperty("max_tokens").GetInt32(), mirror.GetInt32());
        else
            Assert.False(Has(body, "reasoning.max_tokens"));

        // Исходные настройки не меняются.
        Assert.Same(source, settings.ReasoningSettings);
        Assert.Equal(topLevel, settings.ReasoningEffort);
    }

    [Fact]
    public void ObjectLevel_IsMoreExplicit_ThanTopLevelField()
    {
        JsonElement body = Body(new GenerateSettings
        {
            ReasoningSettings = new ReasoningSettings { MaxTokens = 1500 }, ReasoningEffort = "high",
        });

        Assert.Equal(1500, body.GetProperty("reasoning").GetProperty("max_tokens").GetInt32());
        Assert.False(body.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public void KnownSupport_MovesLevelToTheFormTheModelAccepts()
    {
        JsonElement openAi = Body(new GenerateSettings
        {
            ReasoningSettings = new ReasoningSettings { Effort = "high" }, ReasoningSupport = ReasoningSupport.TopLevelEffort,
        });
        JsonElement openRouter = Body(new GenerateSettings
        {
            ReasoningEffort = "low", ReasoningSupport = ReasoningSupport.Effort | ReasoningSupport.MaxTokens,
        });

        Assert.Equal("high", openAi.GetProperty("reasoning_effort").GetString());
        Assert.False(openAi.TryGetProperty("reasoning", out _));
        Assert.Equal("low", openRouter.GetProperty("reasoning").GetProperty("effort").GetString());
        Assert.False(openRouter.TryGetProperty("reasoning_effort", out _));
    }

    [Fact]
    public void Enabled_FollowsTheChosenField_SameForInitializerAndConstructor()
    {
        bool viaInitializer = Body(new GenerateSettings { ReasoningSettings = new ReasoningSettings { Effort = "high" } })
            .GetProperty("reasoning").GetProperty("enabled").GetBoolean();
        bool viaConstructor = Body(new GenerateSettings { ReasoningSettings = new ReasoningSettings(effort: "high") })
            .GetProperty("reasoning").GetProperty("enabled").GetBoolean();
        bool none = Body(new GenerateSettings { ReasoningSettings = new ReasoningSettings { Effort = "none" } })
            .GetProperty("reasoning").GetProperty("enabled").GetBoolean();

        Assert.True(viaInitializer);
        Assert.True(viaConstructor);
        Assert.False(none);
    }

    [Fact]
    public void Pair_IsRefused_ByConstructorAndInitializerAlike()
    {
        Assert.Throws<ArgumentException>(() => new ReasoningSettings(effort: "high", maxTokens: 100));
        Assert.Throws<ArgumentException>(() => new ReasoningSettings { Effort = "high", MaxTokens = 100 });
        Assert.Throws<ArgumentException>(() => new ReasoningSettings { MaxTokens = 100, Effort = "high" });
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReasoningSettings(maxTokens: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ReasoningSettings { MaxTokens = 0 });

        // Снять одно поле и задать другое можно.
        var settings = new ReasoningSettings { Effort = "high" };
        settings.Effort = null;
        settings.MaxTokens = 100;
        Assert.Equal(100, settings.MaxTokens);
    }

    private static JsonElement Body(GenerateSettings settings)
    {
        var data = new SendDataLLM("model", settings);
        data.SetMessages([LLMMessage.CreateMessage(Roles.User, "q")]);
        return JsonDocument.Parse(data.GetJson()).RootElement.Clone();
    }

    private static bool Has(JsonElement body, string path)
    {
        JsonElement node = body;
        foreach (string part in path.Split('.'))
        {
            if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(part, out node))
                return false;
        }

        return node.ValueKind != JsonValueKind.Null;
    }
}
