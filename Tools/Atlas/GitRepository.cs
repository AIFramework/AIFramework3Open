using System.Diagnostics;
using System.Text;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Вызовы git: корень репозитория и файлы рабочего дерева.
/// </summary>
public static class GitRepository
{
    /// <summary>Корень репозитория, которому принадлежит папка.</summary>
    /// <exception cref="InvalidOperationException">Папка не в репозитории git или git не найден.</exception>
    public static string TopLevel(string path) =>
        Path.GetFullPath(Run(path, "rev-parse", "--show-toplevel").Trim());

    /// <summary>
    /// Файлы рабочего дерева: отслеживаемые и новые, не исключённые <c>.gitignore</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">Папка не в репозитории git или git не найден.</exception>
    public static IReadOnlyList<string> WorkingTreeFiles(string path)
    {
        string top = TopLevel(path);
        string[] names = Run(top, "ls-files", "-z", "--cached", "--others", "--exclude-standard")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries);

        return [.. names.Select(name => Path.Combine(top, name)).Where(File.Exists)];
    }

    /// <summary>
    /// Состояние рабочего дерева одной строкой: коммит и отпечаток незакоммиченных правок
    /// (пути, размеры и время изменения файлов из <c>git status</c>). Совпало — индекс по
    /// этому дереву пересобирать незачем.
    /// </summary>
    public static string State(string path)
    {
        string top = TopLevel(path);
        string head = Run(top, "rev-list", "-n", "1", "--all").Trim() is { Length: > 0 } ? Run(top, "rev-parse", "HEAD").Trim() : NoCommits;
        // Выходы сборки (bin, obj) — не исходники: без .gitignore их пишет даже загрузка проектов.
        string[] changed = [.. Run(top, "status", "--porcelain=v1", "-z", "--untracked-files=all")
            .Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(entry => !entry.Split('/', '\\').Any(segment => segment is "bin" or "obj"))];

        if (changed.Length == 0) return head;

        var builder = new StringBuilder();
        foreach (string entry in changed)
        {
            string file = Path.Combine(top, entry.Length > 3 ? entry[3..] : entry);
            var info = new FileInfo(file);
            builder.Append(entry).Append('|').Append(info.Exists ? $"{info.Length}|{info.LastWriteTimeUtc.Ticks}" : "-").Append('\n');
        }

        byte[] hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return $"{head}+{Convert.ToHexString(hash, 0, 8).ToLowerInvariant()}";
    }

    /// <summary>Короткая запись версии: коммит и пометка о правках.</summary>
    public static string Describe(string state)
    {
        string head = state.Split('+')[0];
        return (head.Length > 8 && head.All(char.IsAsciiHexDigit) ? head[..8] : head) + (state.Contains('+') ? " с незакоммиченными правками" : "");
    }

    private const string NoCommits = "без коммитов";

    private static string Run(string folder, params string[] arguments)
    {
        // Свой stdin обязателен: в MCP-сервере унаследованный stdin — канал протокола, и git
        // на нём зависал (проверено зондом по stdio). Пустой закрытый поток ничего не ждёт.
        var start = new ProcessStartInfo("git")
        {
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            CreateNoWindow = true,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(folder);
        foreach (string argument in arguments) start.ArgumentList.Add(argument);

        Process process;

        try
        {
            process = Process.Start(start) ?? throw new InvalidOperationException("Не удалось запустить git.");
        }
        catch (System.ComponentModel.Win32Exception)
        {
            throw new InvalidOperationException("git не найден в PATH.");
        }

        using (process)
        {
            process.StandardInput.Close();
            Task<string> error = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
                throw new InvalidOperationException($"git завершился с кодом {process.ExitCode}: {error.Result.Trim()}");

            return output;
        }
    }
}
