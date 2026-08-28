using AI.Script.Binding;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>math</c>: элементарные функции над числами.
/// </summary>
/// <remarks>
/// Все функции работают со скалярами. Поэлементные операции над векторами делают операторы
/// (<c>v * 2</c>, <c>v + w</c>) и пространство <c>vec</c>.
/// </remarks>
[ScriptModule("math", "Элементарные математические функции над числами", Version = "0.1")]
public static class MathModule
{
    [ScriptFn("abs", "Модуль числа", Example = "math.abs(-3)")]
    public static double Abs([ScriptParam("число")] double x) => Math.Abs(x);

    [ScriptFn("sqrt", "Квадратный корень", Example = "math.sqrt(2)")]
    public static double Sqrt([ScriptParam("число")] double x) => Math.Sqrt(x);

    [ScriptFn("cbrt", "Кубический корень", Example = "math.cbrt(27)")]
    public static double Cbrt([ScriptParam("число")] double x) => Math.Cbrt(x);

    [ScriptFn("exp", "Экспонента", Example = "math.exp(1)")]
    public static double Exp([ScriptParam("число")] double x) => Math.Exp(x);

    [ScriptFn("log", "Натуральный логарифм либо логарифм по основанию", Example = "math.log(8, base: 2)")]
    public static double Log(
        [ScriptParam("число")] double x,
        [ScriptParam("основание; по умолчанию e")] double @base = double.NaN)
        => double.IsNaN(@base) ? Math.Log(x) : Math.Log(x, @base);

    [ScriptFn("log10", "Десятичный логарифм", Example = "math.log10(1000)")]
    public static double Log10([ScriptParam("число")] double x) => Math.Log10(x);

    [ScriptFn("log2", "Двоичный логарифм", Example = "math.log2(1024)")]
    public static double Log2([ScriptParam("число")] double x) => Math.Log2(x);

    [ScriptFn("pow", "Возведение в степень", Example = "math.pow(2, 10)")]
    public static double Pow(
        [ScriptParam("основание")] double x,
        [ScriptParam("показатель")] double y)
        => Math.Pow(x, y);

    [ScriptFn("sin", "Синус (радианы)", Example = "math.sin(pi / 2)")]
    public static double Sin([ScriptParam("угол в радианах")] double x) => Math.Sin(x);

    [ScriptFn("cos", "Косинус (радианы)", Example = "math.cos(0)")]
    public static double Cos([ScriptParam("угол в радианах")] double x) => Math.Cos(x);

    [ScriptFn("tan", "Тангенс (радианы)", Example = "math.tan(0.5)")]
    public static double Tan([ScriptParam("угол в радианах")] double x) => Math.Tan(x);

    [ScriptFn("asin", "Арксинус", Example = "math.asin(0.5)")]
    public static double Asin([ScriptParam("число")] double x) => Math.Asin(x);

    [ScriptFn("acos", "Арккосинус", Example = "math.acos(0.5)")]
    public static double Acos([ScriptParam("число")] double x) => Math.Acos(x);

    [ScriptFn("atan", "Арктангенс", Example = "math.atan(1)")]
    public static double Atan([ScriptParam("число")] double x) => Math.Atan(x);

    [ScriptFn("atan2", "Арктангенс отношения с учётом четверти", Example = "math.atan2(y, x)")]
    public static double Atan2(
        [ScriptParam("ордината")] double y,
        [ScriptParam("абсцисса")] double x)
        => Math.Atan2(y, x);

    [ScriptFn("sinh", "Гиперболический синус", Example = "math.sinh(1)")]
    public static double Sinh([ScriptParam("число")] double x) => Math.Sinh(x);

    [ScriptFn("cosh", "Гиперболический косинус", Example = "math.cosh(1)")]
    public static double Cosh([ScriptParam("число")] double x) => Math.Cosh(x);

    [ScriptFn("tanh", "Гиперболический тангенс", Example = "math.tanh(1)")]
    public static double Tanh([ScriptParam("число")] double x) => Math.Tanh(x);

    [ScriptFn("floor", "Округление вниз", Example = "math.floor(2.7)")]
    public static double Floor([ScriptParam("число")] double x) => Math.Floor(x);

    [ScriptFn("ceil", "Округление вверх", Example = "math.ceil(2.1)")]
    public static double Ceil([ScriptParam("число")] double x) => Math.Ceiling(x);

    [ScriptFn("trunc", "Отбрасывание дробной части", Example = "math.trunc(-2.7)")]
    public static double Trunc([ScriptParam("число")] double x) => Math.Truncate(x);

    [ScriptFn("sign", "Знак числа: -1, 0 либо 1", Example = "math.sign(-5)")]
    public static double Sign([ScriptParam("число")] double x) => Math.Sign(x);

    [ScriptFn("min", "Наименьшее из чисел", Example = "math.min(3, 7)")]
    public static double Min(
        [ScriptParam("первое число")] double a,
        [ScriptParam("второе число")] double b)
        => Math.Min(a, b);

    [ScriptFn("max", "Наибольшее из чисел", Example = "math.max(3, 7)")]
    public static double Max(
        [ScriptParam("первое число")] double a,
        [ScriptParam("второе число")] double b)
        => Math.Max(a, b);

    [ScriptFn("clamp", "Ограничивает число отрезком", Example = "math.clamp(x, low: 0, high: 1)")]
    public static double Clamp(
        [ScriptParam("число")] double x,
        [ScriptParam("нижняя граница")] double low = 0,
        [ScriptParam("верхняя граница")] double high = 1)
        => Math.Clamp(x, low, high);

    /// <summary>
    /// Сравнение вещественных чисел с допуском.
    /// </summary>
    /// <remarks>
    /// Оператор <c>==</c> сравнивает числа точно и остаётся таким: подмешивать эпсилон в него
    /// значило бы сломать проверку <c>x == 0</c> там, где нужен именно ноль. Для сравнения
    /// результатов счёта существует эта функция.
    /// </remarks>
    [ScriptFn("approx", "Равны ли числа с точностью до допуска", Example = "math.approx(a, b, eps: 1e-6)")]
    public static bool Approx(
        [ScriptParam("первое число")] double a,
        [ScriptParam("второе число")] double b,
        [ScriptParam("абсолютный допуск")] double eps = 1e-9)
        => Math.Abs(a - b) <= eps;

    [ScriptFn("hypot", "Гипотенуза по катетам", Example = "math.hypot(3, 4)")]
    public static double Hypot(
        [ScriptParam("первый катет")] double a,
        [ScriptParam("второй катет")] double b)
        => Math.Sqrt((a * a) + (b * b));

    [ScriptFn("degrees", "Радианы в градусы", Example = "math.degrees(pi)")]
    public static double Degrees([ScriptParam("угол в радианах")] double x) => x * 180.0 / Math.PI;

    [ScriptFn("radians", "Градусы в радианы", Example = "math.radians(180)")]
    public static double Radians([ScriptParam("угол в градусах")] double x) => x * Math.PI / 180.0;

    [ScriptFn("factorial", "Факториал целого неотрицательного числа", Example = "math.factorial(5)")]
    public static double Factorial([ScriptParam("число")] int n)
    {
        if (n < 0) throw new ArgumentOutOfRangeException(nameof(n), "факториал определён для неотрицательных чисел");

        double result = 1;
        for (int i = 2; i <= n; i++) result *= i;

        return result;
    }

    [ScriptFn("gcd", "Наибольший общий делитель", Example = "math.gcd(12, 18)")]
    public static double Gcd(
        [ScriptParam("первое число")] long a,
        [ScriptParam("второе число")] long b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);

        while (b != 0) (a, b) = (b, a % b);

        return a;
    }

    [ScriptFn("lcm", "Наименьшее общее кратное", Example = "math.lcm(4, 6)")]
    public static double Lcm(
        [ScriptParam("первое число")] long a,
        [ScriptParam("второе число")] long b)
        => a == 0 || b == 0 ? 0 : Math.Abs(a / (long)Gcd(a, b) * b);

    [ScriptFn("is_nan", "Является ли значение «не числом»", Example = "math.is_nan(x)")]
    public static bool IsNan([ScriptParam("число")] double x) => double.IsNaN(x);

    [ScriptFn("is_finite", "Конечно ли число", Example = "math.is_finite(x)")]
    public static bool IsFinite([ScriptParam("число")] double x) => double.IsFinite(x);

    /// <summary>
    /// Равномерное случайное число.
    /// </summary>
    /// <remarks>
    /// ГСЧ берётся из контекста прогона, а не из статики: зерно объявляется в
    /// <c>options.seed</c>, и два прогона в одном процессе не должны влиять друг на друга.
    /// </remarks>
    [ScriptFn("random", "Случайное число из [low, high)", Example = "math.random(low: 0, high: 1)")]
    public static double Random(
        IScriptContext context,
        [ScriptParam("нижняя граница")] double low = 0,
        [ScriptParam("верхняя граница")] double high = 1)
        => low + (context.Random.NextDouble() * (high - low));

    [ScriptFn("randint", "Случайное целое из [low, high)", Example = "math.randint(low: 1, high: 7)")]
    public static double RandInt(
        IScriptContext context,
        [ScriptParam("нижняя граница")] int low = 0,
        [ScriptParam("верхняя граница")] int high = 2)
        => context.Random.Next(low, high);

    [ScriptFn("gauss", "Случайное число из нормального распределения", Example = "math.gauss(mean: 0, std: 1)")]
    public static double Gauss(
        IScriptContext context,
        [ScriptParam("среднее")] double mean = 0,
        [ScriptParam("среднеквадратичное отклонение")] double std = 1)
    {
        double u1 = 1.0 - context.Random.NextDouble();
        double u2 = context.Random.NextDouble();

        return mean + (std * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
    }
}
