using AI.Agents.Contracts.Models;
using AI.Agents.Contracts.Tools;
using AI.Agents.Interop.Models;
using AI.LLM.Core.Models.Common.Messages;
using AI.LLM.Core.Models.Common.Messages.Content;
using AI.LLM.Core.Models.Common.Requests;
using Xunit;

namespace AI.Agents.Interop.Tests.Models;

/// <summary>Запрос контракта в старой форме: сообщения, части, инструменты, схема, отказы до отправки.</summary>
public sealed class ModelRequestTests
{
    [Fact]
    public void Roles_AndPlainText()
    {
        List<LLMMessage> messages = ModelTranslation.ToLlmMessages(
        [
            ModelKit.Text(MessageRole.System, "правила"),
            ModelKit.Text(MessageRole.User, "вопрос"),
            ModelKit.Text(MessageRole.Assistant, "ответ"),
        ]);

        Assert.Equal(["system", "user", "assistant"], messages.Select(m => m.Role));
        Assert.Equal(["правила", "вопрос", "ответ"], messages.Select(m => m.Content as string));
    }

    [Fact]
    public void UnknownRole_IsRejected() =>
        Assert.Throws<ArgumentException>(() => ModelKit.Text(MessageRole.Unknown, "x").ToLlmMessage());

    [Fact]
    public void Cache_MarksLastTextPart()
    {
        var message = new ModelMessage
        {
            Role = MessageRole.System,
            Parts =
            [
                new MessagePart.Text { Content = "a", Origin = ModelKit.Trusted },
                new MessagePart.Text { Content = "b", Origin = ModelKit.Trusted },
            ],
            Cache = true,
        };

        var content = Assert.IsType<MessageContent>(message.ToLlmMessage().Content);

        Assert.Null(((TextContentItem)content[0]).CacheControl);
        Assert.NotNull(((TextContentItem)content[1]).CacheControl);
        Assert.True(message.ToLlmMessage().ToModelMessage().Cache);
    }

    [Fact]
    public void ImageAndAudio_InUserMessage()
    {
        var message = new ModelMessage
        {
            Role = MessageRole.User,
            Parts =
            [
                new MessagePart.Text { Content = "что тут?", Origin = ModelKit.Trusted },
                new MessagePart.Image { ContentType = "image/png", Data = [1, 2, 3], Origin = ModelKit.Trusted },
                new MessagePart.Image { ContentType = "image/jpeg", Uri = "https://example.com/a.jpg", Origin = ModelKit.Trusted },
                new MessagePart.Audio { ContentType = "audio/mpeg", Data = [4, 5], Origin = ModelKit.Trusted },
            ],
        };

        var content = Assert.IsType<MessageContent>(message.ToLlmMessage().Content);

        Assert.Equal("data:image/png;base64,AQID", ((ImageContent)content[1]).ImageUrl.Url);
        Assert.Equal("https://example.com/a.jpg", ((ImageContent)content[2]).ImageUrl.Url);
        Assert.Equal("mp3", ((AudioContent)content[3]).InputAudio.Format);

        ModelMessage back = message.ToLlmMessage().ToModelMessage();
        var image = Assert.IsType<MessagePart.Image>(back.Parts![1]);
        Assert.Equal("image/png", image.ContentType);
        Assert.Equal([1, 2, 3], image.Data);
        Assert.Equal("image/jpeg", Assert.IsType<MessagePart.Image>(back.Parts[2]).ContentType);
        var audio = Assert.IsType<MessagePart.Audio>(back.Parts[3]);
        Assert.Equal("audio/mpeg", audio.ContentType);
        Assert.Equal([4, 5], audio.Data);
        Assert.All(back.Parts, part => Assert.Equal(OriginKind.Unknown, part.Origin.Kind));
    }

    [Theory]
    [InlineData(MessageRole.Tool)]
    [InlineData(MessageRole.Assistant)]
    [InlineData(MessageRole.System)]
    public void Media_OutsideUserMessage_IsRejectedBeforeSending(MessageRole role)
    {
        var message = new ModelMessage
        {
            Role = role,
            Parts = [new MessagePart.Image { ContentType = "image/png", Data = [1], Origin = ModelKit.Trusted }],
        };

        Assert.Throws<NotSupportedException>(() => message.ToLlmMessage());
    }

    [Fact]
    public async Task Media_OutsideUserMessage_ThrowsFromPortAndSendsNothing()
    {
        var client = new FakeClient();
        IModelPort port = client.AsModelPort(ModelKit.Capabilities(), log: new CapturingLog().Logger);
        var tool = new ModelMessage
        {
            Role = MessageRole.Tool,
            ToolCallId = "c1",
            Parts = [new MessagePart.Image { ContentType = "image/png", Data = [1], Origin = ModelKit.Trusted }],
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => port.AskAsync(ModelKit.Ask(tool)));
        Assert.Throws<NotSupportedException>(() => port.StreamAsync(ModelKit.Ask(tool)));
        Assert.Empty(client.Messages);
    }

    [Fact]
    public void AudioByAddress_IsRejected()
    {
        var message = new ModelMessage
        {
            Role = MessageRole.User,
            Parts = [new MessagePart.Audio { ContentType = "audio/wav", Uri = "https://example.com/a.wav", Origin = ModelKit.Trusted }],
        };

        Assert.Throws<NotSupportedException>(() => message.ToLlmMessage());
    }

    [Fact]
    public void ExternalText_GoesAsIs()
    {
        var message = new ModelMessage
        {
            Role = MessageRole.User,
            Parts = [new MessagePart.Text { Content = "текст страницы", Origin = new Origin { Kind = OriginKind.External } }],
        };

        Assert.Equal("текст страницы", message.ToLlmMessage().Content);
    }

    [Fact]
    public void ToolModes()
    {
        ToolSpec[] tools = [Kit.Spec("search"), Kit.Spec("fetch")];
        ModelAsk ask = ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")) with { Tools = tools };

        Assert.Equal("auto", ask.ToSettings().ToolChoice!.Mode);
        Assert.Equal("none", (ask with { ToolMode = ToolMode.None }).ToSettings().ToolChoice!.Mode);
        Assert.Equal("required", (ask with { ToolMode = ToolMode.Required }).ToSettings().ToolChoice!.Mode);
        Assert.Equal("fetch", (ask with { ToolMode = ToolMode.Named, ToolName = "fetch" }).ToSettings().ToolChoice!.FunctionName);

        GenerateSettings settings = ask.ToSettings();
        Assert.Equal(["search", "fetch"], settings.Tools!.Select(t => t.Function.Name));
        Assert.Contains("\"q\"", settings.Tools![0].Function.Parameters!.Value.GetRawText());
    }

    [Fact]
    public void ToolModes_Invalid_AreRejected()
    {
        ModelAsk bare = ModelKit.Ask(ModelKit.Text(MessageRole.User, "q"));

        Assert.Throws<ArgumentException>(() => (bare with { ToolMode = ToolMode.Unknown }).ToSettings());
        Assert.Throws<ArgumentException>(() => (bare with { ToolMode = ToolMode.Required }).ToSettings());
        Assert.Throws<ArgumentException>(() => (bare with { ToolMode = ToolMode.Named, ToolName = "x", Tools = [Kit.Spec("y")] }).ToSettings());
        Assert.Throws<ArgumentException>(() => (bare with { ToolName = "x", Tools = [Kit.Spec("x")] }).ToSettings());
        Assert.Null(bare.ToSettings().ToolChoice);
    }

    [Fact]
    public void Schema_AndMaxTokens_OverBaseSettings()
    {
        var baseSettings = new GenerateSettings(temperature: 0.7, maxTokens: 500)
        {
            ResponseFormat = ResponseFormat.CreateJsonObject(),
        };
        ModelAsk ask = ModelKit.Ask(ModelKit.Text(MessageRole.User, "q")) with
        {
            Schema = Kit.Json(Kit.QuerySchema),
            MaxTokens = 64,
        };

        GenerateSettings settings = ask.ToSettings(baseSettings);

        Assert.Equal("json_schema", settings.ResponseFormat!.Type);
        Assert.Equal(ModelTranslation.SchemaName, settings.ResponseFormat.JsonSchema.Name);
        Assert.Equal(64, settings.MaxTokens);
        Assert.Equal(0.7, settings.Temperature);
        Assert.Equal(500, baseSettings.MaxTokens);
        Assert.Equal("json_object", baseSettings.ResponseFormat!.Type);

        Assert.Equal(500, (ask with { MaxTokens = null, Schema = null }).ToSettings(baseSettings).MaxTokens);
        Assert.Null((ask with { Schema = null }).ToSettings(baseSettings).ResponseFormat);
    }

    [Fact]
    public async Task Port_SendsTranslatedRequest()
    {
        var client = new FakeClient();
        IModelPort port = client.AsModelPort(ModelKit.Capabilities(), new GenerateSettings(temperature: 0.2), new CapturingLog().Logger);

        await port.AskAsync(ModelKit.Ask(ModelKit.Text(MessageRole.System, "s"), ModelKit.Text(MessageRole.User, "u")) with
        {
            Tools = [Kit.Spec("search")],
            MaxTokens = 99,
        });

        Assert.Equal(["system", "user"], client.Messages.Select(m => m.Role));
        Assert.Equal(0.2, client.Settings!.Temperature);
        Assert.Equal(99, client.Settings.MaxTokens);
        Assert.Equal("search", Assert.Single(client.Settings.Tools!).Function.Name);
        Assert.False(client.Streamed);
    }

    [Fact]
    public void Describe_GivesCapabilitiesForAnyRole()
    {
        ModelCapabilities capabilities = ModelKit.Capabilities() with { ContextTokens = 128_000 };
        IModelPort port = new FakeClient().AsModelPort(capabilities);

        Assert.Same(capabilities, port.Describe(ModelRole.Judge));
        Assert.Same(capabilities, port.Describe(new ModelRole("своя")));
    }
}
