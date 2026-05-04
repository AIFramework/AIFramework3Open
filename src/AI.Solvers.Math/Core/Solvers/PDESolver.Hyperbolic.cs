using System.Text.RegularExpressions;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class PDESolver
{
    #region Уравнение переноса (адвекции): u_t + c·u_x = 0

    public static string SolveAdvectionEquation(string equation)
    {
        var match = Regex.Match(equation, @"([+-]?[\d\.]+)\s*\*?\s*u_x", RegexOptions.IgnoreCase);
        string c = match.Success ? match.Groups[1].Value : "c";
        double cValue = 1.0;
        if (match.Success)
            double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out cValue);

        var theory = $@"=== УРАВНЕНИЕ ПЕРЕНОСА (АДВЕКЦИИ) ===

Уравнение: u_t + {c}·u_x = 0

Физический смысл:
  Перенос скаляра (температуры, концентрации)
  со скоростью {c}

ТОЧНОЕ РЕШЕНИЕ (метод характеристик):
+----------------------------------------------------+
| u(x,t) = f(x - {c}t)                              |
|                                                    |
| где f(x) = u(x,0) - начальное условие             |
+----------------------------------------------------+

Характеристики:
  x - {c}t = const  (прямые линии на плоскости x-t)

Пример (начальный профиль f(x) = sin(x)):
  u(x,t) = sin(x - {c}t)

Численные схемы:
  Схема апвинд (устойчивая при CFL < 1):
    u_j^(n+1) = u_j^n - ({c}·Δt/Δx)·(u_j^n - u_(j-1)^n), {c} > 0

Свойство сохранения: ∫ u dx = const";

        return theory + "\n\n" + NumericalPDESolver.SolveAdvectionNumerical(cValue);
    }

    #endregion

    #region Уравнение диффузии-адвекции: u_t + c·u_x = D·u_xx

    public static string SolveDiffusionAdvectionEquation(string equation)
    {
        var matchC = Regex.Match(equation, @"([+-]?[\d\.]+)\s*\*?\s*u_x[^x]", RegexOptions.IgnoreCase);
        var matchD = Regex.Match(equation, @"([+-]?[\d\.]+)\s*\*?\s*u_xx", RegexOptions.IgnoreCase);
        string c = matchC.Success ? matchC.Groups[1].Value : "c";
        string D = matchD.Success ? matchD.Groups[1].Value : "D";
        double cValue = matchC.Success && double.TryParse(matchC.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var cv) ? cv : 1.0;
        double DValue = matchD.Success && double.TryParse(matchD.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dv) ? dv : 0.1;

        double Pe = DValue > 0 ? System.Math.Abs(cValue) / DValue : double.PositiveInfinity;

        var theory = $@"=== УРАВНЕНИЕ ДИФФУЗИИ-АДВЕКЦИИ ===

Уравнение: u_t + {c}·u_x = {D}·u_xx

Физический смысл:
  Перенос примеси (скорость {c}) с диффузией (коэффициент {D})
  Число Пекле Pe = |c|/D = {Pe:F2}
  {(Pe < 1 ? "-> Доминирует ДИФФУЗИЯ" : Pe > 10 ? "-> Доминирует АДВЕКЦИЯ" : "-> Смешанный режим")}

ТОЧНОЕ РЕШЕНИЕ (бесконечная область, начальное условие u(x,0) = δ(x)):
+----------------------------------------------------+
| u(x,t) = 1/√(4π·{D}·t) · exp[-(x-{c}t)²/(4·{D}·t)]|
+----------------------------------------------------+

Метод характеристик + диффузионное уравнение:
  Замена: ξ = x - {c}t,  τ = t
  Уравнение для u(ξ,τ): u_τ = {D}·u_ξξ

Дисперсионное соотношение (гармонические волны):
  u ~ exp[i(kx - ωt)]
  ω = {c}·k - i·{D}·k²
  Мнимая часть -> затухание с коэффициентом {D}·k²";

        return theory + "\n\n" + NumericalPDESolver.SolveDiffusionAdvectionNumerical(cValue, DValue);
    }

    #endregion

    #region Уравнение Бюргерса: u_t + u·u_x = ν·u_xx

    public static string SolveBurgersEquation(string equation)
    {
        var match = Regex.Match(equation, @"nu\s*=\s*([\d\.]+)", RegexOptions.IgnoreCase);
        string nu = match.Success ? match.Groups[1].Value : "ν";
        double nuValue = 0.01;
        if (match.Success)
            double.TryParse(match.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out nuValue);

        var theory = $@"=== УРАВНЕНИЕ БЮРГЕРСА ===

Уравнение: u_t + u·u_x = {nu}·u_xx

Физический смысл:
  Модель турбулентности, ударные волны
  Нелинейный перенос + диффузия

ТОЧНОЕ РЕШЕНИЕ (преобразование Хопфа-Коула):
+----------------------------------------------------+
| Замена: u = -2{nu}·(φ_x / φ)                     |
| Тогда φ удовлетворяет уравнению теплопроводности:  |
| φ_t = {nu}·φ_xx                                   |
+----------------------------------------------------+

Полное решение:
+----------------------------------------------------+
| φ(x,t) = ∫ G(x-ξ, t)·φ₀(ξ) dξ                   |
| u(x,t) = -2{nu}·(∂/∂x)·ln(φ(x,t))               |
| где G - фундаментальное решение для теплопроводности|
+----------------------------------------------------+

При ν -> 0 (инвискидное уравнение Бюргерса):
  * Метод характеристик: x - u₀(x₀)·t = x₀
  * Ударная волна при u_x -> -∞
  * Время образования: t* = -1/min(u₀'(x₀))

Свойства:
  * Конечная скорость роста ударных волн
  * Эффект диссипации при ν > 0
  * Связь с уравнением теплопроводности";

        return theory + "\n\n" + NumericalPDESolver.SolveBurgersNumerical(1.0, nuValue);
    }

    #endregion
}
