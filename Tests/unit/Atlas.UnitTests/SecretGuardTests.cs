using Xunit;

namespace AiFramework.Tools.Atlas.UnitTests;

public sealed class SecretGuardTests : IDisposable
{
    // Не настоящий ключ: формат OpenRouter, но из одних повторов.
    private static readonly string FakeKey = "sk-or-v1-" + string.Concat(Enumerable.Repeat("0123456789abcdef", 4));

    private readonly string _folder = Directory.CreateTempSubdirectory("atlas-guard-").FullName;

    [Fact]
    public void FindsKeyByFormat()
    {
        string file = Write("config.cs", "var a = 1;\nvar key = \"" + FakeKey + "\";\n");

        SecretGuard.Finding finding = Assert.Single(SecretGuard.Scan([file], knownKey: null));

        Assert.Equal(2, finding.Line);
        Assert.Equal("ключ OpenRouter", finding.Kind);
    }

    [Fact]
    public void FindsKnownValueOfAnyShape()
    {
        string file = Write("notes.md", "ключ: my-custom-secret-value\n");

        SecretGuard.Finding finding = Assert.Single(SecretGuard.Scan([file], knownKey: "my-custom-secret-value"));

        Assert.Contains(AtlasSettings.KeyVariable, finding.Kind);
    }

    [Fact]
    public void ReportDoesNotContainTheKey()
    {
        string file = Write("leak.txt", FakeKey);

        SecretGuard.Finding finding = Assert.Single(SecretGuard.Scan([file], knownKey: FakeKey));

        Assert.DoesNotContain(FakeKey, finding.ToString());
    }

    [Fact]
    public void IgnoresPrefixWithoutKeyBody()
    {
        string file = Write("docs.md", "Ключи OpenRouter начинаются с sk-or-v1- и 64 знаков.");

        Assert.Empty(SecretGuard.Scan([file], knownKey: null));
    }

    [Fact]
    public void SkipsBinaryAndMissingFiles()
    {
        string binary = Path.Combine(_folder, "blob.bin");
        File.WriteAllBytes(binary, [0, 1, 2, .. System.Text.Encoding.ASCII.GetBytes(FakeKey)]);

        Assert.Empty(SecretGuard.Scan([binary, Path.Combine(_folder, "missing.txt")], knownKey: null));
    }

    public void Dispose() => Directory.Delete(_folder, recursive: true);

    private string Write(string name, string content)
    {
        string path = Path.Combine(_folder, name);
        File.WriteAllText(path, content);
        return path;
    }
}
