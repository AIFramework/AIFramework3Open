using System.Text.Json.Serialization;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Часть сообщения модели: текст (<see cref="Text"/>) или картинка (<see cref="Image"/>), у каждой части —
/// свое происхождение.
/// </summary>
/// <remarks>
/// Происхождение у части, а не у сообщения: в одно сообщение пользователя попадают и его просьба, и текст
/// страницы, которую прочитал инструмент. Внешнюю часть реализация порта обрамляет как данные.
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(MessagePart.Text), nameof(MessagePart.Text))]
[JsonDerivedType(typeof(MessagePart.Image), nameof(MessagePart.Image))]
public abstract record MessagePart
{
    /// <summary>Откуда часть: доверенная или внешняя.</summary>
    public required Origin Origin { get; init; }

    /// <summary>Текстовая часть.</summary>
    public sealed record Text : MessagePart
    {
        /// <summary>Текст.</summary>
        public required string Content { get; init; }
    }

    /// <summary>Картинка: байтами (<see cref="Data"/>) или адресом (<see cref="Uri"/>).</summary>
    public sealed record Image : MessagePart
    {
        /// <summary>Тип содержимого, например <c>image/png</c>.</summary>
        public required string ContentType { get; init; }

        /// <summary>Байты картинки; <see langword="null"/>: картинка по <see cref="Uri"/>.</summary>
        public byte[]? Data { get; init; }

        /// <summary>Адрес картинки; <see langword="null"/>: картинка в <see cref="Data"/>.</summary>
        public string? Uri { get; init; }
    }
}
