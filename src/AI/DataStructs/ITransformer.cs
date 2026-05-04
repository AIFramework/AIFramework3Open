namespace AI.DataStructs;

/// <summary>
/// Алгоритм преобразования данных: Fit (обучение) + Transform (преобразование).
/// Используется для PCA, TF-IDF, нормализаторов и аналогичных алгоритмов.
/// </summary>
/// <typeparam name="TInput">Тип входных данных</typeparam>
/// <typeparam name="TOutput">Тип преобразованных данных</typeparam>
public interface ITransformer<TInput, TOutput> : IAlgorithm
{
    /// <summary>
    /// Обучение преобразования на массиве данных
    /// </summary>
    /// <param name="data">Массив входных данных</param>
    void Fit(TInput[] data);

    /// <summary>
    /// Преобразование одного входного элемента
    /// </summary>
    /// <param name="input">Входной элемент</param>
    /// <returns>Преобразованный элемент</returns>
    TOutput Transform(TInput input);

    /// <summary>
    /// Преобразование массива входных элементов
    /// </summary>
    /// <param name="data">Массив входных данных</param>
    /// <returns>Массив преобразованных данных</returns>
    TOutput[] Transform(TInput[] data)
    {
        var result = new TOutput[data.Length];
        for (int i = 0; i < data.Length; i++)
            result[i] = Transform(data[i]);
        return result;
    }
}
