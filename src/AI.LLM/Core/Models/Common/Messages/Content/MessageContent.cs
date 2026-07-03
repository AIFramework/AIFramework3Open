namespace AI.LLM.Core.Models.Common.Messages.Content;

/// <summary>
/// Содержание контента (тексты, изображения)
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
