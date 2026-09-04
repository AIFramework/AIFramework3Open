namespace AI.NLP.Morphology;

/// <summary>
/// Разбор словоформы: начальная форма и часть речи
/// </summary>
/// <param name="Lemma">Начальная форма</param>
/// <param name="PartOfSpeech">Часть речи</param>
public readonly record struct MorphAnalysis(string Lemma, PartOfSpeech PartOfSpeech)
{
    /// <summary>Запись разбора</summary>
    public override string ToString() => $"{Lemma} ({PartOfSpeech.ToCode()})";
}
