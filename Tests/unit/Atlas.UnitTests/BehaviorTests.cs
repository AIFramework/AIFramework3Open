using AI.DataStructs.Algebraic;
using System.Reflection;
using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class BehaviorTests
{
    [Fact]
    public void SameSeedGivesSameInputs()
    {
        ValueKind[] kinds = [ValueKind.Series, ValueKind.Integer, ValueKind.Table, ValueKind.Text];

        string[] first = [.. BehaviorInputs.Cases(kinds, seed: 5, count: 30).Select(Dump)];
        string[] again = [.. BehaviorInputs.Cases(kinds, seed: 5, count: 30).Select(Dump)];
        string[] other = [.. BehaviorInputs.Cases(kinds, seed: 6, count: 30).Select(Dump)];

        Assert.Equal(first, again);
        Assert.NotEqual(first.Skip(7), other.Skip(7));
    }

    [Fact]
    public void EdgeCasesComeFirstAndShareSize()
    {
        IReadOnlyList<InputCase> cases = BehaviorInputs.Cases([ValueKind.Series, ValueKind.Series, ValueKind.Table], seed: 1, count: 12);

        Assert.Equal([0, 1, 2], cases.Take(3).Select(c => c.Size));
        Assert.All(cases.Take(7), c => Assert.True(c.Edge));
        Assert.Contains(double.NaN, (double[])cases[4].Values[0]);
        Assert.All(cases, c =>
        {
            Assert.Equal(c.Size, ((double[])c.Values[0]).Length);
            Assert.Equal(c.Size, ((double[])c.Values[1]).Length);
            Assert.Equal(c.Size, ((double[,])c.Values[2]).GetLength(1));
        });
    }

    [Fact]
    public void RelationsBetweenOutputs()
    {
        OutputPair[] variance = [.. new[] { 3, 5, 8 }.Select(n => new OutputPair([Unbiased(Sample(n))], [Biased(Sample(n))], n))];
        Assert.Equal("множитель (n−1)/n", OutputRelation.Fit(variance)!.Name);

        OutputPair[] vectors = [new([1, 2, 3], [2, 4, 6], 3), new([1, -1], [2, -2], 2)];
        Assert.Equal("множитель 2", OutputRelation.Fit(vectors)!.Name);
        Assert.Equal("знак: B = −A", OutputRelation.Fit([new([1, 2], [-1, -2], 2), new([3], [-3], 1)])!.Name);
        Assert.StartsWith("сдвиг 0.5", OutputRelation.Fit([new([1, 2], [1.5, 2.5], 2), new([0], [0.5], 1)])!.Name.Replace(',', '.'));
        Assert.StartsWith("нормировка", OutputRelation.Fit([new([1, 3], [0.25, 0.75], 2), new([2, 2, 4], [0.25, 0.25, 0.5], 3)])!.Name);
        Assert.Null(OutputRelation.Fit([new([120], [120], 5), new([8.1e18], [2.6e22], 23)]));
        Assert.True(OutputRelation.Fit([new([1, double.NaN], [1 + 1e-12, double.NaN], 2)])!.IsIdentity);
    }

    [Fact]
    public void RenamedCopyIsBehaviouralDuplicate() =>
        Assert.Equal(BehaviorProbe.Duplicate, Probe(nameof(Samples.Mean), nameof(Samples.MeanOfVector)).Verdict);

    [Fact]
    public void PlantedBugIsDivergence()
    {
        BehaviorResult result = Probe(nameof(Samples.Mean), nameof(Samples.MeanWithBug));

        Assert.Equal(BehaviorProbe.Different, result.Verdict);
        Assert.Contains(result.Rows, row => !row.Edge);
    }

    [Fact]
    public void PlantedBugInSameNamedCopyIsDivergenceButTemplateIsNot()
    {
        BehaviorResult result = Probe(nameof(Samples.Mean), nameof(Samples.MeanWithBug));

        Assert.Equal(DupReport.Divergence, DupReport.Outcome(Finding(name: 1, doc: 0), result));
        Assert.Equal(DupReport.Divergence, DupReport.Outcome(Finding(name: 0.3, doc: 0.8), result));
        Assert.Equal("разные функции", DupReport.Outcome(Finding(name: 0.3, doc: 0.1), result));
    }

    [Fact]
    public void StateOnlyMethodsAreNotCompared()
    {
        const BindingFlags hidden = BindingFlags.Instance | BindingFlags.NonPublic;
        BehaviorResult result = BehaviorProbe.Compare(typeof(Filter).GetMethod("SetNumerator", hidden)!, typeof(Filter).GetMethod("SetDenominator", hidden)!);

        Assert.Equal(BehaviorProbe.NotChecked, result.Verdict);
        Assert.Contains("не изменил входы", result.Note);
    }

    [Fact]
    public void BiasedVarianceIsRelatedByLengthFactor()
    {
        BehaviorResult result = Probe(nameof(Samples.Unbiased), nameof(Samples.Biased));

        Assert.Equal(BehaviorProbe.Related, result.Verdict);
        Assert.Equal("множитель (n−1)/n", result.Relation);
    }

    [Fact]
    public void GuardedCopyDiffersOnlyOnEdges()
    {
        BehaviorResult result = Probe(nameof(Samples.Unbiased), nameof(Samples.GuardedUnbiased));

        Assert.Equal(BehaviorProbe.DuplicateExceptEdges, result.Verdict);
        Assert.All(result.Rows, row => Assert.True(row.Edge));
    }

    [Fact]
    public void ArgumentsArePermuted()
    {
        BehaviorResult result = Probe(nameof(Samples.Power), nameof(Samples.PowerSwapped));

        Assert.Equal(BehaviorProbe.Duplicate, result.Verdict);
        Assert.Equal("k ← k, x ← x", result.Mapping);
    }

    [Fact]
    public void IntegerSeriesAreGenerated()
    {
        Assert.Equal(ValueKind.Counts, BehaviorInputs.KindOf(typeof(byte[])));
        Assert.Equal(BehaviorProbe.Duplicate, Probe(nameof(Samples.CountEven), nameof(Samples.CountEvenBytes)).Verdict);
    }

    [Fact]
    public void InPlaceAndCopyAreCompared() =>
        Assert.Equal(BehaviorProbe.Duplicate, Probe(nameof(Samples.SortInPlace), nameof(Samples.SortedCopy)).Verdict);

    [Fact]
    public void PrivateInstanceHelperRunsWithoutConstructor()
    {
        MethodInfo helper = typeof(Stateful).GetMethod("Twice", BindingFlags.Instance | BindingFlags.NonPublic)!;

        Assert.Equal(BehaviorProbe.Duplicate, BehaviorProbe.Compare(Method(nameof(Samples.Twice)), helper).Verdict);
        Assert.Contains("состояния объекта", BehaviorProbe.Refusal(typeof(Stateful).GetMethod(nameof(Stateful.Scaled))!));
    }

    [Fact]
    public void SideEffectsAndUnsupportedTypesAreRefused()
    {
        Assert.Contains("внешним эффектом", BehaviorProbe.Refusal(Method(nameof(Samples.SaveResult))));
        Assert.Contains("не генерируется", BehaviorProbe.Refusal(Method(nameof(Samples.Split))));
        Assert.Equal("обобщённый метод", BehaviorProbe.Refusal(Method(nameof(Samples.Identity))));
        Assert.Null(BehaviorProbe.Refusal(Method(nameof(Samples.Mean))));
    }

    [Fact]
    public void DocIdRoundTrip()
    {
        MethodInfo method = Method(nameof(Samples.Trace));

        Assert.Equal("M:AiFramework.Tools.Atlas.UnitTests.Samples.Trace(System.Double[0:,0:])", BehaviorWorker.DocId(method));
        Assert.Same(method, BehaviorWorker.Find(typeof(Samples).Assembly, BehaviorWorker.DocId(method)));
        Assert.Equal("M:AiFramework.Tools.Atlas.UnitTests.Stateful.Scaled(System.Double)", BehaviorWorker.DocId(typeof(Stateful).GetMethod(nameof(Stateful.Scaled))!));
    }

    [Fact]
    public async Task WorkerComparesPairs()
    {
        using var host = new BehaviorHost(AppContext.BaseDirectory);

        BehaviorResult result = await host.CompareAsync(Ref(nameof(Samples.Mean)), Ref(nameof(Samples.MeanOfVector)));

        Assert.Equal(BehaviorProbe.Duplicate, result.Verdict);
    }

    [Fact]
    public async Task HangingMethodIsKilledAndWorkerRestarts()
    {
        using var host = new BehaviorHost(AppContext.BaseDirectory, timeout: TimeSpan.FromSeconds(3));

        BehaviorResult hung = await host.CompareAsync(Ref(nameof(Samples.Hang)), Ref(nameof(Samples.Hang)));
        BehaviorResult next = await host.CompareAsync(Ref(nameof(Samples.Mean)), Ref(nameof(Samples.MeanOfVector)));

        Assert.StartsWith("снят по времени", hung.Note);
        Assert.Equal(BehaviorProbe.Duplicate, next.Verdict);
    }

    [Fact]
    public async Task GreedyMethodIsKilledByMemory()
    {
        using var host = new BehaviorHost(AppContext.BaseDirectory, timeout: TimeSpan.FromSeconds(60), memoryLimit: 400L << 20);

        BehaviorResult result = await host.CompareAsync(Ref(nameof(Samples.Hog)), Ref(nameof(Samples.Hog)));

        Assert.StartsWith("снят по памяти", result.Note);
    }

    private static BehaviorResult Probe(string a, string b) => BehaviorProbe.Compare(Method(a), Method(b));

    private static DupFinding Finding(double name, double doc)
    {
        var unit = new CodeUnit("Lib", "M:Lib.A.F", "method", "public", "a.cs", 1, "F", "", "{ }", null, "");
        return new DupFinding(unit, unit with { Id = "M:Lib.B.F" }, new DupFeatures(0.2, doc, name, 0, 0, 1, true, false),
            "один предмет, разный код", "", unit, "имя");
    }

    private static MethodInfo Method(string name) => typeof(Samples).GetMethod(name)!;

    private static MethodRef Ref(string name) => new(typeof(Samples).Assembly.Location, BehaviorWorker.DocId(Method(name)));

    private static string Dump(InputCase input) => BehaviorInputs.Describe(input) + string.Join("|", input.Values.OfType<double[]>().Select(v => string.Join(",", v)));

    private static double[] Sample(int n) => [.. Enumerable.Range(1, n).Select(i => Math.Sin(i) * 3)];

    private static double Unbiased(double[] x) => Samples.Unbiased(x);

    private static double Biased(double[] x) => Samples.Biased(x);
}

/// <summary>Методы-образцы для исполнения: копии, подложенная ошибка, перестановка аргументов.</summary>
public static class Samples
{
    public static double Mean(double[] x)
    {
        double sum = 0;
        foreach (double value in x) sum += value;
        return sum / x.Length;
    }

    public static double MeanOfVector(Vector values)
    {
        double total = 0;
        for (int i = 0; i < values.Count; i++) total += values[i];
        return total / values.Count;
    }

    public static double MeanWithBug(double[] x)
    {
        double sum = 0;
        for (int i = 0; i < x.Length - 1; i++) sum += x[i];
        return sum / x.Length;
    }

    public static double Unbiased(double[] x)
    {
        double mean = x.Average();
        return x.Sum(v => (v - mean) * (v - mean)) / (x.Length - 1);
    }

    public static double Biased(double[] x)
    {
        double mean = x.Average();
        return x.Sum(v => (v - mean) * (v - mean)) / x.Length;
    }

    public static double GuardedUnbiased(double[] x) => x.Length < 2 ? 0 : Unbiased(x);

    public static double Power(double x, int k) => Math.Pow(x, k);

    public static double PowerSwapped(int k, double x)
    {
        double result = 1;
        for (int i = 0; i < k; i++) result *= x;
        return result;
    }

    public static void SortInPlace(double[] x) => Array.Sort(x);

    public static double[] SortedCopy(double[] x) => [.. x.Order()];

    public static double Twice(double x) => x + x;

    public static int CountEven(int[] values) => values.Count(v => v % 2 == 0);

    public static int CountEvenBytes(byte[] data)
    {
        int count = 0;
        foreach (byte b in data) if ((b & 1) == 0) count++;
        return count;
    }

    public static double Trace(double[,] m)
    {
        double sum = 0;
        for (int i = 0; i < Math.Min(m.GetLength(0), m.GetLength(1)); i++) sum += m[i, i];
        return sum;
    }

    public static void SaveResult(double[] x) => throw new InvalidOperationException("не должен исполняться");

    public static string[] Split(Uri address) => [address.Host];

    public static T Identity<T>(T value) => value;

    public static double Hang(double x)
    {
        while (true) Thread.SpinWait(1000);
    }

    public static double Hog(double x)
    {
        var chunks = new List<byte[]>();
        while (true)
        {
            var chunk = new byte[16 << 20];
            Array.Fill(chunk, (byte)1);
            chunks.Add(chunk);
        }
    }
}

/// <summary>Методы без результата, меняющие только поля: по входам их не сравнить.</summary>
public sealed class Filter
{
    private double[] _numerator = [], _denominator = [];

    private void SetNumerator(double[] b) => _numerator = (double[])b.Clone();

    private void SetDenominator(double[] a) => _denominator = (double[])a.Clone();

    public int Order => Math.Max(_numerator.Length, _denominator.Length);
}

/// <summary>Класс без конструктора по умолчанию: закрытый помощник вызывается на объекте без конструктора.</summary>
public sealed class Stateful(int size)
{
    public int Size { get; } = size;

    public double Scaled(double x) => Size * x;

    private double Twice(double x) => 2 * x;
}
