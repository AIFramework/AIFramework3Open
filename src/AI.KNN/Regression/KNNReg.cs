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
/// Регрессия методом k-ближайших соседей (скалярный выход).
/// Partial sort O(n log K) вместо полного Sort O(n log n).
/// Сериализация через BinarySerializer вместо устаревшего BinaryFormatter.
/// </summary>
[Serializable]
public class KNNReg : IRegression
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
    public StructRegres Reg { get; set; }

    public KNNReg()
    {
        Reg = new StructRegres();
        KernelWindow = r => Math.Exp(-2.0 * r * r);
        Dist = BaseDist.SquareEucl;
    }

    public KNNReg(string path) : this() => Open(path);

    public KNNReg(StructRegres reg) : this() => Reg = reg;

    #region Обучение

    /// <summary>Добавить обучающую точку.</summary>
    public void Train(Vector tData, double targ)
    {
        Reg.Classes.Add(new StructRegr(tData.Clone(), targ));
    }

    /// <summary>Обучить по массивам.</summary>
    public void Train(Vector[] tData, Vector targs)
    {
        for (int i = 0; i < tData.Length; i++)
            Train(tData[i], targs[i]);
    }

    /// <summary>Обучить по вектору x -> вектору y (каждый элемент — отдельная точка).</summary>
    public void Train(Vector tData, Vector targs)
    {
        for (int i = 0; i < tData.Count; i++)
            Train(new Vector(tData[i]), targs[i]);
    }

    #endregion Обучение

    #region Предсказание

    /// <summary>Предсказать одно значение.</summary>
    public double Predict(Vector inp)
    {
        int n = Reg.Classes.Count;
        if (n == 0) throw new InvalidOperationException("Регрессия не обучена.");

        int limit = FixedH && IsNadrMethod ? n : Math.Min(K, n);

        // Вычисляем расстояния
        double[] dists = new double[n];
        for (int i = 0; i < n; i++)
            dists[i] = Dist(inp, Reg.Classes[i].Features);

        // Partial sort — top-limit ближайших
        int[] order = KnnHeap.TopK(dists, limit);
        double h = FixedH && IsNadrMethod ? H : dists[order[limit - 1]];
        if (h < 1e-12) h = 1e-12;

        double pred = 0, w = 0;
        for (int i = 0; i < limit; i++)
        {
            int idx  = order[i];
            double d = dists[idx];
            double mark = Reg.Classes[idx].Target;
            double weight;

            if (IsNadrMethod)
            {
                weight = KernelWindow(d / h);
            }
            else
            {
                weight = 1.0;
            }

            pred += mark * weight;
            w    += weight;
        }

        return w < 1e-12 ? 0 : pred / w;
    }

    /// <summary>Предсказать для каждого элемента вектора.</summary>
    public Vector PredictV(Vector inp)
    {
        double[] res = new double[inp.Count];
        for (int i = 0; i < inp.Count; i++)
            res[i] = Predict(new Vector(inp[i]));
        return new Vector(res);
    }

    #endregion Предсказание

    #region Сохранение / загрузка

    public void Save(string path) => SafeSerializer.Save(path, this, AiKnnJsonOptions.Default);

    public void Open(string path)
    {
        var loaded   = SafeSerializer.Load<KNNReg>(path, AiKnnJsonOptions.Default);
        Reg          = loaded.Reg;
        K            = loaded.K;
        H            = loaded.H;
        FixedH       = loaded.FixedH;
        IsNadrMethod = loaded.IsNadrMethod;
    }
    #endregion Сохранение / загрузка

}
