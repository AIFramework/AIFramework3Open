using AI.Script.Runtime;
using AI.Script.Semantics;
using System.IO.Enumeration;

namespace AI.Script.Hosting;

/// <summary>
/// Пути из скрипта: приведение к одному виду и сверка с маской.
/// </summary>
/// <remarks>
/// Одно место на все хранилища. Правило «наружу нельзя» живёт здесь, а не в каждой
/// реализации: хранилище хоста, написанное завтра, получает его даром и не может забыть.
/// </remarks>
public static class ScriptPaths
{
    /// <summary>Путь корня хранилища.</summary>
    public const string RootPath = ".";

    /// <summary>
    /// Приводит путь к виду <c>папка/файл</c> относительно корня; отказ, если путь пуст,
    /// абсолютен или выходит наружу.
    /// </summary>
    /// <remarks>
    /// Разбирается по частям, а не ищется подстрока <c>..</c>: поиск подстроки обходится
    /// тривиально, а разбор по частям — нет. Оба разделителя равноправны, потому что пути
    /// пишет модель, а она пишет и так, и так.
    /// </remarks>
    public static string Normalize(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                "пустой путь",
                "путь отсчитывается от рабочей папки прогона: \"data/sales.csv\"");
        }

        string trimmed = path.Trim();

        if (IsAbsolute(trimmed))
        {
            throw new ScriptError(
                DiagnosticCodes.SandboxDenied,
                $"абсолютные пути запрещены: '{path}'",
                "пути отсчитываются от рабочей папки прогона: \"data/sales.csv\"");
        }

        var segments = new List<string>();

        foreach (string segment in trimmed.Split('/', '\\'))
        {
            if (segment.Length == 0 || segment == ".") continue;

            if (segment != "..")
            {
                segments.Add(segment);
                continue;
            }

            if (segments.Count == 0)
            {
                throw new ScriptError(
                    DiagnosticCodes.SandboxDenied,
                    $"путь выходит за рабочую папку: '{path}'",
                    "разрешено только внутри рабочей папки прогона");
            }

            segments.RemoveAt(segments.Count - 1);
        }

        return segments.Count == 0 ? RootPath : string.Join('/', segments);
    }

    /// <summary>Подходит ли имя файла под маску вида <c>*.csv</c>; пустая маска — любое.</summary>
    public static bool Matches(string name, string mask) =>
        string.IsNullOrWhiteSpace(mask) || mask == "*" || FileSystemName.MatchesSimpleExpression(mask, name, ignoreCase: true);

    /// <summary>
    /// Абсолютен ли путь на любой платформе.
    /// </summary>
    /// <remarks>
    /// <see cref="Path.IsPathRooted(string)"/> на Linux не считает абсолютным <c>C:/x</c>, а
    /// скрипт, написанный под Windows, исполняется где угодно.
    /// </remarks>
    private static bool IsAbsolute(string path) =>
        path[0] is '/' or '\\' || (path.Length >= 2 && path[1] == ':') || Path.IsPathRooted(path);
}
