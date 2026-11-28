using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace AI.Script.UnitTests;

/// <summary>
/// Песочница сверяет реальное расположение цели, а не строку пути.
/// </summary>
/// <remarks>
/// Строка <c>link/secret.txt</c> выглядит как путь внутри рабочей папки, а на диске <c>link</c> —
/// точка соединения, ведущая наружу. Проверяется каждая операция хранилища и функции скрипта
/// поверх него: одного пропущенного пути достаточно, чтобы скрипт читал чужие файлы.
/// </remarks>
public sealed partial class SandboxRealPathTests : IDisposable
{
    private readonly string _base;
    private readonly string _root;
    private readonly string _outside;
    private readonly WorkspaceSandbox _sandbox;

    public SandboxRealPathTests()
    {
        _base = Path.Combine(Path.GetTempPath(), "aiscript-realpath", Guid.NewGuid().ToString("N"));
        _root = Path.Combine(_base, "work");
        _outside = Path.Combine(_base, "secret");

        _ = Directory.CreateDirectory(_outside);
        File.WriteAllText(Path.Combine(_outside, "secret.txt"), "тайна");
        File.WriteAllText(Path.Combine(_outside, "secret.db"), "не база");

        _sandbox = new WorkspaceSandbox(_root);
    }

    public void Dispose()
    {
        try
        {
            Links.DeleteTree(_base);
        }
        catch (IOException)
        {
            // Уборка временной папки не должна ронять тест.
        }
    }

    private RunOptions Options => new() { Sandbox = _sandbox };

    /// <summary>Ссылка-папка внутри рабочей папки, ведущая наружу.</summary>
    private void LinkOutside(string name = "link") => Links.Directory(Path.Combine(_root, name), _outside);

    // --- точка соединения наружу ---

    [Fact]
    public async Task Junction_Read_IsRefused()
    {
        LinkOutside();

        ScriptError error = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.ReadAsync("link/secret.txt"));

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
        Assert.Contains("ссылк", error.Message, StringComparison.Ordinal);
        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.OpenReadAsync("link/secret.txt"));
        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.InfoAsync("link/secret.txt"));
    }

    [Fact]
    public async Task Junction_OverwriteExisting_IsRefusedAndFileIntact()
    {
        LinkOutside();

        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.WriteAsync("link/secret.txt", [1, 2, 3]));

        Assert.Equal("тайна", File.ReadAllText(Path.Combine(_outside, "secret.txt")));
    }

    [Fact]
    public async Task Junction_CreateNew_IsRefusedAndNothingAppearsOutside()
    {
        LinkOutside();

        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.WriteAsync("link/new.txt", [1]));
        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.WriteAsync("link/deep/nested/new.txt", [1]));

        Assert.False(File.Exists(Path.Combine(_outside, "new.txt")));
        Assert.False(Directory.Exists(Path.Combine(_outside, "deep")));
    }

    [Fact]
    public async Task Junction_List_IsRefused()
    {
        LinkOutside();

        ScriptError error = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.ListAsync("link", "*"));

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
    }

    [Fact]
    public void Junction_NestedBelowRealFolder_IsRefused()
    {
        _ = Directory.CreateDirectory(Path.Combine(_root, "a", "b"));
        Links.Directory(Path.Combine(_root, "a", "b", "out"), _outside);

        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve("a/b/out/secret.txt", forWriting: false));
        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve("a/b/out/x/../secret.txt", forWriting: false));
    }

    [Fact]
    public void Junction_DifferentCase_IsRefused()
    {
        LinkOutside("Link");

        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve("LINK/SECRET.TXT", forWriting: false));
    }

    [Fact]
    public async Task Junction_Inside_IsAllowed()
    {
        string real = Path.Combine(_root, "real");

        _ = Directory.CreateDirectory(real);
        File.WriteAllText(Path.Combine(real, "a.txt"), "внутри");
        Links.Directory(Path.Combine(_root, "alias"), real);

        Assert.Equal("внутри", System.Text.Encoding.UTF8.GetString(await _sandbox.ReadAsync("alias/a.txt")));
        Assert.Equal(["alias/a.txt"], (await _sandbox.ListAsync("alias", "*")).Select(file => file.Path));

        await _sandbox.WriteAsync("alias/b.txt", [7]);

        Assert.Equal([7], File.ReadAllBytes(Path.Combine(real, "b.txt")));
    }

    // --- функции скрипта поверх хранилища ---

    [Theory]
    [InlineData("emit r = io.read_text(\"link/secret.txt\")")]
    [InlineData("io.write_text(\"x\", \"link/new.txt\")")]
    [InlineData("emit r = io.ls(\"link\")")]
    [InlineData("emit r = data.tables(\"link/secret.db\")")]
    public void Script_ThroughJunction_IsRefused(string source)
    {
        LinkOutside();

        RunResult result = Script.RunWith(Script.FullHost(), source, Options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.SandboxDenied, result.Error!.Code);
        Assert.False(File.Exists(Path.Combine(_outside, "new.txt")));
    }

    [Fact]
    public void Script_ExistsThroughJunction_SeesNothing()
    {
        LinkOutside();

        // io.exists не отказывает, а отвечает «нет»: о файлах снаружи скрипт не узнаёт ничего.
        RunResult result = Script.RunOk("emit r = io.exists(\"link/secret.txt\")", Options);

        Assert.Equal(false, result.Emitted["r"]);
    }

    // --- символическая ссылка ---

    [SymlinkFact]
    public async Task Symlink_FileOutside_IsRefusedAndHiddenFromList()
    {
        File.WriteAllText(Path.Combine(_root, "own.txt"), "своё");
        _ = File.CreateSymbolicLink(Path.Combine(_root, "leak.txt"), Path.Combine(_outside, "secret.txt"));

        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.ReadAsync("leak.txt"));
        _ = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.WriteAsync("leak.txt", [1]));

        Assert.Equal("тайна", File.ReadAllText(Path.Combine(_outside, "secret.txt")));
        Assert.Equal(["own.txt"], (await _sandbox.ListAsync(".", "*")).Select(file => file.Path));
    }

    [SymlinkFact]
    public async Task Symlink_FileInside_IsListedWithTargetSize()
    {
        File.WriteAllText(Path.Combine(_root, "own.txt"), "12345");
        _ = File.CreateSymbolicLink(Path.Combine(_root, "alias.txt"), Path.Combine(_root, "own.txt"));

        IReadOnlyList<ScriptFileInfo> files = await _sandbox.ListAsync(".", "alias*");

        Assert.Equal(5, Assert.Single(files).Size);
        Assert.Equal(5, (await _sandbox.ReadAsync("alias.txt")).Length);
    }

    [SymlinkFact]
    public async Task Symlink_Dangling_WriteIsRefused()
    {
        string target = Path.Combine(_outside, "created-through-link.txt");

        _ = File.CreateSymbolicLink(Path.Combine(_root, "ghost.txt"), target);

        ScriptError error = await Assert.ThrowsAsync<ScriptError>(() => _sandbox.WriteAsync("ghost.txt", [1]));

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
        Assert.False(File.Exists(target));
    }

    [SymlinkFact]
    public void Symlink_DirectoryOutside_IsRefused()
    {
        _ = Directory.CreateSymbolicLink(Path.Combine(_root, "dir"), _outside);

        _ = Assert.Throws<ScriptError>(() => _sandbox.Resolve("dir/secret.txt", forWriting: false));
    }

    // --- строка пути ---

    [Theory]
    [InlineData("../secret/secret.txt")]
    [InlineData("a/../../secret/secret.txt")]
    [InlineData("\\\\?\\C:\\Windows\\win.ini")]
    [InlineData("\\\\.\\C:\\Windows\\win.ini")]
    [InlineData("C:secret.txt")]
    public void Path_Outside_IsRefused(string path)
    {
        ScriptError error = Assert.Throws<ScriptError>(() => _sandbox.Resolve(path, forWriting: false));

        Assert.Equal(DiagnosticCodes.SandboxDenied, error.Code);
    }

    [Fact]
    public async Task Path_CaseOfExistingFile_FollowsFileSystem()
    {
        await _sandbox.WriteAsync("Data/Report.txt", [1]);

        if (OperatingSystem.IsWindows()) Assert.Equal([1], await _sandbox.ReadAsync("data/REPORT.TXT"));
        else Assert.Null(await _sandbox.InfoAsync("data/REPORT.TXT"));
    }

    [Fact]
    public async Task Root_GivenWithExtendedPrefix_Works()
    {
        if (!OperatingSystem.IsWindows()) return;

        var sandbox = new WorkspaceSandbox(@"\\?\" + _root);

        await sandbox.WriteAsync("sub/a.txt", [5]);

        Assert.Equal([5], await sandbox.ReadAsync("sub/a.txt"));

        LinkOutside();

        _ = await Assert.ThrowsAsync<ScriptError>(() => sandbox.ReadAsync("link/secret.txt"));
    }

    [Fact]
    public void DeviceName_IsRefusedWithItsOwnReason()
    {
        if (!OperatingSystem.IsWindows()) return;

        ScriptError error = Assert.Throws<ScriptError>(() => _sandbox.Resolve("NUL", forWriting: false));

        Assert.Contains("устройств", error.Message, StringComparison.Ordinal);
    }

    // --- оба способа раскрытия согласны ---

    [Fact]
    public void RealPath_HandleAndWalk_Agree()
    {
        LinkOutside();

        string viaLink = Path.Combine(_root, "link", "secret.txt");
        string direct = ScriptRealPath.Of(Path.Combine(_outside, "secret.txt"));

        Assert.Equal(direct, ScriptRealPath.Of(viaLink), ignoreCase: OperatingSystem.IsWindows());
        Assert.Equal(direct, ScriptRealPath.Of(ScriptRealPath.Walk(viaLink)), ignoreCase: OperatingSystem.IsWindows());
        Assert.False(ScriptRealPath.IsInside(ScriptRealPath.Walk(viaLink), ScriptRealPath.Walk(_root)));
    }

    [Fact]
    public void RealPath_MissingTail_IsAppendedToRealParent()
    {
        LinkOutside();

        string real = ScriptRealPath.Of(Path.Combine(_root, "link", "no", "such.txt"));

        Assert.True(ScriptRealPath.IsInside(real, ScriptRealPath.Of(_outside)));
        Assert.EndsWith(Path.Combine("no", "such.txt"), real, StringComparison.Ordinal);
    }

    // --- храповик: дверь на диск одна ---

    /// <summary>
    /// Файлы сборок скрипта, которым разрешено трогать диск, и сколько таких обращений в каждом.
    /// </summary>
    /// <remarks>
    /// Число только уменьшается. Новое обращение к <c>File</c>/<c>Directory</c>/<c>FileStream</c>
    /// в функции скрипта — это путь в обход <see cref="WorkspaceSandbox.Resolve"/>, то есть в
    /// обход сверки реального расположения.
    /// </remarks>
    private static readonly Dictionary<string, int> s_allowed = new(StringComparer.Ordinal)
    {
        // Сама дверь: каждое обращение стоит за Resolve.
        ["src/AI.Script/Hosting/WorkspaceSandbox.cs"] = 9,
        // Проверка реального расположения.
        ["src/AI.Script/Hosting/ScriptRealPath.cs"] = 3,
        // Кэш стадий: папку задаёт хост, имя файла — хэш ключа; путь из скрипта сюда не попадает.
        ["src/AI.Script/Hosting/IStageCache.cs"] = 5,
        // Временная копия базы SQLite: имя — GUID в системной папке Temp, исходник читается через хранилище.
        ["src/AI.Script.Data/SqliteCopy.cs"] = 3,
    };

    [Fact]
    public void ScriptAssemblies_TouchDiskOnlyThroughSandbox()
    {
        string root = RepositoryRoot();
        var found = new Dictionary<string, int>(StringComparer.Ordinal);

        foreach (string folder in Directory.EnumerateDirectories(Path.Combine(root, "src"), "AI.Script*"))
        {
            foreach (string file in Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(root, file).Replace('\\', '/');

                if (relative.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light")) continue;

                int count = DirectAccess().Matches(CodeOnly(File.ReadAllText(file))).Count;

                if (count > 0) found[relative] = count;
            }
        }

        string[] offenders = [.. found
            .Where(pair => !s_allowed.TryGetValue(pair.Key, out int limit) || pair.Value > limit)
            .Select(pair => $"{pair.Key}: {pair.Value} (разрешено {(s_allowed.TryGetValue(pair.Key, out int limit) ? limit : 0)})")
            .Order(StringComparer.Ordinal)];

        Assert.True(offenders.Length == 0, "обращения к диску в обход WorkspaceSandbox.Resolve:\n  " + string.Join("\n  ", offenders));

        string[] slack = [.. s_allowed
            .Where(pair => found.GetValueOrDefault(pair.Key) < pair.Value)
            .Select(pair => $"{pair.Key}: стало {found.GetValueOrDefault(pair.Key)}, в списке {pair.Value}")];

        Assert.True(slack.Length == 0, "обращений стало меньше — уменьшите число в храповике:\n  " + string.Join("\n  ", slack));
    }

    /// <summary>Убирает комментарии: упоминание <c>File.ReadAllText</c> в описании — не обращение к диску.</summary>
    private static string CodeOnly(string text) => Comments().Replace(text, string.Empty);

    private static string RepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory != null && !Directory.Exists(Path.Combine(directory.FullName, "src", "AI.Script")))
            directory = directory.Parent;

        return directory?.FullName ?? throw new InvalidOperationException("корень репозитория не найден");
    }

    [GeneratedRegex(@"\b(File|Directory|ZipFile)\s*\.\s*[A-Z]\w*\s*\(|\bnew\s+(FileInfo|FileStream|DirectoryInfo|StreamWriter|SKFileStream|SKFileWStream)\s*\(\s*(?!stream\b|new\b)|\bPath\s*\.\s*GetTempPath\b")]
    private static partial Regex DirectAccess();

    [GeneratedRegex(@"//[^\n]*|/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex Comments();
}

/// <summary>Создание ссылок для тестов.</summary>
internal static class Links
{
    /// <summary>Может ли процесс создать символическую ссылку: в Windows нужен режим разработчика или права администратора.</summary>
    public static readonly bool CanCreateSymlink = ProbeSymlink();

    /// <summary>
    /// Ссылка-папка без особых прав: в Windows — точка соединения (junction), в остальных ОС — символическая ссылка.
    /// </summary>
    public static void Directory(string link, string target)
    {
        if (!OperatingSystem.IsWindows())
        {
            _ = System.IO.Directory.CreateSymbolicLink(link, target);

            return;
        }

        var start = new ProcessStartInfo("cmd.exe")
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

        start.ArgumentList.Add("/c");
        start.ArgumentList.Add("mklink");
        start.ArgumentList.Add("/J");
        start.ArgumentList.Add(link);
        start.ArgumentList.Add(target);

        using Process process = Process.Start(start)!;

        string errors = process.StandardError.ReadToEnd();

        process.WaitForExit();

        if (process.ExitCode != 0 || !System.IO.Directory.Exists(link))
        {
            throw new InvalidOperationException($"не удалось создать точку соединения '{link}': {errors}");
        }
    }

    /// <summary>Удаляет дерево, не заходя в ссылки: удаляется сама ссылка, а не то, куда она ведёт.</summary>
    public static void DeleteTree(string folder)
    {
        foreach (string entry in System.IO.Directory.EnumerateFileSystemEntries(folder))
        {
            FileAttributes attributes = File.GetAttributes(entry);

            if ((attributes & FileAttributes.Directory) == 0) File.Delete(entry);
            else if ((attributes & FileAttributes.ReparsePoint) != 0) System.IO.Directory.Delete(entry);
            else DeleteTree(entry);
        }

        System.IO.Directory.Delete(folder);
    }

    private static bool ProbeSymlink()
    {
        string folder = Path.Combine(Path.GetTempPath(), "aiscript-symlink-probe-" + Guid.NewGuid().ToString("N"));

        try
        {
            _ = System.IO.Directory.CreateDirectory(folder);
            File.WriteAllText(Path.Combine(folder, "target.txt"), "x");
            _ = File.CreateSymbolicLink(Path.Combine(folder, "link.txt"), Path.Combine(folder, "target.txt"));

            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        finally
        {
            try
            {
                System.IO.Directory.Delete(folder, recursive: true);
            }
            catch (IOException)
            {
                // Проба во временной папке: её уберёт система.
            }
        }
    }
}

/// <summary>
/// Тест, которому нужна символическая ссылка: пропускается с причиной, если ОС не даёт её создать.
/// </summary>
internal sealed class SymlinkFactAttribute : FactAttribute
{
    public SymlinkFactAttribute()
    {
        if (!Links.CanCreateSymlink)
        {
            Skip = "ОС не дала создать символическую ссылку: в Windows нужен режим разработчика или права администратора";
        }
    }
}
