namespace AI.LLM.Core.Models.Common.Messages.Content;

/// <summary>
/// Содержание контента (тексты, изображения, звук)
/// </summary>
[Serializable]
public class MessageContent : List<IContentItem>
{
    public MessageContent() { }

    public MessageContent(string content)
    {
        TextContentItem textContent = new TextContentItem();
        textContent.Text = content;
        Add(textContent);
    }

    public void AddImage(string url)
    {
        ImageContent imageContent = new ImageContent(url);
        Add(imageContent);
    }


    public void AddImage(IEnumerable<byte> image)
    {
        ImageContent imageContent = new ImageContent(image);
        Add(imageContent);
    }


    /// <param name="format">Контейнер записи: <c>wav</c>, <c>mp3</c>, <c>ogg</c>, <c>flac</c>, <c>m4a</c>.</param>
    public void AddAudio(string base64, string format)
    {
        AudioContent audioContent = new AudioContent(base64, format);
        Add(audioContent);
    }


    public void AddAudio(IEnumerable<byte> audio)
    {
        AudioContent audioContent = new AudioContent(audio);
        Add(audioContent);
    }


    public void AddText(string text)
    {
        TextContentItem textContent = new TextContentItem(text);
        Add(textContent);
    }


    public override string ToString()
    {
        // Склеиваем все текстовые части через перенос строки (пустая строка, если текстов нет)
        return string.Join("\n", this
            .OfType<TextContentItem>()
            .Select(item => item.Text)
            .Where(text => text != null));
    }
}
