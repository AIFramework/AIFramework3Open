using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using AI.Agents.Contracts.Engines;
using AI.Agents.Contracts.Tools;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Перечисления контрактов: пишутся строкой без настройки у вызывающего, ноль — Unknown, неизвестное имя и
/// число при чтении — понятная ошибка.
/// </summary>
public sealed class EnumContractTests
{
    /// <summary>Все публичные перечисления контрактов.</summary>
    public static TheoryData<Type> Enums()
    {
        var data = new TheoryData<Type>();

        foreach (Type type in Repository.Contracts.GetExportedTypes().Where(type => type.IsEnum).OrderBy(type => type.Name))
            data.Add(type);

        return data;
    }

    /// <summary>Ноль каждого перечисления — Unknown: незаданное значение не совпадает ни с одним выбором.</summary>
    [Theory]
    [MemberData(nameof(Enums))]
    public void Enum_ZeroValue_IsUnknown(Type type)
    {
        Assert.Equal("Unknown", Enum.GetName(type, 0));
    }

    /// <summary>Конвертер объявлен на самом перечислении — строгий строковый.</summary>
    [Theory]
    [MemberData(nameof(Enums))]
    public void Enum_DeclaresStrictConverter(Type type)
    {
        Type? converter = type.GetCustomAttribute<JsonConverterAttribute>()?.ConverterType;

        Assert.NotNull(converter);
        Assert.True(converter!.IsGenericType, $"{type.Name}: конвертер {converter.Name} не строгий");
        Assert.Equal("StrictEnumConverter`1", converter.GetGenericTypeDefinition().Name);
        Assert.Equal(type, converter.GetGenericArguments()[0]);
    }

    /// <summary>Каждое значение пишется своим именем и читается обратно; настройки вызывающего не нужны.</summary>
    [Theory]
    [MemberData(nameof(Enums))]
    public void Enum_EveryValue_IsWrittenAsNameAndReadBack(Type type)
    {
        foreach (object value in Enum.GetValues(type))
        {
            string json = JsonSerializer.Serialize(value, type);
            Assert.Equal($"\"{Enum.GetName(type, value)}\"", json);
            Assert.Equal(value, JsonSerializer.Deserialize(json, type));
        }
    }

    /// <summary>Неизвестное имя — ошибка с именем перечисления и пришедшим значением, а не тихий ноль.</summary>
    [Theory]
    [MemberData(nameof(Enums))]
    public void Enum_UnknownName_ThrowsClearError(Type type)
    {
        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("\"NoSuchValue\"", type));

        Assert.Contains(type.Name, error.Message);
        Assert.Contains("NoSuchValue", error.Message);
        Assert.Contains("Unknown", error.Message);
    }

    /// <summary>Число вместо имени не принимается: «0» и «5» не должны молча стать значением.</summary>
    [Theory]
    [MemberData(nameof(Enums))]
    public void Enum_Number_IsRejected(Type type)
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("0", type));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("5", type));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize("\"5\"", type));
    }

    /// <summary>Неизвестное имя внутри записи тоже дает ошибку чтения.</summary>
    [Fact]
    public void Read_UnknownEnumNameInsideRecord_Throws()
    {
        const string json = """{"Name":"x","Description":"d","Parameters":{"type":"object"},"Action":"Delete"}""";

        var error = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<ToolSpec>(json));
        Assert.Contains("Delete", error.Message);
    }

    /// <summary>Необъявленное значение не записывается: в JSON не уходит то, что нельзя прочитать.</summary>
    [Fact]
    public void Write_UndeclaredValue_Throws()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((ToolAction)42));
    }

    /// <summary>Статусы движка различаются: «не доделано» — не сбой и не успех.</summary>
    [Fact]
    public void EngineStatus_Incomplete_DiffersFromFailedAndDone()
    {
        Assert.NotEqual(EngineStatus.Incomplete, EngineStatus.Failed);
        Assert.NotEqual(EngineStatus.Incomplete, EngineStatus.Done);
        Assert.NotEqual(EngineStatus.Failed, EngineStatus.Done);
        Assert.Equal("\"Incomplete\"", JsonSerializer.Serialize(EngineStatus.Incomplete));
        Assert.Equal(EngineStatus.Incomplete, JsonSerializer.Deserialize<EngineStatus>("\"Incomplete\""));
        Assert.NotEqual(EngineStatus.Incomplete, default);
    }
}
