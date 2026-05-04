namespace AI.ML.NeuralNetworks.V2.Losses;

/// <summary>
/// Способ редукции тензора потерь до скаляра.
/// </summary>
public enum Reduction
{
    /// <summary>Не редуцировать (возвратить тензор поэлементных потерь).</summary>
    None,
    /// <summary>Среднее по всем элементам.</summary>
    Mean,
    /// <summary>Сумма по всем элементам.</summary>
    Sum,
}
