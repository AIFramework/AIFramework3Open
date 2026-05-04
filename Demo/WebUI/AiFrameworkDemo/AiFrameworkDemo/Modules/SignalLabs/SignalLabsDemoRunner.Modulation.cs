using AI.DataStructs.Algebraic;
using AI.DataStructs.WithComplexElements;
using AI.SignalLab.Filters;
using AI.SignalLab.Modulation.Demodulation;
using AI.SignalLab.Modulation.Modulation;
using AI.SignalLab.Modulation.Modulation.DigitalModulations;
using AiFrameworkDemo.Core;
using System.Collections;
using System.Numerics;
using System.Text;
using static AiFrameworkDemo.Core.DemoRunnerBase;
using Vector = AI.DataStructs.Algebraic.Vector;

namespace AiFrameworkDemo.Modules.SignalLabs;

public static partial class SignalLabsDemoRunner
{
    /// <summary>
    /// Демо цифровой квадратурной модуляции:
    ///   Текст -> Биты -> Созвездие -> SRRC Tx -> Модуляция несущей -> SRRC Rx -> IQ -> Декодирование.
    /// Отображает: модулированный сигнал, диаграмму созвездия и IQ-компоненты.
    /// </summary>
    internal static DemoResult DoModulation(
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        DemoSettings settings)
    {
        int    modType    = I(p, "modType", 0);
        double f0         = N(p, "carrierFreq", 3000);
        double sr         = N(p, "sampleRate", 144100);
        double bitDurUs   = N(p, "bitDuration", 900);         // мкс
        double bitDur     = bitDurUs * 1e-6;                  // секунды
        double rollOffRaw = N(p, "rollOff", 3);
        double rollOff    = rollOffRaw * 0.1;

        string text = T(tp, "_text", "AI.SignalLabs");
        if (string.IsNullOrEmpty(text)) text = "AI.SignalLabs";
        // Ограничиваем длину текста, чтобы не генерировать гигантские сигналы
        if (text.Length > 24) text = text[..24];

        // Выбираем модуляцию
        BaseIQModulation modulation = modType switch
        {
            1 => new QPSK(),
            2 => new QAM8(),
            3 => new QAM16(),
            _ => new BPSK(),
        };

        // Текст -> байты -> биты
        byte[] bytes = ModulationBitsTools.Obj2Bytes(text);
        BitArray bitArray = new BitArray(bytes);
        bool[] bits = new bool[bitArray.Length];
        bitArray.CopyTo(bits, 0);

        // Маппинг на созвездие
        Complex[] symbols = modulation.MapBitsToSymbols(bits);

        int sps = (int)Math.Round(bitDur * sr); // samples per symbol
        sps = Math.Max(sps, 4);

        int span = 4;
        var txI = new RootRaisedCosineFilter(bitDur, sr, rollOff, span);
        var txQ = new RootRaisedCosineFilter(bitDur, sr, rollOff, span);
        var demod = new QudratureDemodulation(f0, sr, bitDur, rollOff, span);

        int txDelay    = txI.Length / 2;
        int totalDelay = txDelay + demod.FilterDelay;
        int sigLen     = symbols.Length * sps + totalDelay + sps;

        // Импульсные веса (Dirac comb)
        Vector impI = new Vector(sigLen);
        Vector impQ = new Vector(sigLen);
        for (int i = 0; i < symbols.Length; i++)
        {
            impI[i * sps] = symbols[i].Real;
            impQ[i * sps] = symbols[i].Imaginary;
        }

        // Формирующий SRRC на Tx
        Vector bbI = txI.FilterOutp(impI);
        Vector bbQ = txQ.FilterOutp(impQ);

        // Квадратурная модуляция: s(t) = I·cos(w0·t) + Q·sin(w0·t)
        double dt = 1.0 / sr;
        Vector modulated = new Vector(bbI.Count);
        for (int i = 0; i < bbI.Count; i++)
        {
            double arg = 2 * Math.PI * f0 * i * dt;
            modulated[i] = bbI[i] * Math.Cos(arg) + bbQ[i] * Math.Sin(arg);
        }

        // Демодуляция: полный IQ и точки созвездия
        var (iqFull, iqSymbols) = demod.GetIQBoth(modulated, txDelay);

        // Декодируем текст
        string decoded;
        try
        {
            bool[] rxBits = modulation.DemapSymbolsToBits(iqSymbols, bits.Length);
            BitArray rxBa = new BitArray(rxBits);
            byte[] rxBytes = new byte[(rxBa.Length + 7) / 8];
            rxBa.CopyTo(rxBytes, 0);
            decoded = Encoding.UTF8.GetString(rxBytes).TrimEnd('\0');
        }
        catch (Exception ex)
        {
            decoded = $"[Ошибка декодирования: {ex.Message}]";
        }

        bool ok = decoded.TrimEnd('\0') == text;

        // Ограничиваем количество отображаемых точек сигнала
        int displayPts = Math.Min(modulated.Count, 1500);
        int stepSig = Math.Max(1, modulated.Count / displayPts);
        int ptsCount = modulated.Count / stepSig;

        double[] xSig = new double[ptsCount];
        double[] ySig = new double[ptsCount];
        double[] yI   = new double[ptsCount];
        double[] yQ   = new double[ptsCount];

        for (int i = 0; i < ptsCount; i++)
        {
            xSig[i] = i * stepSig * dt * 1000; // мс
            ySig[i] = modulated[i * stepSig];
            yI[i]   = iqFull[i * stepSig].Real;
            yQ[i]   = iqFull[i * stepSig].Imaginary;
        }

        // Созвездие (все принятые символы)
        double[] cxArr = new double[iqSymbols.Count];
        double[] cyArr = new double[iqSymbols.Count];
        for (int i = 0; i < iqSymbols.Count; i++)
        {
            cxArr[i] = iqSymbols[i].Real;
            cyArr[i] = iqSymbols[i].Imaginary;
        }

        // --- Главный график: модулированный сигнал + IQ ---
        var cvMain = MakeView(settings);
        cvMain.ChartName = $"{ModName(modType)}: модулированный сигнал и IQ-компоненты";
        cvMain.LabelX = "Время, мс";
        cvMain.LabelY = "Амплитуда";
        cvMain.AddPlot(new Vector(xSig), new Vector(ySig), "Модул. сигнал", width: 1);
        cvMain.AddPlot(new Vector(xSig), new Vector(yI), "I (синфазная)");
        cvMain.AddPlot(new Vector(xSig), new Vector(yQ), "Q (квадрат.)");

        // Текстовый результат
        var sb = new StringBuilder();
        sb.AppendLine($"Модуляция:      {ModName(modType)} ({modulation.BitsPerSymbol} бит/символ)");
        sb.AppendLine($"Несущая:        {f0:F0} Гц   |   ЧД: {sr:F0} Гц");
        sb.AppendLine($"Длит. символа:  {bitDurUs:F0} мкс   |   Roll-off β: {rollOff:F2}");
        sb.AppendLine($"Символов:       {symbols.Length}   |   Бит: {bits.Length}");
        sb.AppendLine($"Принятых симв.: {iqSymbols.Count}");
        sb.AppendLine();
        sb.AppendLine($"Отправлено:   «{text}»");
        sb.AppendLine($"Принято:      «{decoded}»");
        sb.AppendLine($"Статус:       {(ok ? "OK — текст восстановлен верно" : "ОШИБКА декодирования")}");
        sb.AppendLine();
        sb.AppendLine("Диаграмма созвездия (Rx символы I / Q):");
        int showSyms = Math.Min(iqSymbols.Count, 12);
        for (int i = 0; i < showSyms; i++)
            sb.AppendLine($"  [{i}] I={iqSymbols[i].Real:F3}  Q={iqSymbols[i].Imaginary:F3}");
        if (iqSymbols.Count > showSyms) sb.AppendLine($"  ... (+{iqSymbols.Count - showSyms} симв.)");

        return Png(cvMain, settings, textOutput: sb.ToString());
    }

    private static string ModName(int t) => t switch
    {
        1 => "QPSK",
        2 => "8-QAM",
        3 => "16-QAM",
        _ => "BPSK",
    };
}
