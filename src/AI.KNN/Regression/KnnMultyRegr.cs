using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.Distances;
using AI.KNN.Json;
using AI.ML.Regression;
using System;
using System.IO;
using System.Text.Json.Serialization;

namespace AI.KNN.Regression;

/// <summary>
/// Регрессия методом k-ближайших соседей (векторный выход).
/// Partial sort O(n log K) вместо полного Sort O(n log n).
/// Сериализация через BinarySerializer вместо устаревшего BinaryFormatter.
/// </summary>
[Serializable]
public class KnnMultyRegr : IMultyRegression<Vector>
{
    /// <summary>Число соседей.</summary>
    public int K { get; set; } = 4;

    /// <summary>Ширина окна.</summary>
    public double H { get; set; } = 1.0;

    /// <summary>Фиксирована ли ширина окна.</summary>
    public bool FixedH { get; set; } = false;

    /// <summary>Использовать метод Надарая-Ватсона.</summary>
    public bool IsNadrMethod { get; set; } = false;

    /// <summary>Ядро окна.</summary>
    [JsonIgnore]
    public Func<double, double> KernelWindow { get; set; }

    /// <summary>Функция расстояния.</summary>
    [JsonIgnore]
    public Func<Vector, Vector, double> Dist { get; set; }

    /// <summary>Обучающие данные.</summary>
    public StructRegresMulty Reg { get; set; }

    public KnnMultyRegr()
    {
        Reg = new StructRegresMulty();
        KernelWindow = r => Math.Exp(-2.0 * r * r);
        Dist = BaseDist.SquareEucl;
    }

    public KnnMultyRegr(string path) : this() => Open(path);

    public KnnMultyRegr(StructRegresMulty reg) : this() => Reg = reg;

    #region Обучение

    public void Train(Vector tData, Vector targ)
    {
        Reg.Classes.Add(new StructRegrMulty
        {
            CentGiperSfer = tData.Clone(),
            Targets       = targ
        });
    }

    public void Train(Vector[] tData, Vector[] targs)
    {
        for (int i = 0; i < tData.Length; i++)
            Train(tData[i], targs[i]);
    }

    public void Train(Vector tData, Vector[] targs)
    {
        for (int i = 0; i < tData.Count; i++)
            Train(new Vector(tData[i]), targs[i]);
    }

    #endregion Обучение

    #region Предсказание

    public Vector Predict(Vector inp)
    {
        int n = Reg.Classes.Count;
        if (n == 0) throw new InvalidOperationException("Регрессия не обучена.");

        int limit = FixedH && IsNadrMethod ? n : Math.Min(K, n);

        // Вычисляем расстояния
        double[] dists = new double[n];
        for (int i = 0; i < n; i++)
            dists[i] = Dist(inp, Reg.Classes[i].CentGiperSfer);

        // Partial sort — top-limit ближайших
        int[] order = KnnHeap.TopK(dists, limit);
        double h = FixedH && IsNadrMethod ? H : dists[order[limit - 1]];
        if (h < 1e-12) h = 1e-12;

        int outDim   = Reg.Classes[order[0]].Targets.Count;
        double[] acc = new double[outDim];
        double w     = 0;

        for (int i = 0; i < limit; i++)
        {
            int idx      = order[i];
            double d     = dists[idx];
            Vector mark  = Reg.Classes[idx].Targets;
            double weight = IsNadrMethod ? KernelWindow(d / h) : 1.0;

            for (int j = 0; j < outDim; j++)
                acc[j] += mark[j] * weight;
            w += weight;
        }

        double norm = w < 1e-12 ? 1.0 : w;
        double[] result = new double[outDim];
        for (int j = 0; j < outDim; j++)
            result[j] = acc[j] / norm;

        return new Vector(result);
    }

    public Vector[] PredictV(Vector[] inp)
    {
        var res = new Vector[inp.Length];
        for (int i = 0; i < inp.Length; i++)
            res[i] = Predict(inp[i]);
        return res;
    }

    #endregion Предсказание

    #region Сохранение / загрузка

    public void Save(string path) => SafeSerializer.Save(path, this, AiKnnJsonOptions.Default);

    public void Open(string path)
    {
        var loaded   = SafeSerializer.Load<KnnMultyRegr>(path, AiKnnJsonOptions.Default);
        Reg          = loaded.Reg;
        K            = loaded.K;
        H            = loaded.H;
        FixedH       = loaded.FixedH;
        IsNadrMethod = loaded.IsNadrMethod;
    }
    #endregion Сохранение / загрузка

}
