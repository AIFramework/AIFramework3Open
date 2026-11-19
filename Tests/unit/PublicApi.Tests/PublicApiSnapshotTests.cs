using Xunit;
using Xunit.Abstractions;

namespace AIFramework.PublicApi.Tests;

/// <summary>
/// Публичный API каждой сборки src совпадает со снимком в Snapshots/&lt;сборка&gt;.txt.
/// Любое изменение публичного API требует решения владельца и явного обновления снимка
/// (CONTRIBUTING.md, раздел «Изменение публичного API»).
/// </summary>
public sealed class PublicApiSnapshotTests
{
    private const string UpdateHint =
        "Если изменение согласовано владельцем, обновите снимок явно и закоммитьте его вместе с кодом:\n" +
        "  PowerShell: $env:" + ApiSnapshot.UpdateVariable + "='1'; dotnet test Tests/unit/PublicApi.Tests -c Release; " +
        "Remove-Item Env:" + ApiSnapshot.UpdateVariable + "\n" +
        "  bash:       " + ApiSnapshot.UpdateVariable + "=1 dotnet test Tests/unit/PublicApi.Tests -c Release\n" +
        "Публичный член не удаляют сразу: сначала [Obsolete] с указанием замены (CONTRIBUTING.md, «Изменение публичного API»).";

    private readonly ITestOutputHelper _output;

    /// <summary>
    /// Создаёт тест с выводом сообщений об обновлении снимков.
    /// </summary>
    public PublicApiSnapshotTests(ITestOutputHelper output)
    {
        _output = output;
    }

    /// <summary>
    /// Сборки, у которых проверяется снимок.
    /// </summary>
    public static TheoryData<string> CheckedAssemblies()
    {
        var data = new TheoryData<string>();

        foreach (string name in SrcAssemblies.Checked())
            data.Add(name);

        return data;
    }

    /// <summary>
    /// Фактический публичный API сборки совпадает со снимком; снимка нет — падение.
    /// </summary>
    [Theory]
    [MemberData(nameof(CheckedAssemblies))]
    public void PublicApi_OfSrcAssembly_MatchesSnapshot(string assemblyName)
    {
        string actual = ApiSnapshot.Generate(assemblyName);
        string? expected = ApiSnapshot.Read(assemblyName);

        if (expected == actual)
            return;

        if (ApiSnapshot.UpdateRequested)
        {
            Assert.False(ApiSnapshot.RunningInCi,
                $"{ApiSnapshot.UpdateVariable}=1 в CI: снимки обновляются только локально, по решению владельца.");

            ApiSnapshot.Write(assemblyName, actual);
            _output.WriteLine($"Снимок {(expected == null ? "создан" : "обновлён")}: {SrcAssemblies.SnapshotPath(assemblyName)}");
            return;
        }

        if (expected == null)
        {
            Assert.Fail(
                $"Нет снимка публичного API сборки {assemblyName}: {SrcAssemblies.SnapshotPath(assemblyName)}.\n" +
                $"Новая сборка src должна прийти со снимком.\n{UpdateHint}");
        }

        Assert.Fail(
            $"Публичный API сборки {assemblyName} отличается от снимка {SrcAssemblies.SnapshotPath(assemblyName)}.\n" +
            $"«-» — есть только в снимке, «+» — только в коде.\n\n{LineDiff.Format(expected, actual)}\n{UpdateHint}");
    }

    /// <summary>
    /// Храповик: у каждой сборки src есть снимок, у каждого снимка — сборка, исключения актуальны.
    /// </summary>
    [Fact]
    public void Snapshots_OfSrcAssemblies_AreCompleteAndNotStale()
    {
        IReadOnlyList<string> assemblies = SrcAssemblies.All();
        var snapshots = SrcAssemblies.SnapshotNames().ToHashSet(StringComparer.Ordinal);
        var problems = new List<string>();

        // При явном обновлении недостающие снимки создаёт PublicApi_OfSrcAssembly_MatchesSnapshot,
        // а порядок тестов в xUnit не задан: здесь их отсутствие не проверяется
        if (!ApiSnapshot.UpdateRequested)
        {
            foreach (string name in SrcAssemblies.Checked().Where(name => !snapshots.Contains(name)))
                problems.Add($"сборка {name} есть в src, а снимка Snapshots/{name}.txt нет");
        }

        foreach (string name in snapshots.Where(name => !assemblies.Contains(name)).Order(StringComparer.Ordinal))
            problems.Add($"снимок Snapshots/{name}.txt лежит, а сборки {name} в src нет — удалите файл вместе с проектом");

        foreach (string name in SrcAssemblies.Excluded.Keys.Where(name => !assemblies.Contains(name)))
            problems.Add($"исключение {name} в SrcAssemblies.Excluded устарело: такой сборки в src нет");

        Assert.True(assemblies.Count > 0, $"В {SrcAssemblies.RepositoryRoot}\\src не найдено ни одного проекта.");
        Assert.True(problems.Count == 0, string.Join("\n", problems) + "\n\n" + UpdateHint);
    }
}
