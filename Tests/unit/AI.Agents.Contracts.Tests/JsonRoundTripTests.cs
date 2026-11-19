using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Запись и чтение JSON для каждого публичного неабстрактного типа контрактов, сравнение по значению.
/// </summary>
public sealed class JsonRoundTripTests
{
    /// <summary>Настройки вызывающего: по умолчанию и веб (camelCase). Контрактам не нужны свои настройки.</summary>
    private static readonly JsonSerializerOptions[] CallerOptions =
    [
        JsonSerializerOptions.Default,
        JsonSerializerOptions.Web,
    ];

    /// <summary>Публичные неабстрактные типы контрактов, кроме перечислений (их значения перебираются отдельно).</summary>
    private static IEnumerable<Type> ConcreteTypes() =>
        Repository.Contracts.GetExportedTypes()
            .Where(type => !type.IsAbstract && !type.IsInterface && !type.IsEnum)
            .OrderBy(type => type.FullName, StringComparer.Ordinal);

    /// <summary>
    /// Храповик: у каждого публичного неабстрактного типа есть образец, у каждого образца — тип; исключения
    /// только для типов с портами хоста.
    /// </summary>
    [Fact]
    public void Samples_CoverEveryConcreteType()
    {
        var types = ConcreteTypes().ToHashSet();
        var problems = new List<string>();

        foreach (Type type in types)
        {
            if (!ContractSamples.All.ContainsKey(type) && !ContractSamples.RuntimeOnly.ContainsKey(type))
                problems.Add($"{type.FullName}: нет образца в ContractSamples.All — новый тип приходит с образцом");
        }

        foreach (Type type in ContractSamples.All.Keys.Concat(ContractSamples.RuntimeOnly.Keys))
        {
            if (!types.Contains(type))
                problems.Add($"{type.FullName}: образец лежит, а публичного неабстрактного типа нет — удалите образец");
        }

        foreach (Type type in ContractSamples.RuntimeOnly.Keys)
        {
            bool hasPort = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Any(property => property.PropertyType.IsInterface && property.PropertyType.Assembly == Repository.Contracts);

            if (!hasPort)
                problems.Add($"{type.FullName}: в RuntimeOnly, но портов-интерфейсов контрактов у него нет — дайте образец");
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Первый образец каждого типа заполняет все свойства с init.</summary>
    [Fact]
    public void Samples_FirstSampleFillsEveryProperty()
    {
        var problems = new List<string>();

        foreach ((Type type, Func<object>[] samples) in ContractSamples.All)
        {
            object sample = samples[0]();

            if (sample.GetType() != type)
                problems.Add($"{type.Name}: образец имеет тип {sample.GetType().Name}");

            string[] exclusive = ContractSamples.Exclusive.GetValueOrDefault(type, []);
            List<string> unfilled = ValueComparison.UnfilledProperties(sample);

            foreach (string property in unfilled.Except(exclusive))
                problems.Add($"{type.Name}.{property}: не заполнено в первом образце");

            if (exclusive.Length > 0 && exclusive.All(unfilled.Contains))
                problems.Add($"{type.Name}: первый образец не заполняет ни одного из {string.Join(", ", exclusive)}");

            // Каждое взаимоисключающее свойство заполнено хотя бы в одном образце
            foreach (string property in exclusive)
            {
                if (samples.All(next => ValueComparison.UnfilledProperties(next()).Contains(property)))
                    problems.Add($"{type.Name}.{property}: взаимоисключающее свойство не заполнено ни в одном образце");
            }
        }

        foreach (Type type in ContractSamples.Exclusive.Keys.Where(type => !ContractSamples.All.ContainsKey(type)))
            problems.Add($"{type.Name}: записан в Exclusive, но образца нет");

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Каждый образец проходит запись и чтение без потерь: значения равны, повторная запись дает тот же текст.</summary>
    [Fact]
    public void Samples_SurviveJsonRoundTrip()
    {
        var problems = new List<string>();

        foreach (JsonSerializerOptions options in CallerOptions)
        {
            foreach ((Type type, Func<object>[] samples) in ContractSamples.All)
            {
                for (int i = 0; i < samples.Length; i++)
                    problems.AddRange(RoundTrip(samples[i](), type, options).Select(problem => $"{type.Name}#{i}: {problem}"));
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Наследники полиморфных записей проходят круг и через базовый тип: вид восстанавливается по дискриминатору.</summary>
    [Fact]
    public void DerivedSamples_SurviveRoundTripThroughBaseType()
    {
        var problems = new List<string>();
        int checkedCount = 0;

        foreach ((Type type, Func<object>[] samples) in ContractSamples.All)
        {
            Type? baseType = type.BaseType;

            if (baseType is null || !baseType.IsAbstract || baseType.Assembly != Repository.Contracts)
                continue;

            foreach (Func<object> sample in samples)
            {
                checkedCount++;
                problems.AddRange(RoundTrip(sample(), baseType, JsonSerializerOptions.Default)
                    .Select(problem => $"{type.Name} как {baseType.Name}: {problem}"));
            }
        }

        Assert.True(checkedCount > 0);
        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>
    /// Храповик полиморфизма: каждая абстрактная запись контрактов объявляет [JsonPolymorphic], и все ее
    /// публичные наследники перечислены в [JsonDerivedType].
    /// </summary>
    [Fact]
    public void AbstractRecords_DeclareEveryDerivedType()
    {
        Type[] exported = Repository.Contracts.GetExportedTypes();
        var problems = new List<string>();

        foreach (Type baseType in exported.Where(type => type.IsClass && type.IsAbstract && !type.IsSealed))
        {
            if (baseType.GetCustomAttribute<JsonPolymorphicAttribute>() is null)
                problems.Add($"{baseType.Name}: нет [JsonPolymorphic]");

            var declared = baseType.GetCustomAttributes<JsonDerivedTypeAttribute>().Select(attribute => attribute.DerivedType).ToHashSet();

            foreach (Type derived in exported.Where(type => type.BaseType == baseType && !type.IsAbstract))
            {
                if (!declared.Contains(derived))
                    problems.Add($"{baseType.Name}: наследник {derived.Name} не перечислен в [JsonDerivedType]");
            }
        }

        Assert.True(problems.Count == 0, string.Join("\n", problems));
    }

    /// <summary>Пропущенное обязательное поле при чтении — ошибка, а не значение по умолчанию.</summary>
    [Fact]
    public void Read_MissingRequiredMember_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ToolCall>("""{"Id":"1","Name":"x"}"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<EngineResult>("""{"Status":"Done"}"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ToolSpec>(
            """{"Name":"x","Description":"d","Parameters":{"type":"object"}}"""));
    }

    /// <summary>Ответ человека: значения любого вида JSON проходят круг по ключам.</summary>
    [Fact]
    public void Answer_ValuesOfEveryJsonKind_SurviveRoundTrip()
    {
        var answer = new Answer
        {
            QuestionId = "q",
            Values = new Dictionary<string, JsonElement>
            {
                ["text"] = ContractSamples.Json("\"да\""),
                ["number"] = ContractSamples.Json("42"),
                ["flag"] = ContractSamples.Json("true"),
                ["list"] = ContractSamples.Json("""["a",1,null]"""),
                ["object"] = ContractSamples.Json("""{"nested":{"x":1}}"""),
            },
        };

        string json = JsonSerializer.Serialize(answer);
        Answer back = JsonSerializer.Deserialize<Answer>(json)!;

        Assert.Empty(ValueComparison.Differences(answer, back));
        Assert.Equal(JsonValueKind.Array, back.Values!["list"].ValueKind);
        Assert.Null(back.Confirmed);
    }

    /// <summary>Ссылка на агента с версией и без: без версии читается как «действующая» (null).</summary>
    [Fact]
    public void AgentRef_WithAndWithoutVersion_SurvivesRoundTrip()
    {
        var pinned = new AgentRef { Id = "writer", Version = 3 };
        var current = new AgentRef { Id = "writer" };

        Assert.Equal(pinned, JsonSerializer.Deserialize<AgentRef>(JsonSerializer.Serialize(pinned)));
        Assert.Equal(current, JsonSerializer.Deserialize<AgentRef>(JsonSerializer.Serialize(current)));
        Assert.Null(JsonSerializer.Deserialize<AgentRef>("""{"Id":"writer"}""")!.Version);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentRef>("""{"Version":2}"""));
    }

    private static List<string> RoundTrip(object sample, Type type, JsonSerializerOptions options)
    {
        string json;
        object? back;

        try
        {
            json = JsonSerializer.Serialize(sample, type, options);
            back = JsonSerializer.Deserialize(json, type, options);
        }
        catch (Exception exception)
        {
            return [$"{exception.GetType().Name}: {exception.Message}"];
        }

        var problems = ValueComparison.Differences(sample, back);

        if (back is not null && back.GetType() != sample.GetType())
            problems.Add($"после чтения тип {back.GetType().Name}, а был {sample.GetType().Name}");

        string again = JsonSerializer.Serialize(back, type, options);

        if (again != json)
            problems.Add($"повторная запись отличается:\n  {json}\n  {again}");

        return problems;
    }
}
