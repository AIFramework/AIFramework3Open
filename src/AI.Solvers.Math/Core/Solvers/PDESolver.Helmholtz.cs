using System.Text.RegularExpressions;

namespace AI.Solvers.Math.Core.Solvers;

public static partial class PDESolver
{
    #region Уравнение Гельмгольца: a·u_xx + b·u_yy + k²·u = 0

    public static string SolveHelmholtzEquation(string equation)
    {
        var eqClean = equation.Replace(" ", "").ToLower();
        double a  = ParseCoeff(eqClean, @"([+-]?\d*\.?\d+)\*?u_xx");
        double b  = ParseCoeff(eqClean, @"([+-]?\d*\.?\d+)\*?u_yy");
        string k2 = "k²";
        double k2Value = 0;

        var matchK = Regex.Match(eqClean, @"([+-]?\d*\.?\d+)\*?u\s*=\s*0");
        if (matchK.Success)
        {
            var s = matchK.Groups[1].Value;
            if (!string.IsNullOrEmpty(s) && s != "+" && s != "-")
            {
                k2 = s;
                double.TryParse(s, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out k2Value);
            }
            else k2Value = s == "-" ? -1.0 : 1.0;
        }

        bool isAnisotropic = System.Math.Abs(a - b) > 0.001;

        if (isAnisotropic)
        {
            return $@"=== ОБОБЩЁННОЕ УРАВНЕНИЕ ГЕЛЬМГОЛЬЦА (АНИЗОТРОПНОЕ) ===

Уравнение: {a}·u_xx + {b}·u_yy + {k2Value}·u = 0
ВАЖНО: Анизотропная среда — a = {a}, b = {b}, k² = {k2Value}

Замена переменных для нормализации:
  ξ = x,  η = y·√(a/b) = y·{System.Math.Sqrt(a / b):F4}

После замены получаем СТАНДАРТНОЕ уравнение Гельмгольца:
+----------------------------------------------------+
| u_ξξ + u_ηη + (k²/a)·u = 0,  k²_эфф = {k2Value / a:F4} |
+----------------------------------------------------+

Условие резонанса: (mπ/L_x)² + (nπ·√(a/b)/L_y)² = k²/a

" + NumericalPDESolver.SolveHelmholtzNumerical(a, b, k2Value);
        }

        var theory = $@"=== УРАВНЕНИЕ ГЕЛЬМГОЛЬЦА ===

Уравнение: ∇²u + {k2}·u = 0  или  u_xx + u_yy + {k2}·u = 0

Физический смысл:
  Стационарное волновое уравнение
  Колебания мембраны на частоте ω (где k² = ω²/c²)

РЕШЕНИЕ ДЛЯ ПРЯМОУГОЛЬНИКА [0,a]×[0,b]:
+----------------------------------------------------+
| u(x,y) = Σ Σ A_mn·sin(mπx/a)·sin(nπy/b)           |
|                                                    |
| где (mπ/a)² + (nπ/b)² = {k2}  (условие резонанса)|
+----------------------------------------------------+

Для круга радиуса R (полярные координаты):
  u(r,θ) = Σ [A_n·J_n(kr) + B_n·Y_n(kr)]·[C_n·cos(nθ) + D_n·sin(nθ)]
  где J_n, Y_n - функции Бесселя

Собственные значения (мембрана):
  k_mn² = (mπ/a)² + (nπ/b)²
  Частоты: ω_mn = c·k_mn";

        return theory + "\n\n" + NumericalPDESolver.SolveHelmholtzNumerical(1.0, 1.0, k2Value);
    }

    #endregion

    #region Уравнение Шрёдингера

    public static string SolveSchrodingerEquation(string equation) =>
        @"=== УРАВНЕНИЕ ШРЁДИНГЕРА ===

Уравнение: iℏ·ψ_t = -(ℏ²/2m)·∇²ψ + V(x)·ψ

Физический смысл:
  Фундаментальное уравнение квантовой механики
  Описывает эволюцию волновой функции частицы
  ψ - волновая функция, V - потенциал

Стационарное уравнение (ψ = φ(x)·e^(-iEt/ℏ)):
  -(ℏ²/2m)·∇²φ + V(x)·φ = E·φ

РЕШЕНИЯ ДЛЯ РАЗНЫХ ПОТЕНЦИАЛОВ:

1. Свободная частица (V = 0):
+----------------------------------------------------+
| ψ(x,t) = A·exp[i(kx - ωt)],  E = ℏω = ℏ²k²/(2m)  |
+----------------------------------------------------+

2. Потенциальная яма (0 < x < L, V = 0; иначе V = ∞):
  φ_n(x) = √(2/L)·sin(nπx/L)
  E_n = (n²π²ℏ²)/(2mL²)

3. Гармонический осциллятор (V = ½mω²x²):
  φ_n(x) = (mω/πℏ)^(1/4) · (1/√(2^n·n!)) · H_n(√(mω/ℏ)·x) · exp(-mωx²/2ℏ)
  E_n = ℏω(n + ½)

4. Атом водорода (V = -e²/r):
  φ_nlm(r,θ,φ) = R_nl(r)·Y_lm(θ,φ)
  E_n = -13.6 эВ / n²

Свойства:
  * Сохранение вероятности: ∫|ψ|² dx = 1
  * Принцип суперпозиции
  * Туннельный эффект";

    #endregion
}
