using AI.Script.Binding;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class ExtractionTests : IDisposable
{
    private const string Source = """
        using AI.Script.Binding;
        using System;
        using System.Linq;

        namespace Lib
        {
            /// <summary>Спектральный анализ.</summary>
            public static class Fft
            {
                /// <summary>Амплитудный спектр.</summary>
                /// <param name="signal">Отсчёты.</param>
                public static double[] Spectrum(double[] signal) => signal.Select(Math.Abs).ToArray();

                public static double Peak(double[] signal)
                {
                    Func<double[], double[]> f = s => Spectrum(s);
                    return f(signal).Max() + Spectrum(signal)[0];
                }

                private static int Helper() => 1;

                public static double Chain(double[] x)
                {
                    var s = Spectrum(x);
                    return Peak(Spectrum(s));
                }
            }

            internal sealed class Hidden
            {
                public void Visible() { }
            }

            /// <summary>Разделяемый тип.</summary>
            public partial class Split { public void First() { } }
            public partial class Split { public void Second() { } }

            public sealed class Box
            {
                public Box(int size) { }
                public Box() : this(1) { }
                public static Box operator +(Box a, Box b) => new Box();
            }

            public record Point(double X, double Y);

            public delegate int Op(int a);

            [ScriptModule("dsp", "Сигналы")]
            public sealed class DspModule
            {
                [ScriptFn("fft", "Спектр", Example = "dsp.fft(x)")]
                public static double[] Fft(double[] x) => Lib.Fft.Spectrum(x);

                [ScriptFn(description: "Длина")]
                public static int SignalLength(double[] x) => x.Length;
            }
        }
        """;

    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-extract-").FullName;

    [Fact]
    public void BuildsDocIdsSignaturesAndDocs()
    {
        IReadOnlyList<CodeUnit> units = Extract().Units;
        CodeUnit spectrum = units.Single(u => u.Id == "M:Lib.Fft.Spectrum(System.Double[])");

        Assert.Equal("Lib", spectrum.Project);
        Assert.Equal("public static double[] Fft.Spectrum(double[] signal)", spectrum.Signature);
        Assert.Equal("Амплитудный спектр.", UnitReport.Summary(spectrum.Doc));
        Assert.Contains("signal.Select", spectrum.Body);
        Assert.Equal(12, spectrum.Line);
    }

    [Fact]
    public void AccessAccountsForContainingType()
    {
        IReadOnlyList<CodeUnit> units = Extract().Units;

        Assert.Equal("private", units.Single(u => u.Id == "M:Lib.Fft.Helper").Access);
        Assert.Equal("internal", units.Single(u => u.Id == "M:Lib.Hidden.Visible").Access);
        Assert.Equal("public", units.Single(u => u.Id == "T:Lib.Fft").Access);
    }

    [Fact]
    public void PartialTypeIsOneUnitCompilerMembersAreSkipped()
    {
        IReadOnlyList<CodeUnit> units = Extract().Units;

        Assert.Single(units, u => u.Id == "T:Lib.Split");
        Assert.Contains(units, u => u.Id == "M:Lib.Split.Second");
        Assert.Equal(["M:Lib.Point.#ctor(System.Double,System.Double)"], units.Where(u => u.Id.StartsWith("M:Lib.Point.", StringComparison.Ordinal)).Select(u => u.Id));
        Assert.Contains(units, u => u.Id == "M:Lib.DspModule.#ctor");
        Assert.Contains(units, u => u.Id == "M:Lib.Box.op_Addition(Lib.Box,Lib.Box)" && u.Kind == "operator");
        Assert.Contains(units, u => u.Id == "M:Lib.Box.#ctor(System.Int32)" && u.Kind == "ctor");
    }

    [Fact]
    public void CallsIncludeLambdasConstructorsAndExternalMethods()
    {
        IReadOnlyList<CodeCall> calls = Extract().Calls;

        CodeCall inPeak = calls.Single(c => c.CallerId == "M:Lib.Fft.Peak(System.Double[])" && c.CalleeId == "M:Lib.Fft.Spectrum(System.Double[])");
        Assert.Equal(2, inPeak.Count);

        Assert.Contains(calls, c => c.CallerId == "M:Lib.Box.#ctor" && c.CalleeId == "M:Lib.Box.#ctor(System.Int32)");
        Assert.Contains(calls, c => c.CallerId == "M:Lib.Fft.Spectrum(System.Double[])"
            && c.CalleeId.StartsWith("M:System.Linq.Enumerable.Select", StringComparison.Ordinal));
    }

    [Fact]
    public void FlowsFollowResultsIntoNextCalls()
    {
        IReadOnlyList<CodeFlow> flows = Extract().Flows;

        CodeFlow flow = Assert.Single(flows, f => f.FromId == "M:Lib.Fft.Spectrum(System.Double[])" && f.ToId == "M:Lib.Fft.Peak(System.Double[])");
        Assert.Equal(1, flow.Count);
        Assert.DoesNotContain(flows, f => f.FromId == f.ToId);
    }

    [Fact]
    public void TypesOfInputsOutputsAndSupertypesAreRecorded()
    {
        IReadOnlyList<CodeUnit> units = Extract().Units;

        CodeUnit spectrum = units.Single(u => u.Id == "M:Lib.Fft.Spectrum(System.Double[])");
        Assert.Equal("double[]", spectrum.Returns);
        Assert.Equal("double[]", spectrum.Inputs);
        Assert.True(spectrum.IsStatic);
        Assert.Equal("Lib.Box", units.Single(u => u.Id == "M:Lib.Box.#ctor(System.Int32)").Returns);
        Assert.Contains("System.IEquatable<Lib.Point>", units.Single(u => u.Id == "T:Lib.Point").Supertypes.Split('|'));
    }

    [Fact]
    public void ScriptNamesComeFromAttributes()
    {
        IReadOnlyList<CodeUnit> units = Extract().Units;

        Assert.Equal("dsp.fft", units.Single(u => u.Id == "M:Lib.DspModule.Fft(System.Double[])").Script);
        Assert.Equal("dsp.signal_length", units.Single(u => u.Id == "M:Lib.DspModule.SignalLength(System.Double[])").Script);
        Assert.Null(units.Single(u => u.Id == "M:Lib.Fft.Spectrum(System.Double[])").Script);
    }

    [Fact]
    public void ScriptFunctionWithoutXmlDocTakesDescriptionFromAttribute()
    {
        CodeUnit fft = Extract().Units.Single(u => u.Id == "M:Lib.DspModule.Fft(System.Double[])");

        Assert.Equal("Спектр", UnitReport.Summary(fft.Doc));
        Assert.Contains("<example>dsp.fft(x)</example>", fft.Doc);
    }

    [Fact]
    public void PackageModeMatchesPublicSourceUnits()
    {
        CSharpCompilation compilation = Compile();
        string dll = Path.Combine(_folder, "Lib.dll");

        using (FileStream pe = File.Create(dll))
        using (FileStream xml = File.Create(Path.ChangeExtension(dll, ".xml")))
        {
            EmitResult result = compilation.Emit(pe, xmlDocumentationStream: xml);
            Assert.True(result.Success, string.Join("\n", result.Diagnostics));
        }

        File.Copy(typeof(ScriptFnAttribute).Assembly.Location, Path.Combine(_folder, "AI.Script.dll"));

        IReadOnlyList<CodeUnit> package = PackageReader.Read(dll);
        IEnumerable<CodeUnit> source = Extract().Units.Where(u => u.Access == "public");

        Assert.Equal(source.Select(u => u.Id).Order(), package.Select(u => u.Id).Order());
        Assert.Equal(
            source.Single(u => u.Id == "M:Lib.Fft.Spectrum(System.Double[])").Signature,
            package.Single(u => u.Id == "M:Lib.Fft.Spectrum(System.Double[])").Signature);
        Assert.Equal("Амплитудный спектр.", UnitReport.Summary(package.Single(u => u.Id == "M:Lib.Fft.Spectrum(System.Double[])").Doc));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private static (IReadOnlyList<CodeUnit> Units, IReadOnlyList<CodeCall> Calls, IReadOnlyList<CodeFlow> Flows) Extract()
    {
        CSharpCompilation compilation = Compile();
        SyntaxTree tree = compilation.SyntaxTrees[0];
        return SourceIndexer.Extract(compilation.GetSemanticModel(tree), "src/Lib.cs");
    }

    private static CSharpCompilation Compile()
    {
        var parse = new CSharpParseOptions(LanguageVersion.Latest, DocumentationMode.Parse);
        string platform = (string)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES")!;

        IEnumerable<MetadataReference> references = platform.Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Append(MetadataReference.CreateFromFile(typeof(ScriptFnAttribute).Assembly.Location));

        return CSharpCompilation.Create("Lib", [CSharpSyntaxTree.ParseText(Source, parse)], references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
