using System.Text.RegularExpressions;
using Xunit;

namespace AIFramework.UnitTests.Dsp;

/// <summary>
/// Храповик одного места: дизайн фильтров и передискретизация живут только в своих модулях AI.DSP
/// (Multirate — многоскоростная обработка и КИХ по ТЗ, IIR — БИХ по ТЗ). Старые реализации заморожены списком:
/// они помечены [Obsolete] и не должны множиться.
/// </summary>
public sealed partial class DspOneHomeRatchetTests
{
    private static readonly string[] Homes = ["src/AI.DSP/Multirate/", "src/AI.DSP/IIR/"];

    // Унаследованные файлы; список только сокращается
    private static readonly HashSet<string> Legacy =
    [
        "src/AI.ClassicMath/MathUtils/SpecialFunction/EllipticIntegral.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/BiQuad/BiQuadFilter.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/Butterworth/PrototypeButterworth.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/ChebyshevI/PrototypeChebyshevI.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/ChebyshevII/PrototypeChebyshevII.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/Elliptic/PrototypeElliptic.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/Fda/DesignFilter.Iir.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/Fda/DesignFilter.Sos.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/Fda/DesignFilter.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Filters/Polyphase/PolyphaseSystem.cs",
        "src/AI.DSP/BackEnds/DSP/NWaves/Operations/Resampler.cs",
        "src/AI.DSP/Oversampling/UpSampling.cs",
    ];

    [Fact]
    public void FilterDesignAndResampling_LiveOnlyInTheirModules()
    {
        string root = DspGolden.RepositoryRoot;
        string[] offenders = [.. new[] { "src", "Tools" }
            .Select(d => Path.Combine(root, d))
            .Where(Directory.Exists)
            .SelectMany(d => Directory.EnumerateFiles(d, "*.cs", SearchOption.AllDirectories))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))
            .Where(p => !p.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light"))
            .Where(p => !Homes.Any(h => p.StartsWith(h, StringComparison.Ordinal)) && !Legacy.Contains(p))
            .Where(p => DesignType().IsMatch(File.ReadAllText(Path.Combine(root, p))))];

        Assert.True(offenders.Length == 0,
            "Дизайн фильтров или передискретизация вне AI.DSP.Multirate / AI.DSP.IIR — используйте их: " + string.Join(", ", offenders));
    }

    [Fact]
    public void BesselI0_HasOneImplementation()
    {
        string root = DspGolden.RepositoryRoot;
        string[] bodies = [.. Directory.EnumerateFiles(Path.Combine(root, "src"), "*.cs", SearchOption.AllDirectories)
            .Where(p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            .Where(p => I0Body().IsMatch(File.ReadAllText(p)))
            .Select(p => Path.GetRelativePath(root, p).Replace('\\', '/'))];

        Assert.Equal(["src/AI.DSP/Multirate/KaiserFir.cs"], bodies);
    }

    [GeneratedRegex(@"(class|struct|record|interface)\s+\w*(Resampler|Resampling|Polyphase|Farrow|Cic[A-Z]|Decimator|Upsampl|UpSampling|Butterworth|Chebyshev|Elliptic|Kaiser|Biquad|BiQuad|IirDesign|FilterDesign|DesignFilter)\w*")]
    private static partial Regex DesignType();

    // Тело функции I₀ (не переадресация через =>)
    [GeneratedRegex(@"double\s+(BesselI0|I0)\s*\(\s*double\s+\w+\s*\)\s*\{")]
    private static partial Regex I0Body();
}
