using System.Text.Json;
using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Звук на входе порта модели: байты или адрес, ровно одно из двух, в коде, через with и при чтении JSON.
/// </summary>
public sealed class AudioPartTests
{
    private static readonly Origin Trusted = new() { Kind = OriginKind.Trusted };

    /// <summary>Звук с байтами и звук с адресом проходят круг через базовый тип части.</summary>
    [Fact]
    public void Audio_WithDataOrUri_SurvivesRoundTrip()
    {
        foreach (MessagePart.Audio audio in new[] { ContractSamples.AudioWithData(), ContractSamples.AudioWithUri() })
        {
            string json = JsonSerializer.Serialize<MessagePart>(audio);
            MessagePart back = JsonSerializer.Deserialize<MessagePart>(json)!;

            Assert.Contains("\"$type\":\"Audio\"", json);
            Assert.IsType<MessagePart.Audio>(back);
            Assert.Empty(ValueComparison.Differences(audio, back));
        }
    }

    /// <summary>Байты и адрес вместе отвергаются в любом порядке, через with и при чтении JSON.</summary>
    [Fact]
    public void Audio_WithBoth_IsRejected()
    {
        var dataFirst = Assert.Throws<ArgumentException>(() => new MessagePart.Audio
        {
            Origin = Trusted, ContentType = "audio/wav", Data = [1], Uri = "https://example.org/a.wav",
        });
        var uriFirst = Assert.Throws<ArgumentException>(() => new MessagePart.Audio
        {
            Origin = Trusted, ContentType = "audio/wav", Uri = "https://example.org/a.wav", Data = [1],
        });
        var viaWith = Assert.Throws<ArgumentException>(() => ContractSamples.AudioWithData() with { Uri = "https://example.org/a.wav" });
        var json = Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<MessagePart>(
            """{"$type":"Audio","Origin":{"Kind":"Trusted"},"ContentType":"audio/wav","Data":"AQ==","Uri":"https://example.org/a.wav"}"""));

        foreach (Exception exception in new Exception[] { dataFirst, uriFirst, viaWith, json })
            Assert.Contains("ровно одно из двух", exception.Message);
    }

    /// <summary>Ни байтов, ни адреса — отказ при чтении JSON и при записи части в сообщение, в том числе после with.</summary>
    [Fact]
    public void Audio_WithNeither_IsRejected()
    {
        var json = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessagePart>(
            """{"$type":"Audio","Origin":{"Kind":"Trusted"},"ContentType":"audio/wav"}"""));
        var nulls = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessagePart.Audio>(
            """{"Origin":{"Kind":"Trusted"},"ContentType":"audio/wav","Data":null,"Uri":null}"""));
        var inMessage = Assert.Throws<ArgumentException>(() => new ModelMessage
        {
            Role = MessageRole.User,
            Parts = [new MessagePart.Audio { Origin = Trusted, ContentType = "audio/wav" }],
        });
        var emptied = Assert.Throws<ArgumentException>(() => new ModelMessage
        {
            Role = MessageRole.User,
            Parts = [ContractSamples.AudioWithData() with { Data = null }],
        });

        foreach (Exception exception in new Exception[] { json, nulls, inMessage, emptied })
            Assert.Contains("не задано ни одного", exception.Message);

        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<ModelMessage>(
            """{"Role":"User","Parts":[{"$type":"Audio","Origin":{"Kind":"Trusted"},"ContentType":"audio/wav"}]}"""));
    }

    /// <summary>Пустые байты и пустой адрес не считаются значением; Origin и ContentType обязательны при чтении.</summary>
    [Fact]
    public void Audio_BlankOrMissingRequired_IsRejected()
    {
        Assert.Throws<ArgumentException>(() => new MessagePart.Audio { Origin = Trusted, ContentType = "audio/wav", Data = [] });
        Assert.Throws<ArgumentException>(() => new MessagePart.Audio { Origin = Trusted, ContentType = "audio/wav", Uri = " " });
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessagePart.Audio>(
            """{"ContentType":"audio/wav","Uri":"https://example.org/a.wav"}"""));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<MessagePart.Audio>(
            """{"Origin":{"Kind":"Trusted"},"Uri":"https://example.org/a.wav"}"""));
    }
}
