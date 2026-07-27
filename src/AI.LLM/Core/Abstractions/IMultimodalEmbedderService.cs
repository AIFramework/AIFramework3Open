using AI.DataStructs.Algebraic;
using AI.LLM.Core.Models.Common.Messages.Content;

namespace AI.LLM.Core.Abstractions;

/// <summary>
/// Эмбеддер, укладывающий разные модальности (текст, изображения) в одно векторное пространство.
/// Позволяет искать картинки текстовым запросом и наоборот.
/// </summary>
public interface IMultimodalEmbedderService : IEmbedderService
{
    /// <summary>
    /// Поддерживает ли текущая модель изображения на входе
    /// </summary>
    bool SupportsImages { get; }

    /// <summary>
    /// Асинхронно генерирует единый вектор для набора частей контента (текст + изображения).
    /// </summary>
    /// <param name="content">Части контента, объединяемые в один эмбеддинг.</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Совместный вектор для всех переданных частей.</returns>
    Task<Vector> EncodeAsync(MessageContent content, CancellationToken cancellationToken = default);

    /// <summary>
    /// Асинхронно генерирует векторы для коллекции мультимодальных элементов.
    /// Каждый элемент коллекции превращается в один вектор.
    /// </summary>
    /// <param name="contents">Коллекция элементов, каждый из которых состоит из частей контента.</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    /// <returns>Массив векторов в порядке следования входных элементов.</returns>
    Task<Vector[]> EncodeAsync(IEnumerable<MessageContent> contents, CancellationToken cancellationToken = default);
}
