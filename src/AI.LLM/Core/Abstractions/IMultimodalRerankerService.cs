using AI.DataStructs.Algebraic;
using AI.LLM.Core.Models.Common.Requests;

namespace AI.LLM.Core.Abstractions;

/// <summary>
/// Реранкер, умеющий оценивать релевантность не только текста, но и изображений
/// (например, страниц документа, отрендеренных в картинки).
/// </summary>
public interface IMultimodalRerankerService
{
    /// <summary>
    /// Принимает ли текущая модель изображения в документах
    /// </summary>
    bool SupportsImages { get; }

    /// <summary>
    /// Оценки релевантности документов запросу, в порядке следования документов на входе.
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    /// <param name="documents">Документы (текст и/или изображение)</param>
    /// <param name="instruct">Инструкция; способ её применения зависит от реализации</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task<Vector> SimsAsync(
        string query,
        IEnumerable<RerankDocument> documents,
        string instruct = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Топ-k документов по релевантности: пары (индекс во входном списке, оценка), по убыванию оценки.
    /// </summary>
    /// <param name="query">Поисковый запрос</param>
    /// <param name="documents">Документы (текст и/или изображение)</param>
    /// <param name="k">Сколько результатов вернуть</param>
    /// <param name="instruct">Инструкция; способ её применения зависит от реализации</param>
    /// <param name="cancellationToken">Токен отмены операции</param>
    Task<List<(int Index, double Score)>> TopKAsync(
        string query,
        IEnumerable<RerankDocument> documents,
        int k = 5,
        string instruct = null,
        CancellationToken cancellationToken = default);
}
