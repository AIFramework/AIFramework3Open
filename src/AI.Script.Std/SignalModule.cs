using AI.DataStructs.Algebraic;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>signal</c>: генерация испытательных сигналов.
/// </summary>
/// <remarks>
/// Генераторы <c>AI.SignalLabs</c> — объекты с состоянием и <c>IDisposable</c>: они рассчитаны
/// на потоковую выдачу отсчётов, а прототипу нужен готовый вектор целиком. Поэтому здесь не
/// обёртка над ними, а прямая генерация: обёртка навязала бы скрипту жизненный цикл
/// объекта ради вызова, который умещается в одну строку.
/// <para>
/// Шум берётся из ГСЧ прогона, засеянного <c>options.seed</c>: два запуска одного скрипта
/// обязаны дать один и тот же сигнал, иначе сравнивать метрики фильтрации бессмысленно.
/// </para>
/// </remarks>
[ScriptModule("signal", "Генерация испытательных сигналов: синус, шум, импульсы", Version = "0.1")]
public static class SignalModule
{
    [ScriptFn("time", "Вектор моментов времени длительностью seconds при частоте fs",
        Example = "let t = signal.time(1, fs: 8000)")]
    public static Vector Time(
        IScriptContext context,
        [ScriptParam("длительность в секундах")] double seconds,
        [ScriptParam("частота дискретизации")] int fs = 8000)
    {
        int count = Count(seconds, fs, "signal.time");
        context.CountAllocation(count);

        var result = new Vector(count);

        for (int i = 0; i < count; i++) result[i] = (double)i / fs;

        return result;
    }

    [ScriptFn("sine", "Синусоида на заданной сетке времени",
        Example = "signal.sine(t, freq: 440, amp: 1)")]
    public static Vector Sine(
        [ScriptParam("вектор моментов времени")] Vector t,
        [ScriptParam("частота в герцах")] double freq,
        [ScriptParam("амплитуда")] double amp = 1,
        [ScriptParam("начальная фаза в радианах")] double phase = 0)
    {
        var result = new Vector(t.Count);

        for (int i = 0; i < t.Count; i++) result[i] = amp * Math.Sin((2 * Math.PI * freq * t[i]) + phase);

        return result;
    }

    [ScriptFn("square", "Меандр на заданной сетке времени", Example = "signal.square(t, freq: 50)")]
    public static Vector Square(
        [ScriptParam("вектор моментов времени")] Vector t,
        [ScriptParam("частота в герцах")] double freq,
        [ScriptParam("амплитуда")] double amp = 1)
    {
        var result = new Vector(t.Count);

        for (int i = 0; i < t.Count; i++) result[i] = Math.Sin(2 * Math.PI * freq * t[i]) >= 0 ? amp : -amp;

        return result;
    }

    [ScriptFn("chirp", "Сигнал с линейно нарастающей частотой",
        Example = "signal.chirp(t, from: 100, to: 2000)")]
    public static Vector Chirp(
        [ScriptParam("вектор моментов времени")] Vector t,
        [ScriptParam("начальная частота")] double from,
        [ScriptParam("конечная частота")] double to,
        [ScriptParam("амплитуда")] double amp = 1)
    {
        if (t.Count == 0) return new Vector(0);

        double duration = t[^1] - t[0];

        if (duration <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "signal.chirp: сетка времени не возрастает");

        double rate = (to - from) / duration;
        var result = new Vector(t.Count);

        for (int i = 0; i < t.Count; i++)
        {
            double time = t[i] - t[0];
            result[i] = amp * Math.Sin(2 * Math.PI * ((from * time) + (rate * time * time / 2)));
        }

        return result;
    }

    [ScriptFn("noise", "Белый гауссов шум", Example = "signal.noise(8000, sigma: 0.2)")]
    public static Vector Noise(
        IScriptContext context,
        [ScriptParam("число отсчётов")] int count,
        [ScriptParam("среднеквадратичное отклонение")] double sigma = 1,
        [ScriptParam("среднее")] double mean = 0)
    {
        if (count < 0) throw new ScriptError(DiagnosticCodes.BadOperand, "signal.noise: длина отрицательна");

        context.CountAllocation(count);

        var result = new Vector(count);

        for (int i = 0; i < count; i++)
        {
            double u1 = 1.0 - context.Random.NextDouble();
            double u2 = context.Random.NextDouble();

            result[i] = mean + (sigma * Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2));
        }

        return result;
    }

    [ScriptFn("impulse", "Единичный импульс в заданной позиции", Example = "signal.impulse(1024, at: 0)")]
    public static Vector Impulse(
        IScriptContext context,
        [ScriptParam("число отсчётов")] int count,
        [ScriptParam("позиция импульса")] int at = 0,
        [ScriptParam("амплитуда")] double amp = 1)
    {
        if (count < 0) throw new ScriptError(DiagnosticCodes.BadOperand, "signal.impulse: длина отрицательна");
        context.CountAllocation(count);

        var result = new Vector(count);

        if (at >= 0 && at < count) result[at] = amp;

        return result;
    }

    private static int Count(double seconds, int fs, string what)
    {
        if (fs <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: частота дискретизации должна быть больше нуля");
        if (seconds <= 0) throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: длительность должна быть больше нуля");

        return (int)Math.Round(seconds * fs);
    }
}
