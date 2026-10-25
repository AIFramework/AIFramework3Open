using AI.Solvers.Chem.Database;

namespace AI.Solvers.Chem.UnitTests;

/// <summary>База ретросинтетических маршрутов.</summary>
public class SynthesisDatabaseTests
{
    [Fact]
    public void WithoutFileNextToProgram_EmbeddedDatabaseIsLoaded()
    {
        // Так база загружается у потребителя пакета NuGet: файла Data\synthesis_database.json рядом нет.
        var manager = new SynthesisDatabaseManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".json"));

        Assert.NotEmpty(manager.GetAvailableCompounds());
    }
}
