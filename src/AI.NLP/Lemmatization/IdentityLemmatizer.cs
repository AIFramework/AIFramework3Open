using System;

namespace AI.NLP.Lemmatization;

/// <summary>
/// Лемматизатор-заглушка: возвращает исходное слово без изменений.
/// Удобен как fallback, для тестов или чтобы выключить лемматизацию,
/// не меняя вызывающий код.
/// </summary>
[Serializable]
public sealed class IdentityLemmatizer : LemmatizerBase
{
    /// <summary>
    /// Единственный экземпляр (лемматизатор stateless).
    /// </summary>
    public static readonly IdentityLemmatizer Instance = new IdentityLemmatizer();

    /// <inheritdoc />
    public override string Lemmatize(string word) => word ?? string.Empty;
}
