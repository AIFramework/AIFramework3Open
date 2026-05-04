using System.Text.RegularExpressions;

namespace AI.Solvers.Math.Core.Solvers;

/// <summary>Символьный решатель дифференциальных уравнений в частных производных.</summary>
public static partial class PDESolver
{
    #region Уравнение теплопроводности: u_t = α·u_xx

    public static string SolveHeatEquation(string equation)
    {
        var match = Regex.Match(equation, @"u_t\s*=\s*([\d\.]+)\s*\*?\s*u_xx", RegexOptions.IgnoreCase);
        string alpha = "α";
        double alphaValue = 1.0;
        if (match.Success && match.Groups[1].Value != "1")
        {
            alpha = match.Groups[1].Value;
            double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out alphaValue);
        }

        var theory = $@"=== УРАВНЕНИЕ ТЕПЛОПРОВОДНОСТИ ===

Уравнение: u_t = {alpha}·u_xx

Физический смысл: 
  Описывает распространение тепла в стержне
  {alpha} - коэффициент температуропроводности

Метод решения: Разделение переменных
  u(x,t) = X(x)·T(t)

ОБЩЕЕ РЕШЕНИЕ:
+----------------------------------------------------+
| u(x,t) = Σ A_n·exp(-{alpha}·λ_n²·t)·sin(λ_n·x)    |
|                                                    |
| где λ_n = nπ/L - собственные значения             |
|     A_n - коэффициенты из начальных условий       |
+----------------------------------------------------+

Для бесконечной области (фундаментальное решение):
  u(x,t) = 1/√(4π·{alpha}·t) · exp(-x²/(4·{alpha}·t))

Пример (стержень длины L, с граничными условиями u(0,t)=u(L,t)=0):
  u(x,t) = Σ b_n·exp(-{alpha}·(nπ/L)²·t)·sin(nπx/L)
  где b_n = (2/L)·∫₀ᴸ f(x)·sin(nπx/L) dx
      f(x) = u(x,0) - начальное распределение температуры";

        return theory + "\n\n" + NumericalPDESolver.SolveHeatEquationNumerical(alphaValue);
    }

    #endregion

    #region Волновое уравнение: u_tt = c²·u_xx

    public static string SolveWaveEquation(string equation)
    {
        var match = Regex.Match(equation, @"([a-z])_tt\s*=\s*([a-z\d\.]+)\s*\*\s*([a-z])_xx");
        var c = match.Success ? match.Groups[2].Value : "c";
        double cValue = 1.0;
        if (match.Success && double.TryParse(match.Groups[2].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out double parsedC))
            cValue = System.Math.Sqrt(parsedC);

        var theory = $@"=== ВОЛНОВОЕ УРАВНЕНИЕ ===

Уравнение: u_tt = {c}²·u_xx

Физический смысл:
  Описывает колебания струны, звуковые волны

ОБЩЕЕ РЕШЕНИЕ (формула Д'Аламбера):
+----------------------------------------------------+
| u(x,t) = ½[φ(x-{c}t) + φ(x+{c}t)]                 |
|        + 1/(2{c})·∫_(x-{c}t)^(x+{c}t) ψ(s) ds    |
|                                                    |
| где φ(x) = u(x,0) - начальное смещение            |
|     ψ(x) = u_t(x,0) - начальная скорость          |
+----------------------------------------------------+

Разложение в ряд (метод Фурье):
  u(x,t) = Σ [A_n·cos({c}·λ_n·t) + B_n·sin({c}·λ_n·t)]·sin(λ_n·x)

Пример (струна длины L):
  λ_n = nπ/L
  A_n = (2/L)·∫₀ᴸ φ(x)·sin(nπx/L) dx
  B_n = (2/(nπ{c}))·∫₀ᴸ ψ(x)·sin(nπx/L) dx

Свойства:
  * Скорость распространения волн: {c}
  * Бегущие волны: f(x±{c}t)
  * Стоячие волны: sin(λx)·cos(λ{c}t)";

        return theory + "\n\n" + NumericalPDESolver.SolveWaveEquationNumerical(cValue);
    }

    #endregion
}
