using AI.Script.Hosting;
using AI.Script.Runtime;

namespace AI.Script.UnitTests;

/// <summary>
/// Песочница как отдельный слой: проверяется без интерпретатора.
/// </summary>
/// <remarks>
/// Отдельно от <see cref="IoTests"/> намеренно: правило «наружу нельзя» должно держаться на
/// самой песочнице, а не на том, что все функции модуля <c>io</c> её аккуратно вызывают.
/// </remarks>
public sealed class SandboxTests : IDisposable
{
    private readonly string _root;
    private readonly WorkspaceSandbox _sandbox;

    public SandboxTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "aiscript-sandbox", Guid.NewGuid().ToString("N"));
        _sandbox = new WorkspaceSandbox(_root);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
        catch (IOException)
        {
            // Уборка временной папки не должна ронять тест.
        }
    }

    [Fact]
    public void Sandbox_CreatesRoot() => Assert.True(Directory.Exists(_root));

    [Theory]
    [InlineData("a.txt")]
    [InlineData("sub/a.txt")]
    [InlineData("./sub/../a.txt")]
    public void Sandbox_ResolvesInsidePaths(string path)
    {
        string full = _sandbox.Resolve(path, forWriting: false);

        Assert.StartsWith(_root, full, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("../a.txt")]
    [InlineData("sub/../../a.txt")]
    [InlineData("../../../../etc/passwd")]
    public void Sandbox_RejectsEscapes(string path)
    {
        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve(path, forWriting: false));
    }

    [Fact]
    public void Sandbox_RejectsAbsolutePaths()
    {
        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve(Path.Combine(_root, "a.txt"), forWriting: false));
    }

    [Fact]
    public void Sandbox_RejectsEmptyPath()
    {
        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve("  ", forWriting: false));
    }

    [Fact]
    public void Sandbox_CreatesParentDirectoryForWriting()
    {
        string full = _sandbox.Resolve("deep/nested/file.txt", forWriting: true);

        Assert.True(Directory.Exists(Path.GetDirectoryName(full)));
    }

    [Fact]
    public void Sandbox_ReadOnly_RejectsWriting()
    {
        var readOnly = new WorkspaceSandbox(_root, readOnly: true);

        _ = readOnly.Resolve("a.txt", forWriting: false);
        _ = Assert.Throws<ScriptError>(() => readOnly.Resolve("a.txt", forWriting: true));
    }

    [Fact]
    public void Sandbox_ListsRelativeNames()
    {
        File.WriteAllText(Path.Combine(_root, "a.csv"), "x");
        _ = Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "b.csv"), "x");

        Assert.Equal(["a.csv"], _sandbox.List(".", "*.csv"));
        Assert.Equal(["sub/b.csv"], _sandbox.List("sub", "*.csv"));
    }

    [Fact]
    public void Sandbox_ListOfMissingDirectory_IsEmpty() => Assert.Empty(_sandbox.List("nope", "*"));

    [Fact]
    public void Sandbox_Denied_RefusesEverything()
    {
        _ = Assert.Throws<ScriptError>(() => DeniedSandbox.Instance.Resolve("a.txt", forWriting: false));
        _ = Assert.Throws<ScriptError>(() => DeniedSandbox.Instance.List(".", "*"));
        Assert.False(DeniedSandbox.Instance.Enabled);
    }

    [Fact]
    public void RunOptions_DenyFilesByDefault() => Assert.False(new RunOptions().Sandbox.Enabled);
}
