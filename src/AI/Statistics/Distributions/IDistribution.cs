using AI.DataStructs.Algebraic;
using System.Collections.Generic;

namespace AI.Statistics.Distributions;

/// <summary>
/// Параметризованное вероятностное распределение. Параметры
/// передаются словарями «имя -> значение», что позволяет легко
/// наследовать одно семейство (например, диагональная гауссиана)
/// для произвольных размерностей.
/// </summary>
/// <remarks>
/// Имена исторически начинаются с <c>Culc*</c>. Рядом добавлены
/// <c>Calc*</c> с таким же контрактом для единообразия (default
/// interface methods делегируют в Culc*). Внешние пользователи могут
/// писать новый код через <c>Calc*</c>, старые реализации продолжают
/// работать без изменений.
/// </remarks>
public interface IDistribution
{
    /// <summary>Плотность в многомерной точке.</summary>
    double CulcProb(Vector x, Dictionary<string, Vector> param_dist);

    /// <summary>Плотность в одномерной точке.</summary>
    double CulcProb(double x, Dictionary<string, double> param_dist);

    /// <summary>Лог-плотность в одномерной точке.</summary>
    double CulcLogProb(double x, Dictionary<string, double> param_dist);

    /// <summary>Лог-плотность в многомерной точке.</summary>
    double CulcLogProb(Vector x, Dictionary<string, Vector> param_dist);

    // ---- Единообразные алиасы (default interface methods). ----
    // Не требуют изменения существующих реализаций.

    /// <summary>Алиас <see cref="CulcProb(Vector, Dictionary{string, Vector})"/>.</summary>
    double CalcProb(Vector x, Dictionary<string, Vector> param_dist) => CulcProb(x, param_dist);

    /// <summary>Алиас <see cref="CulcProb(double, Dictionary{string, double})"/>.</summary>
    double CalcProb(double x, Dictionary<string, double> param_dist) => CulcProb(x, param_dist);

    /// <summary>Алиас <see cref="CulcLogProb(double, Dictionary{string, double})"/>.</summary>
    double CalcLogProb(double x, Dictionary<string, double> param_dist) => CulcLogProb(x, param_dist);

    /// <summary>Алиас <see cref="CulcLogProb(Vector, Dictionary{string, Vector})"/>.</summary>
    double CalcLogProb(Vector x, Dictionary<string, Vector> param_dist) => CulcLogProb(x, param_dist);
}
