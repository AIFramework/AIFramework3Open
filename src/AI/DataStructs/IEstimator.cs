namespace AI.DataStructs;

/// <summary>
/// Обучаемый алгоритм с предсказанием: Fit (обучение) + Predict (инференс).
/// Используется как базовый контракт для классификаторов, регрессоров и кластеризаторов.
/// </summary>
/// <typeparam name="TInput">Тип входных данных (обычно Vector)</typeparam>
/// <typeparam name="TLabel">Тип метки / выхода (int для классификации, double для регрессии)</typeparam>
public interface IEstimator<TInput, TLabel> : IAlgorithm
{
    /// <summary>
    /// Обучение модели на массиве данных и соответствующих метках
    /// </summary>
    /// <param name="data">Массив входных данных</param>
    /// <param name="labels">Массив меток (целевых значений)</param>
    void Fit(TInput[] data, TLabel[] labels);

    /// <summary>
    /// Предсказание метки для одного входного элемента
    /// </summary>
    /// <param name="input">Входной элемент</param>
    /// <returns>Предсказанная метка</returns>
    TLabel Predict(TInput input);
}
