using AI.DataStructs.Algebraic;
using AI.HighLevelFunctions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AI.ML.Clustering;

/// <summary>
/// Самоорганизующиеся карты Кохонена
/// </summary>
[Serializable]
public class KohonenNet : IClustering
{
    /// <summary>
    /// Функция расстояния
    /// </summary>
    public Func<Vector, Vector, double> DistanceFunction { get; set; } = Distances.BaseDist.EuclideanDistance;

    /// <summary>
    /// Веса сети (центры кластеров в пространстве линейного классификатора)
    /// </summary>
    public Vector[] Centroids { get; set; }
    /// <summary>
    /// Веса смещения
    /// </summary>
    public Vector bias;
    /// <summary>
    /// Neural network setup steps
    /// </summary>
    public int Steps { get; set; } = 50;
    /// <summary>
    /// Начальная скорость обучения
    /// </summary>
    public double Eta0 { get; set; } = 0.2;
    /// <summary>
    /// Финальная скорость обучения (после обучения)
    /// </summary>
    public double EtaFinal { get; private set; }
    /// <summary>
    /// Среднее обучающей выборки (для восстановления исходных координат)
    /// </summary>
    public Vector Mean { get; private set; }
    /// <summary>
    /// Стандартное отклонение обучающей выборки
    /// </summary>
    public Vector Std { get; private set; }

    private readonly int _clusters;
    private readonly Random rnd;

    /// <summary>
    /// Массив кластеров
    /// </summary>
    public Cluster[] Clusters
    {
        get
        {
            Cluster[] cls = new Cluster[Centroids.Length];

            for (int i = 0; i < Centroids.Length; i++)
            {
                cls[i] = new Cluster
                {
                    Centr = Centroids[i],
                    Dataset = new[] { Centroids[i] }
                };
            }

            return cls;
        }
    }

    /// <summary>
    /// Самоорганизующиеся карты Кохонена
    /// </summary>
    public KohonenNet(int clusters, int inpDim, int seed = 1)
    {
        Centroids = new Vector[clusters];
        rnd = new Random(seed);

        for (int i = 0; i < Centroids.Length; i++)
        {
            Centroids[i] = Statistics.Statistic.UniformDistribution(inpDim, rnd);
        }

        _clusters = clusters;
        bias = new Vector(clusters);
    }


    /// <summary>
    /// Классификация
    /// </summary>
    /// <param name="vector"></param>
    /// <returns></returns>
    public int Classify(Vector vector)
    {
        Vector outp = new Vector(Centroids.Length);

        _ = Parallel.For(0, _clusters, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, i =>
        {
            outp[i] = AnalyticGeometryFunctions.Dot(Centroids[i], vector) + bias[i];
        });

        return outp.MaxElementIndex();
    }

    /// <summary>
    /// Массива векторов
    /// </summary>
    public int[] Classify(IEnumerable<Vector> vectors)
    {
        return vectors.Select((vector) => Classify(vector)).ToArray();
    }

    /// <summary>
    /// Обучение и классификация
    /// </summary>
    /// <param name="vect"></param>
    /// <returns></returns>
    public int ClassifyAndTrain(Vector vect)
    {
        double newP = 0.0001, old = 1.0 - newP;
        Vector k = new Vector(_clusters);

        _ = Parallel.For(0, _clusters, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount }, j =>
        {
            k[j] = DistanceFunction(vect, Centroids[j]);
            Centroids[j] = (old * Centroids[j]) - (0.01 * newP * vect);
        });

        int ind = k.MinElementIndex();
        Centroids[ind] += newP * vect;

        return Classify(vect);
    }

    /// <summary>
    /// Обучение сети кохонена
    /// </summary>
    /// <param name="datasetInp"></param>
    /// <param name="param"></param>
    public void Train(Vector[] datasetInp, int param)
    {
        Std  = Statistics.Statistic.EnsembleStd(datasetInp);
        Mean = Vector.Mean(datasetInp);
        Vector[] dataset = new Vector[datasetInp.Length];

        for (int i = 0; i < dataset.Length; i++)
        {
            dataset[i] = (datasetInp[i] - Mean) / Std;
        }

        RunEpoch(dataset);

        // Преобразование весов для линейного классификатора в исходном пространстве
        for (int i = 0; i < Centroids.Length; i++)
        {
            Centroids[i] /= Std;
            bias[i] = -AnalyticGeometryFunctions.Dot(Mean, Centroids[i]);
        }
    }

    /// <summary>
    /// Возвращает центроиды в исходном пространстве данных.
    /// Centroids хранятся в виде весов линейного классификатора (w/std),
    /// поэтому для отображения используем: centroid_orig[j] = Centroids[j] * Std[j]² + Mean[j]
    /// </summary>
    public Vector[] GetOriginalCentroids()
    {
        if (Mean == null || Std == null)
            return Centroids;

        var result = new Vector[Centroids.Length];
        for (int i = 0; i < Centroids.Length; i++)
        {
            result[i] = new Vector(Centroids[i].Count);
            for (int j = 0; j < Centroids[i].Count; j++)
                result[i][j] = Centroids[i][j] * Std[j] * Std[j] + Mean[j];
        }
        return result;
    }

    private void RunEpoch(Vector[] dataset)
    {
        double eta    = Eta0;
        double etaMin = 0.001;
        // Экспоненциальный закон убывания: eta(t) = eta0 * exp(-t / tau)
        // tau выбирается так, чтобы за dataset.Length шагов eta -> etaMin
        double tau = Math.Max(1.0, dataset.Length / Math.Log(Math.Max(1.001, eta / etaMin)));

        Vector k = new Vector(_clusters);

        for (int i = 0; i < dataset.Length; i++)
        {
            // Шаг 1: вычисляем расстояния до всех центроидов (ДО изменения весов)
            for (int j = 0; j < _clusters; j++)
                k[j] = DistanceFunction(dataset[i], Centroids[j]);

            // Шаг 2: победитель (BMU — Best Matching Unit)
            int ind = k.MinElementIndex();

            // Шаг 3: обновление весов (WTA + слабое отталкивание проигравших)
            double old = 1.0 - eta;
            for (int j = 0; j < _clusters; j++)
            {
                if (j == ind)
                    // Победитель притягивается к входу
                    Centroids[j] = old * Centroids[j] + eta * dataset[i];
                else
                    // Проигравшие слегка отталкиваются
                    Centroids[j] = old * Centroids[j] - 0.05 * eta * dataset[i];
            }

            // Шаг 4: экспоненциальное убывание, не ниже etaMin
            eta = Math.Max(etaMin, eta * Math.Exp(-1.0 / tau));
        }

        EtaFinal = eta;
    }


}
