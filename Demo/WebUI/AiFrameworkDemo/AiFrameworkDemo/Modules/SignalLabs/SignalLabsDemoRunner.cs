using AiFrameworkDemo.Core;
using static AiFrameworkDemo.Core.DemoRunnerBase;

namespace AiFrameworkDemo.Modules.SignalLabs;

/// <summary>
/// Главный диспетчер демонстраций AI.SignalLabs.
/// Делегирует выполнение конкретным partial-классам по ключу алгоритма.
/// </summary>
public static partial class SignalLabsDemoRunner
{
    public static DemoResult Run(
        string algoKey,
        IReadOnlyDictionary<string, double> p,
        IReadOnlyDictionary<string, string> tp,
        DemoSettings settings) => algoKey switch
    {
        "agc_demo"        => DoAgc(p, settings),
        "modulation_demo" => DoModulation(p, tp, settings),
        "srrc_demo"       => DoSrrc(p, settings),
        _ => new DemoResult { Error = $"Неизвестный алгоритм: {algoKey}" }
    };
}
