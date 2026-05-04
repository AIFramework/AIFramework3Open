namespace AI.SignalLab.AGC;

public interface IAGC
{
    /// <summary>
    /// Ограничение уровня сигнала в АРУ
    /// </summary>
    double TresholdAGC { get; set; }


    /// <summary>
    /// Ограничение уровня сигнала в фильтрах
    /// </summary>
    double TresholdFilter { get; set; }


    /// <summary>
    /// Расчет выхода АРУ
    /// </summary>
    /// <param name="value">Вход в АРУ</param>
    /// <returns></returns>
    double Calculate(double value);
}
