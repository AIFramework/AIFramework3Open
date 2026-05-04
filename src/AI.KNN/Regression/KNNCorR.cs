using AI.DataStructs;
using AI.DataStructs.Algebraic;
using AI.Distances;
using AI.KNN.Json;
using AI.ML.Regression;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace AI.KNN.Regression;

/// <summary>
/// Регрессия k-ближайших соседей с корреляционными весами.
/// Partial sort O(n log K) вместо полного Sort O(n log n).
/// Сериализация через BinarySerializer вместо устаревшего BinaryFormatter.
/// </summary>
[Serializable]
public class KNNCorR : IRegression
{
    /// <summary>Число соседей.</summary>
    public int K { get; set; } = 4;

    /// <summary>Фиксирована ли ширина окна.</summary>
    public bool FixedH { get; set; } = false;

    /// <summary>Инициировать ли мутацию соседей.</summary>
    public bool isMutation = true;

    /// <summary>Число мутировавших точек (счётчик).</summary>
    public int mutCount = 0;

    /// <summary>Функция расстояния.</summary>
    [JsonIgnore]
    public Func<Vector, Vector, double> Dist { get; set; }

    private StructRegres _reges = new StructRegres();

    /// <summary>Обучающие данные.</summary>
    public StructRegres Reg
    {
        get => _reges;
        set => _reges = value;
    }

    public KNNCorR()
    {
        Dist = BaseDist.SquareEucl;
    }

    public KNNCorR(string path) : this() => Open(path);

    public KNNCorR(StructRegres reg) : this() => _reges = reg;

    #region Обучение

    public void Train(Vector tData, double targ)
    {
        if (isMutation)
            AddDataMut(tData, targ);
        else
            AddData(tData, targ);
    }

    public void Train(Vector[] tData, Vector targs)
    {
        for (int i = 0; i < tData.Length; i++)
            Train(tData[i], targs[i]);
    }

    public void Train(Vector tData, Vector targs)
    {
        for (int i = 0; i < tData.Count; i++)
            Train(new Vector(tData[i]), targs[i]);
    }

    private void AddData(Vector tData, double targ)
    {
        _reges.Classes.Add(new StructRegr(tData.Clone(), targ)
        {
            Params = new double[2]
        });
    }

    private void AddDataMut(Vector tData, double targ)
    {
        if (_reges.Classes.Count == 0)
        {
            AddData(tData, targ);
            return;
        }

        // Быстро находим ближайшего соседа без полного Rang
        int n = _reges.Classes.Count;
        double minDist = double.MaxValue, maxDist = 0;
        int nearestIdx = 0;

        for (int i = 0; i < n; i++)
        {
            double d = Dist(tData, _reges.Classes[i].Features);
            if (d < minDist) { minDist = d; nearestIdx = i; }
            if (d > maxDist)   maxDist = d;
        }

        double similarity = Math.Exp(-5.0 * minDist / (maxDist < 1e-12 ? 1e-12 : maxDist));

        if (similarity > 0.97)
        {
            var c = _reges.Classes[nearestIdx];
            c.Features = (c.Features + tData) * 0.5;
            c.Target   = (c.Target + targ) * 0.5;
            mutCount++;
        }
        else
        {
            AddData(tData, targ);
        }
    }

    #endregion Обучение

    #region Предсказание

    public double Predict(Vector inp)
    {
        int n = _reges.Classes.Count;
        if (K <= 0 || n < K)
            throw new InvalidOperationException("Недостаточно обучающих точек для предсказания.");

        // Вычисляем расстояния
        double[] dists = new double[n];
        for (int i = 0; i < n; i++)
            dists[i] = Dist(inp, _reges.Classes[i].Features);

        // Partial sort — top-K ближайших
        int[] order = KnnHeap.TopK(dists, K);

        double h = dists[order[0]];
        for (int i = 1; i < K; i++)
            if (dists[order[i]] > h) h = dists[order[i]];
        if (h < 1e-12) h = 1e-12;

        double pred = 0, w = 0;
        for (int i = 0; i < K; i++)
        {
            int idx     = order[i];
            double d    = dists[idx];
            double weight = Math.Exp(-2.0 * d * d / (h * h));

            // Сохраняем вес для ImpObj (не мутируем Params[1])
            if (_reges.Classes[idx].Params != null && _reges.Classes[idx].Params.Length > 0)
                _reges.Classes[idx].Params[0] = weight;

            pred += _reges.Classes[idx].Target * weight;
            w    += weight;
        }

        return Math.Abs(w) < 1e-12 ? 0 : pred / w;
    }

    public Vector PredictV(Vector inp)
    {
        double[] res = new double[inp.Count];
        for (int i = 0; i < inp.Count; i++)
            res[i] = Predict(new Vector(inp[i]));
        return new Vector(res);
    }

    #endregion Предсказание

    #region Аналитика

    /// <summary>Вектор важности обучающих объектов (накопленные веса).</summary>
    public Vector ImpObj()
    {
        int n = _reges.Classes.Count;
        double[] vs = new double[n];
        for (int i = 0; i < n; i++)
        {
            var p = _reges.Classes[i].Params;
            vs[i] = p != null && p.Length > 1 ? p[1] : 0;
        }
        return new Vector(vs);
    }

    /// <summary>Оставить только n наиболее важных объектов.</summary>
    public void OnlyImp(int n = 60)
    {
        _reges.Classes.Sort((a, b) =>
        {
            double wa = a.Params != null && a.Params.Length > 1 ? a.Params[1] : 0;
            double wb = b.Params != null && b.Params.Length > 1 ? b.Params[1] : 0;
            return wb.CompareTo(wa);
        });

        if (_reges.Classes.Count > n)
            _reges.Classes.RemoveRange(n, _reges.Classes.Count - n);
    }

    #endregion Аналитика

    #region Сохранение / загрузка

    public void Save(string path) => SafeSerializer.Save(path, this, AiKnnJsonOptions.Default);

    public void Open(string path)
    {
        var loaded = SafeSerializer.Load<KNNCorR>(path, AiKnnJsonOptions.Default);
        _reges   = loaded._reges;
        K        = loaded.K;
        FixedH   = loaded.FixedH;
    }
    #endregion Сохранение / загрузка

}
