using AI.DataStructs.Algebraic;
using AI.ML.Regression;
using System;

namespace AI.ML.SequenceAnalysis.SeqPredict;

/// <summary>
/// Прогнозирование последовательности
/// </summary>
[Serializable]
public class SeqPrediction : ISeqPredict
{
    /// <summary>
    /// Регрессионный алгоритм
    /// </summary>
    protected IRegression regressor;
    /// <summary>
    /// Размер окна
    /// </summary>
    protected int window;

    /// <summary>
    /// Прогнозирование последовательности
    /// </summary>
    /// <param name="reg">Алгоритм прогнозирования</param>
    /// <param name="w">Размер окна</param>
    public SeqPrediction(IRegression reg, int w)
    {
        regressor = reg;
        window = w;
    }

    /// <summary>
    /// Прогнозирование последовательности
    /// </summary>
    public SeqPrediction() { }

    /// <summary>
    /// Обучение модели
    /// </summary>
    /// <param name="data">Последовательность</param>
    public void Train(Vector data)
    {
        Vector seq = data.Clone();
        Vector Y = new Vector(seq.Count - window);

        for (int i = window; i < seq.Count; i++)
            Y[i - window] = seq[i];

        Vector[] Xall = Vector.GetWindows(seq, window, 1);
        // GetWindows даёт Count-w+1 окон; целей Count-w — берём первые Count-w.
        Vector[] X = new Vector[Y.Count];
        for (int i = 0; i < Y.Count; i++)
            X[i] = Xall[i];
        regressor.Train(X, Y);
    }

    /// <summary>
    /// Прогноз
    /// </summary>
    /// <param name="data">Известная часть последовательности</param>
    /// <param name="n">Сколько шагов предсказать</param>
    public Vector Predict(Vector data, int n)
    {
        Vector outp = new Vector(data);

        for (int i = 0; i < n; i++)
        {
            int end = outp.Count;
            int start = end - window;
            outp.Add(regressor.Predict(outp.GetInterval(start, end)));
        }

        return outp;
    }

    /// <summary>
    /// Метод не реализован
    /// </summary>
    /// <param name="data"></param>
    /// <returns></returns>
    /// <exception cref="System.NotImplementedException"></exception>
    public double PredictTrain(Vector data)
    {
        throw new System.NotImplementedException();
    }
}
