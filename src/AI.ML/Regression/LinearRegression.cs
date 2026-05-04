using AI.DataStructs.Algebraic;
using AI.ML.Regression;
using AI.Statistics;
using System;

namespace AI.ML.Regression;

/// <summary>
/// Модель для линейной регрессии: f(x) = Slope * x + Intercept
/// </summary>
[Serializable]
public class LinearRegressionModel
{
    /// <summary>
    /// Тангенс угла наклона (коэффициент при x)
    /// </summary>
    public double Slope { get; set; }

    /// <summary>
    /// Смещение (свободный член)
    /// </summary>
    public double Intercept { get; set; }

    /// <summary>Тангенс угла наклона (устаревший алиас)</summary>
    [Obsolete("Используйте Slope")]
    public double k { get => Slope; set => Slope = value; }

    /// <summary>Смещение (устаревший алиас)</summary>
    [Obsolete("Используйте Intercept")]
    public double b { get => Intercept; set => Intercept = value; }
}


/// <summary>
/// Линейная регрессия y = Slope * x + Intercept.
/// Реализует <see cref="IRegressor"/> для совместимости с единой иерархией.
/// </summary>
public class LinearRegression : IRegressor
{
    /// <summary>
    /// Параметры линейной регрессии
    /// </summary>
    public LinearRegressionModel Lrm { get; set; }

    /// <summary>
    /// Конструктор по умолчанию (для последующего вызова Fit)
    /// </summary>
    public LinearRegression()
    {
        Lrm = new LinearRegressionModel();
    }

    /// <summary>
    /// Обучающая выборка
    /// </summary>
    /// <param name="x">Вектор X (независимая переменная)</param>
    /// <param name="y">Вектор Y (зависимая переменная)</param>
    public LinearRegression(Vector x, Vector y) : this()
    {
        FitInternal(x, y);
    }

    /// <summary>
    /// Обучение модели на паре векторов X, Y
    /// </summary>
    /// <param name="x">Вектор X (независимая переменная)</param>
    /// <param name="y">Вектор Y (зависимая переменная)</param>
    public void Fit(Vector x, Vector y) => FitInternal(x, y);

    /// <summary>
    /// Обучение регрессии (совместимость с IRegression)
    /// </summary>
    /// <param name="data">Входные векторы (признаки, одномерные)</param>
    /// <param name="targets">Целевые значения</param>
    public void Train(Vector[] data, Vector targets)
    {
        var x = new Vector(data.Length);
        for (int i = 0; i < data.Length; i++)
            x[i] = data[i][0];
        FitInternal(x, targets);
    }

    /// <summary>
    /// Прогнозирование с помощью линейной модели
    /// </summary>
    /// <param name="x">Независимая переменная</param>
    /// <returns>Зависимая переменная</returns>
    public double Predict(double x)
    {
        return (Lrm.Slope * x) + Lrm.Intercept;
    }

    /// <summary>
    /// Прогнозирование с помощью линейной модели (вектор -> вектор)
    /// </summary>
    /// <param name="x">Вектор независимых переменных</param>
    /// <returns>Вектор зависимых переменных</returns>
    public Vector Predict(Vector x)
    {
        var outp = new Vector(x.Count);
        for (int i = 0; i < x.Count; i++)
            outp[i] = Predict(x[i]);
        return outp;
    }

    /// <inheritdoc/>
    double IRegression.Predict(Vector data) => Predict(data[0]);

    /// <inheritdoc/>
    public override string ToString()
    {
        return string.Format("f(x) ={0}*x+({1})", Lrm.Slope, Lrm.Intercept);
    }

    private void FitInternal(Vector x, Vector y)
    {
        double d = Statistic.CalcVariance(x);
        Lrm.Slope = Statistic.Cov(x, y) / (d == 0 ? 1e-9 : d);
        Lrm.Intercept = Statistic.ExpectedValue(y) - (Lrm.Slope * Statistic.ExpectedValue(x));
    }
}
