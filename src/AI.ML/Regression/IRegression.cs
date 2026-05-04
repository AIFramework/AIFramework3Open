using AI.DataStructs;
using AI.DataStructs.Algebraic;

namespace AI.ML.Regression;

/// <summary>
/// Единая точка входа для регрессии с вещественным скалярным откликом по вектору признаков.
/// Наследует <see cref="IEstimator{TInput, TLabel}"/> для совместимости с единой иерархией алгоритмов.
/// </summary>
/// <example>
/// <code>
/// var xs = new Vector[] { new Vector(1, 0), new Vector(2, 0), new Vector(3, 0) };
/// var ys = new Vector(2, 4, 6);
/// IRegression model = new MultipleRegression();
/// model.Train(xs, ys);
/// double y = model.Predict(new Vector(1.5, 0));
/// </code>
/// </example>
public interface IRegression : IAlgorithm
{
    /// <summary>
    /// Обучение регрессии
    /// </summary>
    /// <param name="data">Входные векторы (признаки)</param>
    /// <param name="targets">Выходной вектор (целевые значения)</param>
    void Train(Vector[] data, Vector targets);

    /// <summary>
    /// Предсказание на базе модели
    /// </summary>
    /// <param name="data">Вектор признаков</param>
    double Predict(Vector data);
}

/// <summary>
/// Типизированный алиас для регрессии, совместимый с <see cref="IEstimator{TInput, TLabel}"/>.
/// Новые регрессоры рекомендуется реализовывать через этот интерфейс.
/// </summary>
public interface IRegressor : IRegression, IEstimator<Vector, double>
{
    #region IEstimator<Vector, double> — default-реализация через Train/Predict

    /// <inheritdoc/>
    void IEstimator<Vector, double>.Fit(Vector[] data, double[] labels)
    {
        Train(data, new Vector(labels));
    }

    /// <inheritdoc/>
    double IEstimator<Vector, double>.Predict(Vector input) => Predict(input);

    #endregion
}
