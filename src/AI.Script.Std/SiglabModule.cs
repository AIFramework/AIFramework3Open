using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.Script.Binding;
using AI.Script.Runtime;
using AI.Script.Semantics;
using AI.SignalLab.AGC;
using AI.SignalLab.Filters;
using AI.SignalLab.Modulation.Demodulation;
using AI.SignalLab.Modulation.Modulation.DigitalModulations;
using System.Text;

// System.Numerics вносит собственный Vector, а здесь Vector — это вектор фреймворка:
// из пространства берётся только комплексное число.
using Complex = System.Numerics.Complex;

namespace AI.Script.Std;

/// <summary>
/// Пространство <c>siglab</c>: цифровая модуляция, демодуляция и приёмный тракт.
/// </summary>
/// <remarks>
/// Дополняет <c>signal</c> и <c>dsp</c>, а не заменяет их: там генерация и спектральный
/// анализ, здесь передача бит по каналу. Генераторы библиотеки с потоковой выдачей отсчётов
/// сюда намеренно не попали — они требуют жизненного цикла объекта ради вызова в одну строку,
/// а готовый вектор уже даёт <c>signal</c>.
/// <para>
/// Биты представлены вектором из нулей и единиц, а не списком логических значений: над ними
/// сразу работают <c>vec.sum</c>, срезы и сравнения, а отдельный тип битовой строки пришлось
/// бы обслуживать во всём языке ради одного пространства.
/// </para>
/// </remarks>
[ScriptModule("siglab", "Радиоканал: модуляция, демодуляция, АРУ, формирующий фильтр", Version = "0.1")]
public static class SiglabModule
{
    // --- биты ---

    [ScriptFn("to_bits", "Биты текста вектором нулей и единиц", Example = "siglab.to_bits(\"привет\")")]
    public static Vector ToBits(
        IScriptContext context,
        [ScriptParam("текст")] string text)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(text ?? string.Empty);
        var bits = new Vector(bytes.Length * 8);

        context.CountAllocation(bits.Count);

        for (int i = 0; i < bytes.Length; i++)
        {
            for (int b = 0; b < 8; b++) bits[(i * 8) + b] = (bytes[i] >> b & 1) != 0 ? 1 : 0;
        }

        return bits;
    }

    [ScriptFn("to_text", "Текст из вектора бит", Example = "siglab.to_text(принятые)")]
    public static string ToText(
        [ScriptParam("вектор бит")] Vector bits)
    {
        if (bits.Count % 8 != 0)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"siglab.to_text: бит {bits.Count}, а нужно кратное восьми",
                "обрежьте хвост: bits[0..(len(bits) - len(bits) % 8)]");
        }

        var bytes = new byte[bits.Count / 8];

        for (int i = 0; i < bytes.Length; i++)
        {
            int value = 0;

            for (int b = 0; b < 8; b++)
            {
                if (bits[(i * 8) + b] != 0) value |= 1 << b;
            }

            bytes[i] = (byte)value;
        }

        return Encoding.UTF8.GetString(bytes);
    }

    /// <summary>
    /// Доля неверно принятых бит.
    /// </summary>
    /// <remarks>
    /// Считается здесь, а не берётся из библиотеки: это одна строка, но без неё сравнить
    /// модуляции между собой нечем, и каждый скрипт писал бы её заново — по-своему и с той же
    /// вероятностью ошибиться в длине.
    /// </remarks>
    [ScriptFn("ber", "Доля ошибочных бит между переданным и принятым", Example = "siglab.ber(отправлено, принято)")]
    public static double Ber(
        [ScriptParam("переданные биты")] Vector sent,
        [ScriptParam("принятые биты")] Vector received)
    {
        if (sent.Count == 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "siglab.ber: передавать нечего");

        int length = Math.Min(sent.Count, received.Count);
        int errors = Math.Abs(sent.Count - received.Count);

        for (int i = 0; i < length; i++)
        {
            if (Bit(sent[i]) != Bit(received[i])) errors++;
        }

        return (double)errors / sent.Count;
    }

    // --- амплитудная манипуляция ---

    [ScriptFn("ask", "Амплитудная манипуляция: биты в сигнал",
        Example = "siglab.ask(биты, carrier: 2000, fs: 48000, bit_duration: 0.001)")]
    public static Vector Ask(
        IScriptContext context,
        [ScriptParam("вектор бит")] Vector bits,
        [ScriptParam("несущая частота, Гц")] double carrier,
        [ScriptParam("частота дискретизации, Гц")] double fs,
        [ScriptParam("длительность бита, с")] double bit_duration = 1e-3,
        [ScriptParam("амплитуда единицы")] double high = 1,
        [ScriptParam("амплитуда нуля")] double low = 0)
    {
        RequireChannel(carrier, fs, bit_duration, "siglab.ask");

        context.CountAllocation((long)(bits.Count * bit_duration * fs));

        return new ASK(carrier, fs, bit_duration, high, low).Modulate(Bools(bits));
    }

    [ScriptFn("ask_demod", "Амплитудная демодуляция: сигнал в биты",
        Example = "siglab.ask_demod(сигнал, carrier: 2000, fs: 48000, bit_duration: 0.001)")]
    public static Vector AskDemod(
        IScriptContext context,
        [ScriptParam("принятый сигнал")] Vector signal,
        [ScriptParam("несущая частота, Гц")] double carrier,
        [ScriptParam("частота дискретизации, Гц")] double fs,
        [ScriptParam("длительность бита, с")] double bit_duration = 1e-3)
    {
        RequireChannel(carrier, fs, bit_duration, "siglab.ask_demod");

        bool[] bits = new ASK(carrier, fs, bit_duration).Demodulate(signal);

        context.CountAllocation(bits.Length);

        return Bits(bits);
    }

    // --- квадратурные модуляции ---

    /// <summary>
    /// Отображение бит в точки созвездия.
    /// </summary>
    /// <remarks>
    /// Символы отдаются двумя вещественными векторами, а не комплексным: комплексных операций
    /// в языке нет, и дескриптор с ними был бы вещью, которую нельзя ни напечатать, ни
    /// нарисовать. Две колонки рисует <c>plot.scatter</c> как есть.
    /// </remarks>
    [ScriptFn("iq", "Биты в IQ-символы выбранной модуляции", Returns = "record",
        Example = "let s = siglab.iq(биты, kind: \"qpsk\")")]
    public static ScriptRecord Iq(
        IScriptContext context,
        [ScriptParam("вектор бит")] Vector bits,
        [ScriptParam("модуляция: \"bpsk\", \"qpsk\", \"qam8\" либо \"qam16\"")] string kind = "qpsk")
    {
        BaseIQModulation modulation = Modulation(kind, "siglab.iq");
        Complex[] symbols = modulation.MapBitsToSymbols(Bools(bits));

        context.CountAllocation(symbols.Length * 2L);

        return Pair(symbols, ("бит_на_символ", ScriptValue.Num(modulation.BitsPerSymbol)));
    }

    [ScriptFn("iq_bits", "IQ-символы обратно в биты по ближайшей точке созвездия",
        Example = "siglab.iq_bits(s.i, s.q, kind: \"qpsk\", bits: len(биты))")]
    public static Vector IqBits(
        IScriptContext context,
        [ScriptParam("синфазная составляющая")] Vector i,
        [ScriptParam("квадратурная составляющая")] Vector q,
        [ScriptParam("модуляция")] string kind = "qpsk",
        [ScriptParam("сколько бит ожидается; 0 — все")] int bits = 0)
    {
        if (i.Count != q.Count)
        {
            throw new ScriptError(
                DiagnosticCodes.SizeMismatch,
                $"siglab.iq_bits: составляющих I — {i.Count}, а Q — {q.Count}");
        }

        BaseIQModulation modulation = Modulation(kind, "siglab.iq_bits");
        int expected = bits > 0 ? bits : i.Count * modulation.BitsPerSymbol;

        var symbols = new ComplexVector(i.Count);

        for (int k = 0; k < i.Count; k++) symbols[k] = new Complex(i[k], q[k]);

        bool[] decoded = modulation.DemapSymbolsToBits(symbols, expected);

        context.CountAllocation(decoded.Length);

        return Bits(decoded);
    }

    [ScriptFn("constellation", "Точки созвездия модуляции", Returns = "record",
        Example = "show plot.scatter(x: siglab.constellation(\"qam16\").i, y: siglab.constellation(\"qam16\").q)")]
    public static ScriptRecord Constellation(
        [ScriptParam("модуляция")] string kind)
    {
        BaseIQModulation modulation = Modulation(kind, "siglab.constellation");

        return Pair(modulation.Constellation, ("бит_на_символ", ScriptValue.Num(modulation.BitsPerSymbol)));
    }

    /// <summary>
    /// Квадратурное разложение принятого сигнала.
    /// </summary>
    /// <remarks>
    /// Даёт то, с чего начинается любой приёмник: снос на нулевую частоту и фильтрация.
    /// Задержка фильтра возвращается вместе с составляющими — без неё символы окажутся
    /// сдвинуты, и созвездие рассыплется по кругу без видимой причины.
    /// </remarks>
    [ScriptFn("quadrature", "Квадратурное разложение сигнала: составляющие I и Q", Returns = "record",
        Example = "let iq = siglab.quadrature(сигнал, carrier: 2000, fs: 48000)")]
    public static ScriptRecord Quadrature(
        IScriptContext context,
        [ScriptParam("принятый сигнал")] Vector signal,
        [ScriptParam("несущая частота, Гц")] double carrier,
        [ScriptParam("частота дискретизации, Гц")] double fs,
        [ScriptParam("частота среза фильтра, Гц; 0 — по несущей")] double cutoff = 0)
    {
        if (carrier <= 0 || fs <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "siglab.quadrature: частоты должны быть положительны");

        var demodulator = new QudratureDemodulation(carrier, fs, cutoff);
        ComplexVector iq = demodulator.GetIQComponents(signal);

        context.CountAllocation(iq.Count * 2L);

        var i = new Vector(iq.Count);
        var q = new Vector(iq.Count);

        for (int k = 0; k < iq.Count; k++)
        {
            i[k] = iq[k].Real;
            q[k] = iq[k].Imaginary;
        }

        return ScriptRecord.From(
        [
            new("i", ScriptValue.Vec(i)),
            new("q", ScriptValue.Vec(q)),
            new("задержка", ScriptValue.Num(demodulator.FilterDelay)),
        ]);
    }

    // --- приёмный тракт ---

    /// <summary>
    /// Автоматическая регулировка усиления.
    /// </summary>
    /// <remarks>
    /// Проходит по отсчётам подряд, потому что регулировка по определению зависит от того,
    /// что было раньше. Это единственная функция пространства, где порядок отсчётов меняет
    /// результат, и параллелить её нельзя.
    /// </remarks>
    [ScriptFn("agc", "Автоматическая регулировка усиления", Example = "siglab.agc(сигнал)")]
    public static Vector Agc(
        IScriptContext context,
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("порог ограничения")] double threshold = 4)
    {
        var regulator = new DirectAGC { TresholdAGC = threshold };
        var output = new Vector(signal.Count);

        context.CountAllocation(signal.Count);

        for (int i = 0; i < signal.Count; i++) output[i] = regulator.Calculate(signal[i]);

        return output;
    }

    [ScriptFn("rrc", "Ядро формирующего фильтра «корень из приподнятого косинуса»",
        Example = "siglab.rrc(symbol_period: 0.001, fs: 48000, roll_off: 0.35)")]
    public static Vector Rrc(
        IScriptContext context,
        [ScriptParam("длительность символа, с")] double symbol_period,
        [ScriptParam("частота дискретизации, Гц")] double fs,
        [ScriptParam("коэффициент скругления от 0 до 1")] double roll_off = 0.35,
        [ScriptParam("длительность в символах")] int span = 6)
    {
        if (symbol_period <= 0 || fs <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "siglab.rrc: период и частота должны быть положительны");

        if (roll_off is < 0 or > 1)
            throw new ScriptError(DiagnosticCodes.BadOperand, "siglab.rrc: скругление лежит в [0, 1]");

        Vector kernel = RootRaisedCosineFilter.ComputeKernel(symbol_period, fs, roll_off, span);

        context.CountAllocation(kernel.Count);

        return kernel;
    }

    [ScriptFn("shape", "Формирование импульсов фильтром «корень из приподнятого косинуса»",
        Example = "siglab.shape(сигнал, symbol_period: 0.001, fs: 48000)")]
    public static Vector Shape(
        IScriptContext context,
        [ScriptParam("сигнал")] Vector signal,
        [ScriptParam("длительность символа, с")] double symbol_period,
        [ScriptParam("частота дискретизации, Гц")] double fs,
        [ScriptParam("коэффициент скругления от 0 до 1")] double roll_off = 0.35,
        [ScriptParam("длительность в символах")] int span = 6)
    {
        if (symbol_period <= 0 || fs <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, "siglab.shape: период и частота должны быть положительны");

        context.CountAllocation(signal.Count);

        return new RootRaisedCosineFilter(symbol_period, fs, roll_off, span).FilterOutp(signal);
    }

    // --- внутреннее ---

    private static BaseIQModulation Modulation(string kind, string what) => kind switch
    {
        "bpsk" => new BPSK(),
        "qpsk" => new QPSK(),
        "qam8" => new QAM8(),
        "qam16" => new QAM16(),
        _ => throw new ScriptError(
            DiagnosticCodes.BadOperand,
            $"{what}: неизвестная модуляция '{kind}'",
            "известны: \"bpsk\" — 1 бит на символ, \"qpsk\" — 2, \"qam8\" — 3, \"qam16\" — 4"),
    };

    private static void RequireChannel(double carrier, double fs, double bitDuration, string what)
    {
        if (carrier <= 0 || fs <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: частоты должны быть положительны");

        if (bitDuration <= 0)
            throw new ScriptError(DiagnosticCodes.BadOperand, $"{what}: длительность бита должна быть положительной");

        if (carrier * 2 > fs)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{what}: несущая {carrier} Гц выше частоты Найквиста {fs / 2} Гц",
                "поднимите частоту дискретизации либо опустите несущую");
        }

        if (bitDuration * fs < 2)
        {
            throw new ScriptError(
                DiagnosticCodes.BadOperand,
                $"{what}: на бит приходится меньше двух отсчётов",
                "увеличьте длительность бита либо частоту дискретизации");
        }
    }

    /// <summary>Ноль — это ноль, всё остальное — единица: биты приходят из вектора чисел.</summary>
    private static bool Bit(double value) => value != 0;

    private static bool[] Bools(Vector bits)
    {
        var result = new bool[bits.Count];

        for (int i = 0; i < bits.Count; i++) result[i] = Bit(bits[i]);

        return result;
    }

    private static Vector Bits(bool[] bits)
    {
        var result = new Vector(bits.Length);

        for (int i = 0; i < bits.Length; i++) result[i] = bits[i] ? 1 : 0;

        return result;
    }

    private static ScriptRecord Pair(IReadOnlyList<Complex> symbols, params (string Name, ScriptValue Value)[] extra)
    {
        var i = new Vector(symbols.Count);
        var q = new Vector(symbols.Count);

        for (int k = 0; k < symbols.Count; k++)
        {
            i[k] = symbols[k].Real;
            q[k] = symbols[k].Imaginary;
        }

        var fields = new List<KeyValuePair<string, ScriptValue>>(extra.Length + 2)
        {
            new("i", ScriptValue.Vec(i)),
            new("q", ScriptValue.Vec(q)),
        };

        foreach ((string name, ScriptValue value) in extra)
            fields.Add(new KeyValuePair<string, ScriptValue>(name, value));

        return ScriptRecord.From(fields);
    }
}
