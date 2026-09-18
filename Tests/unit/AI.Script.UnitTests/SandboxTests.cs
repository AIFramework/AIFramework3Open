using AI.Script.Hosting;
using AI.Script.Runtime;

namespace AI.Script.UnitTests;

/// <summary>
/// Хранилища как отдельный слой: проверяются без интерпретатора.
/// </summary>
/// <remarks>
/// Отдельно от <see cref="IoTests"/> намеренно: правило «наружу нельзя» должно держаться на
/// самом хранилище и на общей нормализации путей, а не на том, что все функции модулей их
/// аккуратно вызывают.
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

    // --- папка на диске ---

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
    public async Task Sandbox_ListsRelativePaths()
    {
        File.WriteAllText(Path.Combine(_root, "a.csv"), "x");
        _ = Directory.CreateDirectory(Path.Combine(_root, "sub"));
        File.WriteAllText(Path.Combine(_root, "sub", "b.csv"), "x");

        Assert.Equal(["a.csv"], (await _sandbox.ListAsync(".", "*.csv")).Select(file => file.Path));
        Assert.Equal(["sub/b.csv"], (await _sandbox.ListAsync("sub", "*.csv")).Select(file => file.Path));
    }

    [Fact]
    public async Task Sandbox_ListOfMissingDirectory_IsEmpty() => Assert.Empty(await _sandbox.ListAsync("nope", "*"));

    [Fact]
    public async Task Sandbox_WritesAndReadsBytes()
    {
        await _sandbox.WriteAsync("d/x.bin", [1, 2, 3]);

        Assert.Equal([1, 2, 3], await _sandbox.ReadAsync("d/x.bin"));

        ScriptFileInfo? info = await _sandbox.InfoAsync("d/x.bin");

        Assert.NotNull(info);
        Assert.Equal(3, info.Size);
        Assert.Equal("x.bin", info.Name);
        Assert.Null(await _sandbox.InfoAsync("d/none.bin"));
    }

    [Fact]
    public async Task Sandbox_Denied_RefusesEverything()
    {
        _ = await Assert.ThrowsAsync<ScriptError>(() => DeniedSandbox.Instance.ReadAsync("a.txt"));
        _ = await Assert.ThrowsAsync<ScriptError>(() => DeniedSandbox.Instance.ListAsync(".", "*"));
        _ = await Assert.ThrowsAsync<ScriptError>(() => DeniedSandbox.Instance.WriteAsync("a.txt", [1]));
        Assert.False(DeniedSandbox.Instance.Enabled);
    }

    [Fact]
    public void RunOptions_DenyFilesByDefault() => Assert.False(new RunOptions().Sandbox.Enabled);

    // --- общая нормализация путей ---

    [Theory]
    [InlineData("a/./b/../c.txt", "a/c.txt")]
    [InlineData("sub\\a.txt", "sub/a.txt")]
    [InlineData(".", ".")]
    [InlineData("a/..", ".")]
    public void Paths_Normalize(string path, string expected) => Assert.Equal(expected, ScriptPaths.Normalize(path));

    [Theory]
    [InlineData("../a")]
    [InlineData("a/../../b")]
    [InlineData("C:/x.txt")]
    [InlineData("/etc/passwd")]
    [InlineData("\\\\server\\share")]
    [InlineData("")]
    public void Paths_RejectOutside(string path)
    {
        _ = Assert.Throws<ScriptError>(() => ScriptPaths.Normalize(path));
    }

    [Theory]
    [InlineData("a.CSV", "table")]
    [InlineData("b.docx", "document")]
    [InlineData("c.mp4", "video")]
    [InlineData("d", "other")]
    [InlineData("e.weird", "other")]
    public void Kinds_FollowExtension(string path, string kind) => Assert.Equal(kind, ScriptFileKinds.KindOf(path));

    // --- память и сужение ---

    [Fact]
    public async Task Memory_ListsOnlyDirectChildren()
    {
        var memory = new MemorySandbox().Put("a.csv", "x").Put("b.txt", "y").Put("sub/c.csv", "z");

        Assert.Equal(["a.csv"], (await memory.ListAsync(".", "*.csv")).Select(file => file.Path));
        Assert.Equal(["sub/c.csv"], (await memory.ListAsync("sub", "*")).Select(file => file.Path));
    }

    [Fact]
    public async Task Memory_ReadOnly_DeniesScriptButNotHost()
    {
        var memory = new MemorySandbox(readOnly: true).Put("a.txt", "есть");

        _ = await Assert.ThrowsAsync<ScriptError>(() => memory.WriteAsync("b.txt", [1]));
        Assert.NotNull(memory.Get("a.txt"));
    }

    [Fact]
    public async Task Scoped_StaysInsideItsFolder()
    {
        var memory = new MemorySandbox().Put("secret.txt", "снаружи");
        var scoped = new ScopedSandbox(memory, "inner");

        await scoped.WriteAsync("a.txt", [1]);

        Assert.NotNull(memory.Get("inner/a.txt"));
        Assert.Equal(["a.txt"], (await scoped.ListAsync(".", "*")).Select(file => file.Path));
        _ = await Assert.ThrowsAsync<ScriptError>(() => scoped.ReadAsync("../secret.txt"));
        _ = Assert.Throws<ScriptError>(() => new ScopedSandbox(memory, "../x"));
    }
}
