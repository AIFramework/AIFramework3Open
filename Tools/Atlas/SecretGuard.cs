using System.Text.RegularExpressions;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Поиск ключей OpenRouter в файлах рабочего дерева.
/// </summary>
/// <remarks>
/// Репозиторий публичный: ключ, попавший в коммит, считается утёкшим, даже если его потом
/// удалить. Сторож смотрит рабочее дерево до коммита — отслеживаемые файлы и новые, не
/// попавшие в <c>.gitignore</c>. Находка печатается как «файл:строка», без самого значения:
/// иначе отчёт сторожа стал бы ещё одним местом утечки.
/// </remarks>
public static class SecretGuard
{
    private const int MaxFileBytes = 10 * 1024 * 1024;
    private const int BinaryProbeBytes = 8000;

    /// <summary>Формат ключа OpenRouter: префикс и 64 шестнадцатеричных знака.</summary>
    private static readonly Regex OpenRouterKey = new("sk-or-v1-[0-9a-f]{64}", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    /// <summary>Находка: где и что.</summary>
    public sealed record Finding(string File, int Line, string Kind);

    /// <summary>Ищет ключи в файлах.</summary>
    /// <param name="files">Файлы; несуществующие, двоичные и крупнее 10 МБ пропускаются.</param>
    /// <param name="knownKey">Значение ключа из окружения; ловит и ключ нестандартного вида.</param>
    public static IReadOnlyList<Finding> Scan(IEnumerable<string> files, string? knownKey)
    {
        ArgumentNullException.ThrowIfNull(files);

        var findings = new List<Finding>();

        foreach (string file in files)
        {
            if (!IsScannableText(file)) continue;

            string[] lines = File.ReadAllLines(file);

            for (int i = 0; i < lines.Length; i++)
            {
                if (knownKey != null && lines[i].Contains(knownKey, StringComparison.Ordinal))
                    findings.Add(new Finding(file, i + 1, $"значение {AtlasSettings.KeyVariable}"));
                else if (OpenRouterKey.IsMatch(lines[i]))
                    findings.Add(new Finding(file, i + 1, "ключ OpenRouter"));
            }
        }

        return findings;
    }

    private static bool IsScannableText(string file)
    {
        var info = new FileInfo(file);

        if (!info.Exists || info.Length > MaxFileBytes) return false;

        using FileStream stream = info.OpenRead();
        var probe = new byte[Math.Min(BinaryProbeBytes, info.Length)];
        int read = stream.Read(probe, 0, probe.Length);

        return Array.IndexOf(probe, (byte)0, 0, read) < 0;
    }
}
