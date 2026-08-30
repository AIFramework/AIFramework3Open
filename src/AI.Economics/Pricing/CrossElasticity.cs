using System;
using AI.DataStructs.Algebraic;
using AI.Econometrics.Numerics;

namespace AI.Economics.Pricing;

/// <summary>
/// Матрица собственных и перекрёстных эластичностей по товарной линейке.
/// </summary>
/// <remarks>
/// <para>
/// Элемент <c>E[i, j]</c> показывает, на сколько процентов изменится спрос на
/// товар <c>i</c> при росте цены товара <c>j</c> на один процент. Диагональ —
/// собственные эластичности, они отрицательны. Положительный внедиагональный
/// элемент означает товары-заменители, отрицательный — дополняющие.
/// </para>
/// <para>
/// Без этой матрицы оптимизация цены по каждому товару отдельно систематически
/// ошибается: снижение цены на флагман забирает продажи у соседней позиции
/// в линейке, и суммарная прибыль падает, хотя по каждому товару в отдельности
/// решение выглядело выгодным.
/// </para>
/// </remarks>
public static class CrossElasticity
{
    /// <summary>
    /// Оценивает матрицу эластичностей по панели «период x товар».
    /// </summary>
    /// <param name="prices">Цены: строки — периоды, столбцы — товары.</param>
    /// <param name="quantities">Объёмы той же формы.</param>
    /// <returns>Матрица эластичностей размера «товары x товары».</returns>
    /// <exception cref="ArgumentNullException">Данные не заданы.</exception>
    /// <exception cref="ArgumentException">Формы не совпадают или данных мало.</exception>
    public static Matrix Estimate(Matrix prices, Matrix quantities)
    {
        ArgumentNullException.ThrowIfNull(prices);
        ArgumentNullException.ThrowIfNull(quantities);

        if (prices.Height != quantities.Height || prices.Width != quantities.Width)
            throw new ArgumentException("Формы матриц цен и объёмов должны совпадать.", nameof(quantities));

        int periods = prices.Height;
        int products = prices.Width;

        if (periods < products + 2)
            throw new ArgumentException(
                $"Нужно минимум {products + 2} периодов для {products} товаров.", nameof(prices));

        var elasticity = new Matrix(products, products);

        for (int target = 0; target < products; target++)
        {
            var x = new double[periods, products + 1];
            var y = new double[periods];

            for (int t = 0; t < periods; t++)
            {
                x[t, 0] = 1.0;
                for (int j = 0; j < products; j++) x[t, j + 1] = Math.Log(Math.Max(prices[t, j], 1e-9));
                y[t] = Math.Log(Math.Max(quantities[t, target], 1e-9));
            }

            // Гребневая регуляризация обязательна: цены внутри линейки почти
            // всегда двигаются вместе, и матрица регрессоров плохо обусловлена
            OlsFit? fit = Ols.Fit(x, y, ridge: 1e-3);
            if (fit is null) continue;

            for (int j = 0; j < products; j++) elasticity[target, j] = fit.Beta[j + 1];
        }

        return elasticity;
    }

    /// <summary>
    /// Диагональная матрица эластичностей: перекрёстные эффекты приняты
    /// нулевыми. Годится как отправная точка, когда панели ещё нет.
    /// </summary>
    /// <param name="ownElasticities">Собственные эластичности товаров.</param>
    /// <returns>Матрица с заданной диагональю.</returns>
    /// <exception cref="ArgumentNullException">Эластичности не заданы.</exception>
    public static Matrix Diagonal(Vector ownElasticities)
    {
        ArgumentNullException.ThrowIfNull(ownElasticities);

        int n = ownElasticities.Count;
        var m = new Matrix(n, n);
        for (int i = 0; i < n; i++) m[i, i] = ownElasticities[i];
        return m;
    }
}
