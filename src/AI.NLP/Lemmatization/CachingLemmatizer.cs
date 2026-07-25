using System;
using System.Collections.Concurrent;
using System.Threading;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Декоратор-кэш: оборачивает любой <see cref="ILemmatizer"/> и запоминает
/// результаты лемматизации отдельных слов. Полезен, когда вызовов много
/// и обёрнутый лемматизатор делает дорогой разбор (регулярки, словарь
/// на диске, ML-модель и т.п.).
/// Потокобезопасен.
/// </summary>
[Serializable]
public sealed class CachingLemmatizer : LemmatizerBase
{
    private readonly ILemmatizer _inner;
    [NonSerialized]
    private ConcurrentDictionary<string, string> _cache;
    private readonly int _maxSize;

    /// <summary>
    /// Создать кэширующий лемматизатор.
    /// </summary>
    /// <param name="inner">Оборачиваемый лемматизатор.</param>
    /// <param name="maxSize">Ограничение на размер кэша (0 или отрицательное — без ограничения).</param>
    public CachingLemmatizer(ILemmatizer inner, int maxSize = 0)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _maxSize = maxSize;
        _cache = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
    }

    /// <summary>
    /// Текущее количество закэшированных слов.
    /// </summary>
    public int CacheSize => _cache?.Count ?? 0;

    /// <summary>
    /// Очистить кэш.
    /// </summary>
    public void ClearCache() => _cache?.Clear();

    /// <summary>
    /// Кэш с восстановлением после десериализации. Через <see cref="Interlocked"/>,
    /// а не простой проверкой на null: два потока могли одновременно увидеть null,
    /// создать по словарю и разойтись по разным экземплярам — часть записей теряла бы
    /// смысл, вопреки заявленной потокобезопасности.
    /// </summary>
    private ConcurrentDictionary<string, string> Cache
    {
        get
        {
            ConcurrentDictionary<string, string> cache = _cache;
            if (cache != null) return cache;

            var created = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
            return Interlocked.CompareExchange(ref _cache, created, null) ?? created;
        }
    }

    /// <inheritdoc />
    public override string Lemmatize(string word)
    {
        if (string.IsNullOrEmpty(word)) return word ?? string.Empty;

        ConcurrentDictionary<string, string> cache = Cache;

        if (cache.TryGetValue(word, out string lemma))
            return lemma;

        lemma = _inner.Lemmatize(word);

        // Простая защита от неограниченного роста: при превышении просто
        // перестаём класть в кэш. Стратегии вытеснения намеренно нет —
        // оставляем реализацию детерминированной и дешёвой.
        // Проверка размера и вставка не атомарны: под нагрузкой кэш может
        // на несколько записей превысить лимит — это осознанный размен на скорость.
        if (_maxSize <= 0 || cache.Count < _maxSize)
            cache.TryAdd(word, lemma);

        return lemma;
    }
}
