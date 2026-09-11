using AI.DataPrepaire.DataLoader.Formats;
using Xunit;

namespace AIFramework.UnitTests;

public class YAMLLoaderTests
{
    [Fact]
    public void Deserialize_ReadsNestedDictionaryWithCyrillicKeys()
    {
        var result = YAMLLoader.Deserialize<Dictionary<string, Dictionary<string, string>>>("Машина:\n  масса_кг: 1500\n  передачи: 5\n");

        Assert.Equal("1500", result["Машина"]["масса_кг"]);
        Assert.Equal("5", result["Машина"]["передачи"]);
    }

    [Fact]
    public void ReadDirectory_ReadsOnlyMatchingFiles()
    {
        var directory = Directory.CreateTempSubdirectory().FullName;
        try
        {
            File.WriteAllText(Path.Combine(directory, "a.yaml"), "x: 1");
            File.WriteAllText(Path.Combine(directory, "b.yaml"), "x: 2");
            File.WriteAllText(Path.Combine(directory, "c.txt"), "x: 3");

            var values = YAMLLoader.ReadDirectory<Dictionary<string, int>>(directory).Select(d => d["x"]).Order();

            Assert.Equal([1, 2], values);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }
}
