using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.ML.DataHandling.DataSets;

namespace AI.ML.Classification;

/// <summary>
/// Единая точка входа для обучаемых классификаторов с векторными признаками и дискретными метками классов.
/// Наследует <see cref="IEstimator{TInput, TLabel}"/> для совместимости с единой иерархией алгоритмов.
/// </summary>
/// <example>
/// <code>
/// var data = new VectorDataset();
/// data.Add(new VectorDatasetItem(new Vector(0.1, 0.2), 0));
/// IClassifier clf = new BayesianClassifier();
/// clf.Train(data);
/// int label = clf.Classify(new Vector(0.15, 0.18));
/// </code>
/// </example>
public interface IClassifier : IEstimator<Vector, int>, ISavable
{
    /// <summary>
    /// Обучение классификатора
    /// </summary>
    /// <param name="features">Признаки</param>
    /// <param name="classes">Классы</param>
    void Train(Vector[] features, int[] classes);

    /// <summary>
    /// Обучение классификатора
    /// </summary>
    /// <param name="dataset">Набор данных</param>
    void Train(VectorDataset dataset);

    /// <summary>
    /// Распознавание
    /// </summary>
    /// <param name="inp">Вектор который надо распознать</param>
    int Classify(Vector inp);

    /// <summary>
    /// Вектор вероятностей принадлежности к классам
    /// </summary>
    /// <param name="inp">Вектор который надо распознать</param>
    Vector ClassifyProbVector(Vector inp);

    #region IEstimator<Vector, int> — default-реализация через Train/Classify

    /// <inheritdoc/>
    void IEstimator<Vector, int>.Fit(Vector[] data, int[] labels) => Train(data, labels);

    /// <inheritdoc/>
    int IEstimator<Vector, int>.Predict(Vector input) => Classify(input);

    #endregion
}
