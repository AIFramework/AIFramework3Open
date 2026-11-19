using System.Text.Json;
using System.Text.Json.Serialization;
using AI.Agents.Contracts.Tools;

namespace AI.Agents.Contracts.Models;

/// <summary>
/// Часть сообщения модели: текст (<see cref="Text"/>), картинка (<see cref="Image"/>) или звук
/// (<see cref="Audio"/>), у каждой части — свое происхождение.
/// </summary>
/// <remarks>
/// Происхождение у части, а не у сообщения: в одно сообщение пользователя попадают и его просьба, и текст
/// страницы, которую прочитал инструмент. Внешнюю часть реализация порта обрамляет как данные.
/// </remarks>
[JsonPolymorphic]
[JsonDerivedType(typeof(MessagePart.Text), nameof(MessagePart.Text))]
[JsonDerivedType(typeof(MessagePart.Image), nameof(MessagePart.Image))]
[JsonDerivedType(typeof(MessagePart.Audio), nameof(MessagePart.Audio))]
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

    /// <summary>
    /// Звук: байтами (<see cref="Data"/>) или адресом (<see cref="Uri"/>), ровно одно из двух.
    /// </summary>
    /// <remarks>
    /// Правило «ровно одно» проверяется при создании: байты и адрес вместе отвергаются сразу, в каком бы порядке их
    /// ни задали, в том числе через <c>with</c>. Отсутствие обоих отвергается при чтении JSON и при записи части в
    /// <see cref="ModelMessage.Parts"/>. Принимает ли модель звук, говорит <see cref="ModelCapabilities.Audio"/>.
    /// </remarks>
    public sealed record Audio : MessagePart, IJsonOnDeserialized
    {
        private readonly byte[]? _data;
        private readonly string? _uri;

        /// <summary>Тип содержимого, например <c>audio/wav</c>.</summary>
        public required string ContentType { get; init; }

        /// <summary>Байты звука; <see langword="null"/>: звук по <see cref="Uri"/>.</summary>
        /// <exception cref="ArgumentException">Массив пустой или уже задан <see cref="Uri"/>.</exception>
        public byte[]? Data
        {
            get => _data;
            init
            {
                if (value is not null)
                {
                    if (value.Length == 0)
                        throw new ArgumentException("Звук: массив байтов пуст; без байтов — null.", nameof(Data));

                    if (_uri is not null)
                        throw new ArgumentException(BothMessage, nameof(Data));
                }

                _data = value;
            }
        }

        /// <summary>Адрес звука; <see langword="null"/>: звук в <see cref="Data"/>.</summary>
        /// <exception cref="ArgumentException">Строка пустая или уже заданы <see cref="Data"/>.</exception>
        public string? Uri
        {
            get => _uri;
            init
            {
                if (value is not null)
                {
                    if (string.IsNullOrWhiteSpace(value))
                        throw new ArgumentException("Звук: адрес пуст; без адреса — null.", nameof(Uri));

                    if (_data is not null)
                        throw new ArgumentException(BothMessage, nameof(Uri));
                }

                _uri = value;
            }
        }

        /// <inheritdoc />
        void IJsonOnDeserialized.OnDeserialized()
        {
            if (_data is null && _uri is null)
                throw new JsonException(NoneMessage);
        }

        /// <summary>Проверка, что задано хотя бы одно из двух; оба сразу исключены уже при создании.</summary>
        internal static void EnsureOne(Audio audio, string paramName)
        {
            if (audio.Data is null && audio.Uri is null)
                throw new ArgumentException(NoneMessage, paramName);
        }

        private const string BothMessage =
            "Звук: заданы и байты (Data), и адрес (Uri), а нужно ровно одно из двух.";

        private const string NoneMessage =
            "Звук: нужны байты (Data) или адрес (Uri), ровно одно из двух, а не задано ни одного.";
    }
}
