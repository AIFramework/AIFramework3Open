using System;
using System.Globalization;
using System.Numerics;

namespace AI.HighLevelFunctions;

/// <summary>
/// Специальные функции одной вещественной переменной: гамма-функция и её логарифм, регуляризованные
/// неполные гамма- и бета-функции, функция ошибок, факториал, функция Бесселя J₀, интегралы Френеля.
/// </summary>
/// <remarks>
/// <para>
/// Единственная реализация во фреймворке: распределения, проверки гипотез, эконометрика, химия и
/// AI-скрипт берут эти функции отсюда. Гамма-функции, функция ошибок и факториал сверены с mpmath
/// (50 знаков) — эталоны выгружает Tools/MathGolden/export_special_golden.py.
/// </para>
/// <para>
/// J₀ и интегралы Френеля нужны задачам распространения волн: J₀ — автокорреляция замираний по
/// Кларку, интегралы Френеля — дифракция на крае; ими же пользуется символьный решатель.
/// </para>
/// <para>
/// Недопустимый аргумент даёт <c>NaN</c>, как у функций <see cref="Math"/>: функция участвует в
/// длинных формулах, и исключение посреди суммы хуже, чем <c>NaN</c> в результате.
/// Исключение — <see cref="Factorial"/>: у него целый аргумент, и отрицательное число там — ошибка вызова.
/// </para>
/// </remarks>
public static class SpecialFunctions
{
    /// <summary>Наибольшее <c>n</c>, при котором <c>n!</c> помещается в <see cref="double"/>.</summary>
    public const int MaxFactorialArgument = 170;

    /// <summary>С этого аргумента ряд Стирлинга точен до последнего разряда.</summary>
    private const double StirlingFrom = 10;
    private const double Sqrt2Pi = 2.506628274631000502415765;
    private const double SqrtPi = 1.772453850905516027298167;
    private const double EulerGamma = 0.5772156649015328606065121;
    private const double Epsilon = 1.1102230246251565e-16;
    private const double Tiny = 1e-300;
    private const int MaxIterations = 300;
    private const double Tolerance = 1e-15;

    // ζ(k) − 1 для k = 2…30: ряд ln Γ(1 + z) около z = 0
    private static readonly double[] s_zetaMinusOne =
    [
        0.64493406684822643647, 0.2020569031595942854, 0.082323233711138191516, 0.036927755143369926331,
        0.017343061984449139715, 0.0083492773819228268398, 0.0040773561979443393787, 0.0020083928260822144179,
        0.00099457512781808533715, 0.0004941886041194645587, 0.00024608655330804829864, 0.00012271334757848914675,
        0.000061248135058704829259, 0.000030588236307020493552, 0.000015282259408651871733, 7.6371976378997622736e-6,
        3.8172932649998398565e-6, 1.9082127165539389257e-6, 9.5396203387279611315e-7, 4.7693298678780646312e-7,
        2.3845050272773299e-7, 1.1921992596531107307e-7, 5.9608189051259479612e-8, 2.9803503514652280186e-8,
        1.4901554828365041235e-8, 7.450711789835429492e-9, 3.7253340247884570548e-9, 1.8626597235130490064e-9,
        9.3132743241966818287e-10,
    ];

    private static readonly double[] s_factorials = BuildFactorials();

    /// <summary>
    /// Функция Бесселя первого рода нулевого порядка J₀(x)
    /// </summary>
    /// <param name="x">Аргумент</param>
    /// <remarks>
    /// Считается по интегральному представлению <c>J₀(x) = (1/π)·∫₀^π cos(x·sin θ) dθ</c> формулой
    /// трапеций. Подынтегральная функция гладкая и периодическая, поэтому ошибка трапеций убывает
    /// быстрее любой степени числа узлов: при числе узлов больше |x| + 32 она на уровне машинной точности
    /// при любом x, без переключения между рядом и асимптотикой.
    /// </remarks>
    public static double BesselJ0(double x)
    {
        if (double.IsNaN(x) || double.IsInfinity(x))
            return double.IsInfinity(x) ? 0 : double.NaN;

        int nodes = 32 + (int)Math.Ceiling(Math.Abs(x));
        double sum = 0;

        for (int k = 0; k < nodes; k++)
            sum += Math.Cos(x * Math.Sin(Math.PI * k / nodes));

        return sum / nodes;
    }

    /// <summary>Интеграл Френеля <c>C(x) = ∫₀ˣ cos(πt²/2) dt</c></summary>
    /// <param name="x">Аргумент</param>
    public static double FresnelC(double x) => Fresnel(x).C;

    /// <summary>Интеграл Френеля <c>S(x) = ∫₀ˣ sin(πt²/2) dt</c></summary>
    /// <param name="x">Аргумент</param>
    public static double FresnelS(double x) => Fresnel(x).S;

    /// <summary>
    /// Оба интеграла Френеля сразу
    /// </summary>
    /// <param name="x">Аргумент</param>
    /// <remarks>
    /// При |x| ≤ 1,5 — степенной ряд, дальше — цепная дробь для дополнительной функции ошибок
    /// комплексного аргумента (схема «Численных рецептов»). Ряд при больших x теряет точность
    /// на взаимном уничтожении слагаемых, цепная дробь при малых сходится медленно — поэтому граница.
    /// Обе функции нечётны и стремятся к 1/2 при x → ∞.
    /// </remarks>
    public static (double C, double S) Fresnel(double x)
    {
        if (double.IsNaN(x))
            return (double.NaN, double.NaN);

        if (double.IsInfinity(x))
            return x > 0 ? (0.5, 0.5) : (-0.5, -0.5);

        double ax = Math.Abs(x);
        double c, s;

        if (ax < 1e-150)
        {
            c = ax;
            s = 0;
        }
        else if (ax <= 1.5)
        {
            double sum = 0, sumS = 0, sumC = ax, sign = 1, term = ax;
            double factor = Math.PI / 2 * ax * ax;
            bool odd = true;
            int n = 3;

            // Слагаемые рядов S и C идут вперемежку: нечётные шаги пополняют S, чётные — C
            for (int k = 1; k <= MaxIterations; k++)
            {
                term *= factor / k;
                sum += sign * term / n;
                double test = Math.Abs(sum) * Tolerance;

                if (odd)
                {
                    sign = -sign;
                    sumS = sum;
                    sum = sumC;
                }
                else
                {
                    sumC = sum;
                    sum = sumS;
                }

                if (term < test)
                    break;

                odd = !odd;
                n += 2;
            }

            s = sumS;
            c = sumC;
        }
        else
        {
            double pix2 = Math.PI * ax * ax;
            var b = new Complex(1, -pix2);
            var cc = new Complex(1e300, 0);
            Complex d = 1 / b;
            Complex h = d;
            int n = -1;

            for (int k = 2; k <= MaxIterations; k++)
            {
                n += 2;
                double a = -n * (n + 1);
                b += 4;
                d = 1 / ((a * d) + b);
                cc = b + (a / cc);
                Complex delta = cc * d;
                h *= delta;

                if (Math.Abs(delta.Real - 1) + Math.Abs(delta.Imaginary) < Tolerance)
                    break;
            }

            h *= new Complex(ax, -ax);
            Complex cs = new Complex(0.5, 0.5) * (1 - (new Complex(Math.Cos(0.5 * pix2), Math.Sin(0.5 * pix2)) * h));
            c = cs.Real;
            s = cs.Imaginary;
        }

        return x < 0 ? (-c, -s) : (c, s);
    }

    /// <summary>Гамма-функция Γ(x).</summary>
    /// <remarks>
    /// Для натуральных <c>x</c> до 171 значение точное: <c>Γ(n) = (n − 1)!</c>. В полюсах
    /// (ноль и отрицательные целые) возвращается <c>+∞</c>, при <c>x &gt; 171.62</c> — тоже.
    /// </remarks>
    /// <param name="x">Аргумент.</param>
    public static double Gamma(double x)
    {
        if (double.IsNaN(x) || double.IsNegativeInfinity(x)) return double.NaN;
        if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;

        if (x == Math.Floor(x))
        {
            if (x <= 0) return double.PositiveInfinity;
            if (x <= MaxFactorialArgument + 1) return s_factorials[(int)x - 1];
        }

        // Формула отражения: Γ(x)·Γ(1 − x) = π / sin(πx)
        if (x < 0.5) return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1 - x));

        // Подъём в область ряда Стирлинга: Γ(x) = Γ(x + n) / (x (x + 1) … (x + n − 1))
        double product = 1;

        while (x < StirlingFrom)
        {
            product *= x;
            x += 1;
        }

        // Степень берётся двумя половинами: x^x переполняется раньше, чем сама Γ(x)
        double half = Math.Pow(x, x / 2);

        return Math.Sqrt(2 * Math.PI / x) * (half * Math.Exp(-x)) * half * Math.Exp(StirlingCorrection(x)) / product;
    }

    /// <summary>Натуральный логарифм гамма-функции ln Γ(x) при <c>x &gt; 0</c>.</summary>
    /// <remarks>
    /// <para>
    /// При <c>x &lt; ½</c> — формула отражения, поэтому малые аргументы считаются точно:
    /// <c>ln Γ(1e-8) = 18.42068…</c>. При <c>x ≥ 10</c> — ряд Стирлинга. Между ними — ряд по
    /// степеням отклонения от нулей функции (<c>x = 1</c> и <c>x = 2</c>): относительная точность
    /// сохраняется и там, где сам логарифм близок к нулю.
    /// </para>
    /// <para>Вне области определения (<c>x ≤ 0</c>) возвращается <c>NaN</c>.</para>
    /// </remarks>
    /// <param name="x">Аргумент, <c>x &gt; 0</c>.</param>
    public static double LogGamma(double x)
    {
        if (double.IsNaN(x) || x <= 0) return double.NaN;
        if (double.IsPositiveInfinity(x)) return double.PositiveInfinity;

        if (x < 0.5) return Math.Log(Math.PI / Math.Sin(Math.PI * x)) - LogGamma(1 - x);

        if (x >= StirlingFrom)
        {
            return ((x - 0.5) * Math.Log(x)) - x + (0.5 * Math.Log(2 * Math.PI)) + StirlingCorrection(x);
        }

        if (x <= 1.5) return LogGammaNearOne(x - 1, includeLog: true);

        // Спуск к отрезку (1.5, 2.5]: ln Γ(x) = ln((x − 1)(x − 2) … y) + ln Γ(y)
        double product = 1;

        while (x > 2.5)
        {
            x -= 1;
            product *= x;
        }

        double nearTwo = LogGammaNearOne(x - 2, includeLog: false);

        return product == 1 ? nearTwo : Math.Log(product) + nearTwo;
    }

    /// <summary>Логарифм бета-функции ln B(a, b) = ln Γ(a) + ln Γ(b) − ln Γ(a + b).</summary>
    /// <remarks>
    /// Когда один параметр велик, разность <c>ln Γ(b) − ln Γ(a + b)</c> берётся по формуле Стирлинга
    /// в свёрнутом виде: сами логарифмы порядка <c>b·ln b</c> и при вычитании теряли бы знаки.
    /// </remarks>
    /// <param name="a">Первый параметр, <c>a &gt; 0</c>.</param>
    /// <param name="b">Второй параметр, <c>b &gt; 0</c>.</param>
    public static double LogBeta(double a, double b)
    {
        double small = Math.Min(a, b), large = Math.Max(a, b);

        if (!(small > 0) || double.IsNaN(large)) return double.NaN;
        if (large < StirlingFrom) return LogGamma(a) + LogGamma(b) - LogGamma(a + b);

        // ln Γ(L) − ln Γ(L + s) = −(L − ½)·ln(1 + s/L) − s·ln(L + s) + s + поправки Стирлинга
        double sum = large + small;
        double difference = (-(large - 0.5) * Log1p(small / large)) - (small * Math.Log(sum)) + small
            + StirlingCorrection(large) - StirlingCorrection(sum);

        return LogGamma(small) + difference;
    }

    /// <summary>Регуляризованная нижняя неполная гамма-функция P(a, x) = γ(a, x) / Γ(a).</summary>
    /// <remarks>
    /// Функция распределения гамма-закона; для хи-квадрат с <c>k</c> степенями свободы —
    /// <c>P(k/2, x/2)</c>. При <c>x &lt; a + 1</c> считается рядом, иначе — как <c>1 − Q</c>.
    /// Когда нужен малый правый хвост, берите <see cref="RegularizedGammaQ"/>: разность
    /// <c>1 − P</c> ниже 1e-16 обращается в ноль.
    /// </remarks>
    /// <param name="a">Параметр формы, <c>a &gt; 0</c>.</param>
    /// <param name="x">Аргумент, <c>x ≥ 0</c>.</param>
    public static double RegularizedGammaP(double a, double x)
    {
        if (!(a > 0) || !(x >= 0) || double.IsInfinity(a)) return double.NaN;
        if (x == 0) return 0;
        if (double.IsPositiveInfinity(x)) return 1;

        return x < a + 1
            ? GammaPrefactor(a, x) * GammaSeries(a, x)
            : 1 - (GammaPrefactor(a, x) * GammaFraction(a, x));
    }

    /// <summary>Регуляризованная верхняя неполная гамма-функция Q(a, x) = Γ(a, x) / Γ(a) = 1 − P(a, x).</summary>
    /// <remarks>
    /// Правый хвост гамма-закона и хи-квадрат. При <c>x ≥ a + 1</c> считается цепной дробью
    /// напрямую, без вычитания, и остаётся верным в относительном смысле до границы <see cref="double"/>.
    /// </remarks>
    /// <param name="a">Параметр формы, <c>a &gt; 0</c>.</param>
    /// <param name="x">Аргумент, <c>x ≥ 0</c>.</param>
    public static double RegularizedGammaQ(double a, double x)
    {
        if (!(a > 0) || !(x >= 0) || double.IsInfinity(a)) return double.NaN;
        if (x == 0) return 1;
        if (double.IsPositiveInfinity(x)) return 0;

        return x < a + 1
            ? 1 - (GammaPrefactor(a, x) * GammaSeries(a, x))
            : GammaPrefactor(a, x) * GammaFraction(a, x);
    }

    /// <summary>Регуляризованная неполная бета-функция I_x(a, b).</summary>
    /// <remarks>
    /// Функция распределения бета-закона; через неё выражаются распределения Стьюдента, Фишера и
    /// биномиальное. Считается цепной дробью; при <c>x &gt; (a + 1)/(a + b + 2)</c> — через симметрию
    /// <c>I_x(a, b) = 1 − I_{1−x}(b, a)</c>. Малое дополнение <c>1 − I_x(a, b)</c> берите как
    /// <c>RegularizedBeta(b, a, 1 − x)</c>.
    /// </remarks>
    /// <param name="a">Первый параметр формы, <c>a &gt; 0</c>.</param>
    /// <param name="b">Второй параметр формы, <c>b &gt; 0</c>.</param>
    /// <param name="x">Аргумент; значения вне [0, 1] приводятся к границе.</param>
    public static double RegularizedBeta(double a, double b, double x) => RegularizedBeta(a, b, x, 1 - x);

    /// <summary>
    /// Регуляризованная неполная бета-функция I_x(a, b), когда дополнение <c>1 − x</c> известно точно.
    /// </summary>
    /// <remarks>
    /// Аргумент часто приходит дробью <c>p/(p + q)</c>; тогда <c>1 − x = q/(p + q)</c> известно без
    /// вычитания, а разность <c>1 − x</c>, посчитанная из самого <c>x</c>, при <c>x</c> около единицы
    /// состоит из разрядов округления. Так считаются хвосты распределений Стьюдента и Фишера.
    /// </remarks>
    /// <param name="a">Первый параметр формы, <c>a &gt; 0</c>.</param>
    /// <param name="b">Второй параметр формы, <c>b &gt; 0</c>.</param>
    /// <param name="x">Аргумент; значения вне [0, 1] приводятся к границе.</param>
    /// <param name="complement">Дополнение <c>1 − x</c>.</param>
    public static double RegularizedBeta(double a, double b, double x, double complement)
    {
        if (!(a > 0) || !(b > 0) || double.IsNaN(x) || double.IsNaN(complement) || double.IsInfinity(a) || double.IsInfinity(b))
        {
            return double.NaN;
        }

        if (x <= 0) return 0;
        if (x >= 1 || complement <= 0) return 1;

        return x <= (a + 1) / (a + b + 2)
            ? BetaPrefactor(a, b, x, complement) * BetaFraction(a, b, x) / a
            : 1 - (BetaPrefactor(b, a, complement, x) * BetaFraction(b, a, complement) / b);
    }

    /// <summary>Функция ошибок erf(x) = 2/√π · ∫₀ˣ e^(−t²) dt.</summary>
    /// <remarks>
    /// Через неполную гамма-функцию: <c>erf(x) = P(½, x²)</c>. При <c>|x| &lt; 1e-10</c> — первый
    /// член ряда <c>2x/√π</c>: там <c>x²</c> уже меньше единицы младшего разряда, а при
    /// <c>|x| &lt; 1.5e-154</c> и вовсе обращается в ноль.
    /// </remarks>
    /// <param name="x">Аргумент.</param>
    public static double Erf(double x)
    {
        if (double.IsNaN(x)) return double.NaN;

        double magnitude = Math.Abs(x);

        if (magnitude < 1e-10) return 2 * x / SqrtPi;

        double t = x * x;

        // x бесконечен или x² переполнился: erf там уже ровно ±1
        if (double.IsInfinity(t)) return Math.Sign(x);

        double front = magnitude * ExpOfMinusSquare(x, t) / SqrtPi;
        double value = t < 1.5 ? front * GammaSeries(0.5, t) : 1 - (front * GammaFraction(0.5, t));

        return x < 0 ? -value : value;
    }

    /// <summary>Дополнительная функция ошибок erfc(x) = 1 − erf(x), вычисленная без вычитания.</summary>
    /// <remarks>
    /// Разность <c>1 − erf(x)</c> при <c>x &gt; 5</c> состоит из одних разрядов округления, а
    /// <c>erfc(5) = 1.54e-12</c> — величина, ради которой функцию и зовут: хвост нормального закона.
    /// </remarks>
    /// <param name="x">Аргумент.</param>
    public static double Erfc(double x)
    {
        if (double.IsNaN(x)) return double.NaN;
        if (x < 0) return 2 - Erfc(-x);
        if (x < 1e-10) return 1 - (2 * x / SqrtPi);

        double t = x * x;

        if (double.IsInfinity(t)) return 0;

        double front = x * ExpOfMinusSquare(x, t) / SqrtPi;

        return t < 1.5 ? 1 - (front * GammaSeries(0.5, t)) : front * GammaFraction(0.5, t);
    }

    /// <summary>
    /// e^(−x²) с поправкой на округление квадрата: <paramref name="square"/> — уже посчитанное x·x.
    /// </summary>
    /// <remarks>
    /// Ошибка округления x² порядка x²·1e-16 попадает в показатель и при x = 6 даёт четыре единицы
    /// пятнадцатого знака. Остаток x·x − square восстанавливается точно одной операцией умножения со сложением.
    /// </remarks>
    private static double ExpOfMinusSquare(double x, double square) =>
        Math.Exp(-square) * (1 - Math.FusedMultiplyAdd(x, x, -square));

    /// <summary>Факториал <c>n!</c> числом двойной точности.</summary>
    /// <remarks>
    /// До <c>22!</c> значение точное, дальше — ближайший <see cref="double"/> к точному целому.
    /// При <c>n &gt; <see cref="MaxFactorialArgument"/></c> результат больше наибольшего
    /// <see cref="double"/> и возвращается <c>+∞</c>; для таких <c>n</c> работайте с
    /// <see cref="LogFactorial"/>. В <see cref="long"/> помещается только <c>20!</c>.
    /// </remarks>
    /// <param name="n">Целое неотрицательное число.</param>
    /// <exception cref="ArgumentOutOfRangeException">Число отрицательно.</exception>
    public static double Factorial(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);

        return n <= MaxFactorialArgument ? s_factorials[n] : double.PositiveInfinity;
    }

    /// <summary>Логарифм факториала ln n! — без переполнения при любом <c>n</c>.</summary>
    /// <param name="n">Целое неотрицательное число.</param>
    /// <exception cref="ArgumentOutOfRangeException">Число отрицательно.</exception>
    public static double LogFactorial(int n)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(n);

        return n <= MaxFactorialArgument ? Math.Log(s_factorials[n]) : LogGamma(n + 1.0);
    }

    /// <summary>
    /// ln Γ(1 + z) при малом z: −ln(1 + z) + z(1 − γ) + Σ (−1)^k (ζ(k) − 1) z^k / k.
    /// Без первого слагаемого это ln Γ(2 + z).
    /// </summary>
    private static double LogGammaNearOne(double z, bool includeLog)
    {
        double sum = 0, power = z;

        for (int k = 2; k < s_zetaMinusOne.Length + 2; k++)
        {
            power *= -z;

            double term = s_zetaMinusOne[k - 2] * power / k;

            sum += term;

            if (Math.Abs(term) < Epsilon * Math.Abs(sum)) break;
        }

        // power = (−z)^(k−1)·z, то есть знак слагаемого (−1)^(k−1); ряд требует (−1)^k
        double series = (z * (1 - EulerGamma)) - sum;

        return includeLog ? series - Log1p(z) : series;
    }

    /// <summary>
    /// Поправка ряда Стирлинга: ln Γ(x) = (x − ½) ln x − x + ½ ln 2π + поправка, x ≥ 10.
    /// </summary>
    private static double StirlingCorrection(double x)
    {
        double inverse = 1 / x, square = inverse * inverse;

        // 1/(12x) − 1/(360x³) + 1/(1260x⁵) − 1/(1680x⁷) + 1/(1188x⁹) − 691/(360360x¹¹) + 1/(156x¹³)
        return inverse * ((1.0 / 12) + (square * ((-1.0 / 360) + (square * ((1.0 / 1260) + (square * ((-1.0 / 1680)
            + (square * ((1.0 / 1188) + (square * ((-691.0 / 360360) + (square / 156))))))))))));
    }

    /// <summary>
    /// Общий множитель неполных гамма-функций: x^a · e^(−x) / Γ(a).
    /// </summary>
    /// <remarks>
    /// Напрямую это exp(a·ln x − x − ln Γ(a)): три слагаемых порядка a·ln a дают в сумме величину
    /// порядка ln a, и при a = 1e5 терялось бы десять знаков. Поэтому при больших a вместо Γ(a)
    /// подставляется формула Стирлинга, и степень сворачивается в a·[ln(1 + u) − u] при
    /// u = (x − a)/a — без вычитания больших чисел. При малых a множители считаются по отдельности.
    /// </remarks>
    private static double GammaPrefactor(double a, double x)
    {
        if (a < StirlingFrom) return Math.Pow(x, a) * Math.Exp(-x) / Gamma(a);

        double u = (x - a) / a;

        return Math.Exp((a * Log1pMinusX(u, x / a)) - StirlingCorrection(a)) * Math.Sqrt(a / (2 * Math.PI));
    }

    /// <summary>Ряд для P(a, x) без общего множителя: Σ x^n / (a (a + 1) … (a + n)).</summary>
    private static double GammaSeries(double a, double x)
    {
        double denominator = a, term = 1 / a, sum = term;
        int limit = 1000 + (int)Math.Min(20 * Math.Sqrt(a), 1e8);

        for (int n = 0; n < limit; n++)
        {
            denominator += 1;
            term *= x / denominator;
            sum += term;

            if (Math.Abs(term) < Math.Abs(sum) * Epsilon) break;
        }

        return sum;
    }

    /// <summary>Цепная дробь для Q(a, x) без общего множителя (метод Лентца).</summary>
    private static double GammaFraction(double a, double x)
    {
        double b = x + 1 - a, c = 1 / Tiny, d = 1 / b, h = d;
        int limit = 1000 + (int)Math.Min(20 * Math.Sqrt(a), 1e8);

        for (int i = 1; i <= limit; i++)
        {
            double an = -i * (i - a);

            b += 2;
            d = (an * d) + b;
            if (Math.Abs(d) < Tiny) d = Tiny;
            c = b + (an / c);
            if (Math.Abs(c) < Tiny) c = Tiny;
            d = 1 / d;

            double delta = d * c;

            h *= delta;

            if (Math.Abs(delta - 1) < 2 * Epsilon) break;
        }

        return h;
    }

    /// <summary>
    /// Общий множитель неполной бета-функции: x^a · y^b / B(a, b), где y = 1 − x.
    /// </summary>
    /// <remarks>
    /// При больших a и b показатель a·ln x + b·ln y − ln B — разность чисел порядка тысяч. Формула
    /// Стирлинга сворачивает её в a·[ln(1 + δ₁) − δ₁] + b·[ln(1 + δ₂) − δ₂], где δ — относительные
    /// отклонения x и y от a/(a + b) и b/(a + b): слагаемые одного знака, вычитания нет.
    /// </remarks>
    private static double BetaPrefactor(double a, double b, double x, double y)
    {
        if (a < StirlingFrom || b < StirlingFrom)
        {
            return Math.Exp((a * Math.Log(x)) + (b * Math.Log(y)) - LogBeta(a, b));
        }

        double sum = a + b;
        double first = ((x * sum) - a) / a, second = ((y * sum) - b) / b;
        double exponent = (a * Log1pMinusX(first, x * sum / a)) + (b * Log1pMinusX(second, y * sum / b));

        return Math.Exp(exponent + StirlingCorrection(sum) - StirlingCorrection(a) - StirlingCorrection(b))
            * Math.Sqrt(a * b / (2 * Math.PI * sum));
    }

    /// <summary>Цепная дробь неполной бета-функции (метод Лентца).</summary>
    private static double BetaFraction(double a, double b, double x)
    {
        double qab = a + b, qap = a + 1, qam = a - 1;
        double c = 1, d = 1 - (qab * x / qap);

        if (Math.Abs(d) < Tiny) d = Tiny;

        d = 1 / d;

        double h = d;
        int limit = 1000 + (int)Math.Min(20 * Math.Sqrt(Math.Max(a, b)), 1e8);

        for (int m = 1; m <= limit; m++)
        {
            int m2 = 2 * m;
            double aa = m * (b - m) * x / ((qam + m2) * (a + m2));

            d = 1 + (aa * d);
            if (Math.Abs(d) < Tiny) d = Tiny;
            c = 1 + (aa / c);
            if (Math.Abs(c) < Tiny) c = Tiny;
            d = 1 / d;
            h *= d * c;

            aa = -(a + m) * (qab + m) * x / ((a + m2) * (qap + m2));

            d = 1 + (aa * d);
            if (Math.Abs(d) < Tiny) d = Tiny;
            c = 1 + (aa / c);
            if (Math.Abs(c) < Tiny) c = Tiny;
            d = 1 / d;

            double delta = d * c;

            h *= delta;

            if (Math.Abs(delta - 1) < 2 * Epsilon) break;
        }

        return h;
    }

    /// <summary>ln(1 + x) без потери точности при малом x.</summary>
    private static double Log1p(double x)
    {
        double u = 1 + x;

        // Приём Кэхэна: ошибка округления 1 + x входит в числитель и знаменатель одинаково
        return u == 1 ? x : Math.Log(u) * x / (u - 1);
    }

    /// <summary>
    /// ln(1 + u) − u без вычитания близких чисел; <paramref name="onePlusU"/> — само 1 + u, посчитанное
    /// вызывающим как отношение.
    /// </summary>
    /// <remarks>
    /// Около нуля — ряд −u²/2 + u³/3 − …; вдали логарифм берётся от готового отношения: сумма 1 + u
    /// при u около −1 потеряла бы знаки.
    /// </remarks>
    private static double Log1pMinusX(double u, double onePlusU)
    {
        if (Math.Abs(u) > 0.4) return Math.Log(onePlusU) - u;

        double power = u, sum = 0;

        for (int k = 2; k < 200; k++)
        {
            power *= -u;

            double term = power / k;

            sum += term;

            if (Math.Abs(term) < Epsilon * Math.Abs(sum)) break;
        }

        return sum;
    }

    private static double[] BuildFactorials()
    {
        var table = new double[MaxFactorialArgument + 1];
        BigInteger value = BigInteger.One;

        table[0] = 1;

        // Точное целое и одно округление в конце: произведение в double копило бы ошибку на каждом шаге.
        // Округление делает разбор десятичной записи — он даёт ближайший double.
        for (int n = 1; n <= MaxFactorialArgument; n++)
        {
            value *= n;
            table[n] = double.Parse(value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);
        }

        return table;
    }
}
