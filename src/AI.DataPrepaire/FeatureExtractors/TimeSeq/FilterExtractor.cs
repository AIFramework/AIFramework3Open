using AI.DataStructs.Algebraic;
using AI.DSP.IIR;
using AI.Statistics;
using System;

namespace AI.DataPrepaire.FeatureExtractors.TimeSeq;

/// <summary>
/// Экстрактор признаков на базе фильтрации
/// </summary>
[Serializable]
public class FilterExtractor : TSExtractor
{
    /// <summary>
    /// Функция преобразование фильрованного сигнала в число
    /// </summary>
    public Func<Vector, double> Transformer { get; set; }

    /// <summary>
    /// Банк БИХ фильтров
    /// </summary>
    public IIRFilter[] IIRs { get; set; }

    /// <summary>
    /// Экстрактор признаков на базе фильтрации
    /// </summary>
    /// <param name="fCutDown">Нижние частоты среза</param>
    /// <param name="fCutUp">Верхние частоты среза</param>
    /// <param name="sr">Частота дискретизации</param>
    /// <param name="order">Порядок фильтров</param>
    public FilterExtractor(double[] fCutDown, double[] fCutUp, double sr, int order = 5)
    {

        Transformer = Statistic.CalcVariance;

        IIRs = new IIRFilter[fCutDown.Length];

        for (int i = 0; i < fCutDown.Length; i++)
        {
            // Расчет фильтров
            IIRs[i] = IirDesign.Design(IirFamily.Butterworth, IirBandType.BandPass, order, sr, new Vector(fCutDown[i], fCutUp[i]))
                .Sections.ToIirFilter();
        }
    }



    /// <summary>
    /// Возвращает признаки после банка фильтров
    /// </summary>
    public override Vector GetFeatures(Vector crop)
    {
        Vector features = new Vector(IIRs.Length);

        for (int i = 0; i < features.Count; i++)
            features[i] = Transformer(IIRs[i].FilterOutp(crop));

        return features;
    }
}
