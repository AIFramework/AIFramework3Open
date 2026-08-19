using System.Text.RegularExpressions;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class PDESolver
{
    #region Уравнение Лапласа / Пуассона / Диффузии

    public static string SolveLaplaceEquation(string equation)
    {
        var eqClean = equation.Replace(" ", "").ToLower();
        double a = ParseCoeff(eqClean, @"([+-]?\d*\.?\d+)\*?u_xx");
        double b = ParseCoeff(eqClean, @"([+-]?\d*\.?\d+)\*?u_yy");

        if (System.Math.Abs(a - b) > 0.001)
        {
            return $@"=== ЭЛЛИПТИЧЕСКОЕ УРАВНЕНИЕ ===

Уравнение: {a}·u_xx + {b}·u_yy = 0

ВНИМАНИЕ: Это НЕ классическое уравнение Лапласа!
Коэффициенты различны: a = {a}, b = {b}

РЕШЕНИЕ через приведение к уравнению Лапласа:

Замена переменных:
  ξ = x
  η = y·√(a/b) = y·{System.Math.Sqrt(a / b):F4}

После замены получаем СТАНДАРТНОЕ уравнение Лапласа:
+----------------------------------------------------+
| u_ξξ + u_ηη = 0                                    |
+----------------------------------------------------+

Для прямоугольника [0,a']×[0,b']:
  u(x,y) = Σ [A_n·sinh(λ_n·y·√(a/b)) + B_n·cosh(λ_n·y·√(a/b))]
           · sin(λ_n·x)
  где λ_n = nπ/a'

Физический смысл:
  * Анизотропная среда (разные коэффициенты диффузии по x и y)
  * Коэффициент {a}/{b} = {a / b:F4} показывает отношение характеристик

" + NumericalPDESolver.SolveLaplaceNumerical(a, b);
        }

        var theory = @"=== УРАВНЕНИЕ ЛАПЛАСА ===

Уравнение: ∇²u = u_xx + u_yy = 0

Физический смысл:
  Стационарное распределение температуры
  Электростатический потенциал
  Течение несжимаемой жидкости

СВОЙСТВА:
  * Гармонические функции
  * Принцип максимума
  * Среднее значение по окружности

РЕШЕНИЯ ДЛЯ РАЗЛИЧНЫХ ОБЛАСТЕЙ:

1. Прямоугольник [0,a]×[0,b]:
+----------------------------------------------------+
| u(x,y) = Σ [A_n·sinh(nπy/a) + B_n·cosh(nπy/a)]    |
|          · sin(nπx/a)                              |
+----------------------------------------------------+

2. Круг радиуса R (полярные координаты):
+----------------------------------------------------+
| u(r,θ) = A_0 + Σ r^n·(A_n·cos(nθ) + B_n·sin(nθ)) |
+----------------------------------------------------+

3. Кольцо (r₁ < r < r₂):
  u(r,θ) = A_0 + B_0·ln(r) + 
           Σ (A_n·r^n + B_n·r^(-n))·(C_n·cos(nθ) + D_n·sin(nθ))

Методы решения:
  * Разделение переменных
  * Метод конформных отображений
  * Функции Грина
  * Метод интегральных уравнений";

        return theory + "\n\n" + NumericalPDESolver.SolveLaplaceNumerical(1.0, 1.0);
    }

    public static string SolvePoissonEquation(string equation) =>
        @"=== УРАВНЕНИЕ ПУАССОНА ===

Уравнение: ∇²u = u_xx + u_yy = f(x,y)

Физический смысл:
  Распределение температуры с источниками
  Электростатический потенциал с зарядами
  Мембрана под нагрузкой

МЕТОД ФУНКЦИИ ГРИНА:
+----------------------------------------------------+
| u(x,y) = ∬_D G(x,y;ξ,η)·f(ξ,η) dξ dη             |
|                                                    |
| где G - функция Грина для области D               |
+----------------------------------------------------+

Для прямоугольника [0,a]×[0,b] с нулевыми краевыми условиями:
  u(x,y) = Σ Σ A_mn·sin(mπx/a)·sin(nπy/b)

  где A_mn = -4/(ab·λ_mn)·∫∫ f(ξ,η)·sin(mπξ/a)·sin(nπη/b) dξ dη
      λ_mn = (mπ/a)² + (nπ/b)²
  Знак минус обязателен: ∇²u даёт -λ_mn·A_mn, и он должен совпасть с f.

Для круга радиуса R (функция Грина методом отражений):
  u(r,θ) = ∫₀^(2π) ∫₀^R G(r,θ;ρ,φ)·f(ρ,φ)·ρ dρ dφ

  G = 1/(4π)·ln[ (r² + ρ² - 2rρ·cos(θ-φ)) / (r²ρ²/R² + R² - 2rρ·cos(θ-φ)) ]
  Второе слагаемое — вклад образа точки (ρ, φ) относительно окружности:
  на r = R числитель и знаменатель совпадают, поэтому G обращается в ноль.

Свойство: при f = 0 уравнение переходит в уравнение Лапласа. Из этого НЕ следует
u = 0: решение определяется краевыми условиями и равно нулю лишь при нулевых
краевых значениях (по принципу максимума).";

    public static string SolveDiffusionEquation(string equation)
    {
        var match = Regex.Match(equation, @"u_t\s*=\s*([\d\.]+)\s*\*", RegexOptions.IgnoreCase);
        string D = "D";
        double DValue = 1.0;
        if (match.Success && match.Groups[1].Value != "1")
        {
            D = match.Groups[1].Value;
            double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out DValue);
        }

        var theory = $@"=== УРАВНЕНИЕ ДИФФУЗИИ (2D) ===

Уравнение: u_t = {D}·(u_xx + u_yy)

Физический смысл:
  Диффузия вещества в 2D, Распространение тепла на плоскости
  {D} - коэффициент диффузии

МЕТОД РАЗДЕЛЕНИЯ ПЕРЕМЕННЫХ:
+----------------------------------------------------+
| u(x,y,t) = X(x)·Y(y)·T(t)                         |
|                                                    |
| Решение: u(x,y,t) = Σ Σ A_mn·exp(-{D}·λ_mn²·t)  |
|                      · φ_m(x)·ψ_n(y)             |
+----------------------------------------------------+

Фундаментальное решение (бесконечная плоскость):
+----------------------------------------------------+
| u(x,y,t) = 1/(4π·{D}·t)·exp[-(x² + y²)/(4·{D}·t)]|
+----------------------------------------------------+

Свойства:
  * Сохранение массы: ∫∫ u dx dy = const";

        return theory + "\n\n" + NumericalPDESolver.SolveDiffusion2DNumerical(DValue);
    }

    #endregion

    #region Вспомогательный: извлечение коэффициента из строки уравнения

    private static double ParseCoeff(string eq, string pattern, double defaultVal = 1.0)
    {
        var m = Regex.Match(eq, pattern);
        if (!m.Success) return defaultVal;
        var s = m.Groups[1].Value;
        if (string.IsNullOrEmpty(s) || s == "+" || s == "-") return s == "-" ? -1.0 : defaultVal;
        return double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : defaultVal;
    }

    #endregion
}
