using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>vec</c>: создание и преобразование числовых векторов.
/// </summary>
/// <remarks>
/// Создающие функции учитывают выделение через <see cref="IScriptContext.CountAllocation"/>:
/// <c>vec.zeros(1e12)</c> укладывает процесс за один шаг интерпретатора, и потолок шагов от
/// этого не спасает.
/// </remarks>
[ScriptModule("vec", "Числовые векторы: создание, срезы, свёртки", Version = "0.1")]
public static class VecModule
{
    [ScriptFn("zeros", "Вектор из нулей", Example = "vec.zeros(10)")]
    public static Vector Zeros(IScriptContext context, [ScriptParam("длина")] int n)
    {
        Guard(context, n);
        return new Vector(n);
    }

    [ScriptFn("ones", "Вектор из единиц", Example = "vec.ones(10)")]
    public static Vector Ones(IScriptContext context, [ScriptParam("длина")] int n)
    {
        Guard(context, n);

        var vector = new Vector(n);
        for (int i = 0; i < n; i++) vector[i] = 1;

        return vector;
    }

    [ScriptFn("full", "Вектор из одинаковых значений", Example = "vec.full(5, value: 3)")]
    public static Vector Full(
        IScriptContext context,
        [ScriptParam("длина")] int n,
        [ScriptParam("значение")] double value = 0)
    {
        Guard(context, n);

        var vector = new Vector(n);
        for (int i = 0; i < n; i++) vector[i] = value;

        return vector;
    }

    [ScriptFn("linspace", "Равномерная сетка из n точек на отрезке", Example = "vec.linspace(0, 1, n: 100)")]
    public static Vector Linspace(
        IScriptContext context,
        [ScriptParam("начало отрезка")] double from,
        [ScriptParam("конец отрезка, включается")] double to,
        [ScriptParam("число точек")] int n = 100)
    {
        Guard(context, n);

        if (n < 2) throw new ScriptError(DiagnosticCodes.BadOperand, "vec.linspace: нужно не меньше двух точек");

        var vector = new Vector(n);
        double step = (to - from) / (n - 1);

        for (int i = 0; i < n; i++) vector[i] = from + (step * i);

        return vector;
    }

    [ScriptFn("arange", "Сетка с заданным шагом; конец не включается", Example = "vec.arange(0, 10, by: 0.5)")]
    public static Vector Arange(
        IScriptContext context,
        [ScriptParam("начало")] double from,
        [ScriptParam("конец, не включается")] double to,
        [ScriptParam("шаг")] double by = 1)
    {
        if (by == 0) throw new ScriptError(DiagnosticCodes.BadOperand, "vec.arange: шаг равен нулю");

        int count = (int)Math.Max(0, Math.Ceiling((to - from) / by));
        Guard(context, count);

        var vector = new Vector(count);
        for (int i = 0; i < count; i++) vector[i] = from + (by * i);

        return vector;
    }

    [ScriptFn("of", "Собирает вектор из последовательности чисел", Example = "vec.of([1, 2, 3])")]
    public static Vector Of([ScriptParam("последовательность чисел")] Vector values) => values;

    [ScriptFn("concat", "Склеивает два вектора", Example = "vec.concat(a, b)")]
    public static Vector Concat(
        [ScriptParam("первый вектор")] Vector a,
        [ScriptParam("второй вектор")] Vector b)
    {
        var result = new Vector(a.Count + b.Count);

        for (int i = 0; i < a.Count; i++) result[i] = a[i];
        for (int i = 0; i < b.Count; i++) result[a.Count + i] = b[i];

        return result;
    }

    [ScriptFn("reverse", "Разворачивает вектор", Example = "v |> vec.reverse()")]
    public static Vector Reverse([ScriptParam("вектор")] Vector v)
    {
        var result = new Vector(v.Count);

        for (int i = 0; i < v.Count; i++) result[i] = v[v.Count - 1 - i];

        return result;
    }

    [ScriptFn("sort", "Сортирует вектор по возрастанию", Example = "v |> vec.sort()")]
    public static Vector Sort(
        [ScriptParam("вектор")] Vector v,
        [ScriptParam("по убыванию")] bool desc = false)
    {
        double[] data = v.ToArray();
        Array.Sort(data);

        if (desc) Array.Reverse(data);

        return new Vector(data);
    }

    [ScriptFn("sum", "Сумма элементов", Example = "vec.sum(v)")]
    public static double Sum([ScriptParam("вектор")] Vector v) => v.Sum();

    [ScriptFn("prod", "Произведение элементов", Example = "vec.prod(v)")]
    public static double Prod([ScriptParam("вектор")] Vector v)
    {
        double result = 1;

        for (int i = 0; i < v.Count; i++) result *= v[i];

        return result;
    }

    [ScriptFn("dot", "Скалярное произведение", Example = "vec.dot(a, b)")]
    public static double Dot(
        [ScriptParam("первый вектор")] Vector a,
        [ScriptParam("второй вектор")] Vector b)
    {
        RequireSameLength(a, b, "vec.dot");

        double sum = 0;
        for (int i = 0; i < a.Count; i++) sum += a[i] * b[i];

        return sum;
    }

    [ScriptFn("norm", "Евклидова норма", Example = "vec.norm(v)")]
    public static double Norm([ScriptParam("вектор")] Vector v) => v.NormL2();

    [ScriptFn("cumsum", "Накопленная сумма", Example = "v |> vec.cumsum()")]
    public static Vector CumSum([ScriptParam("вектор")] Vector v)
    {
        var result = new Vector(v.Count);
        double sum = 0;

        for (int i = 0; i < v.Count; i++)
        {
            sum += v[i];
            result[i] = sum;
        }

        return result;
    }

    [ScriptFn("diff", "Разности соседних элементов; длина уменьшается на единицу", Example = "v |> vec.diff()")]
    public static Vector Diff([ScriptParam("вектор")] Vector v)
    {
        if (v.Count < 2) return new Vector(0);

        var result = new Vector(v.Count - 1);

        for (int i = 1; i < v.Count; i++) result[i - 1] = v[i] - v[i - 1];

        return result;
    }

    [ScriptFn("abs", "Поэлементный модуль", Example = "v |> vec.abs()")]
    public static Vector Abs([ScriptParam("вектор")] Vector v) => v.Transform(Math.Abs);

    [ScriptFn("clip", "Ограничивает элементы отрезком", Example = "v |> vec.clip(low: 0, high: 1)")]
    public static Vector Clip(
        [ScriptParam("вектор")] Vector v,
        [ScriptParam("нижняя граница")] double low = 0,
        [ScriptParam("верхняя граница")] double high = 1)
        => v.Transform(x => Math.Clamp(x, low, high));

    [ScriptFn("slice", "Часть вектора [from, to)", Example = "v |> vec.slice(from: 0, to: 10)")]
    public static Vector Slice(
        [ScriptParam("вектор")] Vector v,
        [ScriptParam("начало включительно")] int from = 0,
        [ScriptParam("конец исключительно; -1 — до конца")] int to = -1)
    {
        int end = to < 0 ? v.Count : Math.Min(to, v.Count);
        int start = Math.Clamp(from, 0, end);
        var result = new Vector(end - start);

        for (int i = start; i < end; i++) result[i - start] = v[i];

        return result;
    }

    [ScriptFn("argmax", "Индекс наибольшего элемента", Example = "vec.argmax(v)")]
    public static double ArgMax([ScriptParam("вектор")] Vector v) =>
        v.Count > 0 ? v.MaxElementIndex() : throw EmptyVector("argmax");

    [ScriptFn("argmin", "Индекс наименьшего элемента", Example = "vec.argmin(v)")]
    public static double ArgMin([ScriptParam("вектор")] Vector v) =>
        v.Count > 0 ? v.MinElementIndex() : throw EmptyVector("argmin");

    private static void Guard(IScriptContext context, int n)
    {
        if (n < 0) throw new ScriptError(DiagnosticCodes.BadOperand, "длина вектора отрицательна");

        context.CountAllocation(n);
    }

    private static void RequireSameLength(Vector a, Vector b, string what)
    {
        if (a.Count == b.Count) return;

        throw new ScriptError(
            DiagnosticCodes.SizeMismatch,
            $"{what}: несовместимые размеры {a.Count} и {b.Count}");
    }

    private static ScriptError EmptyVector(string name) =>
        new(DiagnosticCodes.IndexOutOfRange, $"vec.{name}: вектор пуст");
}
