using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.DataPrepaire.DataNormalizers;

/// <summary>
/// Минимаксная нормализация
/// </summary>
[Serializable]
public class MinimaxNomalizer : Normalizer
{
    /// <summary>
    /// Минимумы по столбцам
    /// </summary>
    public double[] Min { get; set; }
    /// <summary>
    /// Максимумы по столбцам
    /// </summary>
    public double[] Max { get; set; }
    /// <summary>
    /// Размах по столбцам
    /// </summary>
    public double[] MaxMinusMin { get; set; }


    /// <summary>
    /// Восстановление нормализованных данных (Перезапись значений алгебраической структуры)
    /// </summary>
    /// <param name="normalizeData">Нормализованные данные</param>
    public override IAlgebraicStructure<double> Denormalize(IAlgebraicStructure<double> normalizeData)
    {
        if (Min == null) return normalizeData;

        if (normalizeData is Vector)
        {
            return ((normalizeData as Vector) * MaxMinusMin) + Min;
        }

        IAlgebraicStructure<double> dat = normalizeData;

        for (int i = 0; i < normalizeData.Data.Length; i++)
            dat.Data[i] = (normalizeData.Data[i] * MaxMinusMin[i]) + Min[i];

        return dat;
    }

    /// <summary>
    /// Обучение преобразователя
    /// </summary>
    /// <param name="data">Набор данных</param>
    public override void Train(IEnumerable<IAlgebraicStructure<double>> data)
    {
        IAlgebraicStructure<double>[] algebraicStructures = (data is IAlgebraicStructure<double>[]) ? data as IAlgebraicStructure<double>[] : data.ToArray();
        Min = new double[algebraicStructures[0].Shape.Count];
        Max = new double[algebraicStructures[0].Shape.Count];
        MaxMinusMin = new double[algebraicStructures[0].Shape.Count];

        // Инициализация
        double[] dat = algebraicStructures[0].Data;
        for (int i = 0; i < Min.Length; i++)
        {
            Min[i] = dat[i];
            Max[i] = dat[i];
        }


        for (int i = 1; i < algebraicStructures.Length; i++)
        {
            dat = algebraicStructures[i].Data;

            for (int j = 0; j < Min.Length; j++)
            {
                if (Min[j] > dat[j]) Min[j] = dat[j];
                if (Max[j] < dat[j]) Max[j] = dat[j];
            }
        }

        for (int i = 0; i < Min.Length; i++)
        {
            MaxMinusMin[i] = Max[i] - Min[i];
        }
    }


    /// <summary>
    /// Использование преобразователя (Перезапись значений алгебраической структуры)
    /// </summary>
    public override IAlgebraicStructure<double> Transform(IAlgebraicStructure<double> data)
    {
        if (Min == null) return data;

        // Правило нулевого размаха — как у Statistic.MinMaxScale: компонента без размаха переходит в ноль
        IAlgebraicStructure<double> result = data is Vector vector ? vector.Clone() : data;

        for (int i = 0; i < result.Data.Length; i++)
        {
            result.Data[i] = MaxMinusMin[i] > 0 ? (data.Data[i] - Min[i]) / MaxMinusMin[i] : 0.0;
        }

        return result;
    }

}
