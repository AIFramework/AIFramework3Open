using AI.Script.Runtime;
using AI.Script.Semantics;
using Microsoft.Win32.SafeHandles;
using System.ComponentModel;
using System.Runtime.InteropServices;

namespace AI.Script.Hosting;

/// <summary>
/// Реальное расположение пути на диске: все ссылки по дороге раскрыты.
/// </summary>
/// <remarks>
/// <see cref="Path.GetFullPath(string)"/> работает со строкой и о диске ничего не знает: точка
/// соединения (junction) или символическая ссылка внутри рабочей папки остаётся «внутри», хотя
/// ведёт куда угодно. Здесь путь спрашивается у файловой системы.
/// <para>
/// Windows: открывается сам объект (или ближайший существующий предок) и берётся путь его
/// дескриптора — так раскрываются символические ссылки, точки соединения, подставные диски и
/// любые другие точки повторной обработки, которые проходит система. Остальные ОС: путь
/// проходится по частям, каждая ссылка раскрывается.
/// </para>
/// <para>
/// Несуществующий хвост пути ссылкой быть не может, поэтому дописывается как есть. Ссылка без
/// цели — отказ: запись «в неё» создала бы файл там, куда она указывает.
/// </para>
/// </remarks>
public static class ScriptRealPath
{
    /// <summary>Предел переходов по ссылкам: дальше — петля.</summary>
    private const int MaxHops = 40;

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const uint FileFlagBackupSemantics = 0x02000000;
    private const uint OpenExisting = 3;
    private const uint ShareAll = 7;

    /// <summary>
    /// Реальное расположение пути; несуществующий хвост дописывается к реальному пути предка.
    /// </summary>
    /// <param name="path">Путь на диске.</param>
    /// <exception cref="ScriptError">Расположение проверить не удалось либо ссылка ведёт в никуда.</exception>
    public static string Of(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        string full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));

        return OperatingSystem.IsWindows() ? OfExistingAncestor(full) : Walk(full);
    }

    /// <summary>
    /// Реальный путь уже открытого файла; <c>null</c>, если система его не сообщает.
    /// </summary>
    /// <remarks>
    /// Вторая сверка после открытия: между проверкой пути и открытием файл могли подменить
    /// ссылкой, а дескриптор указывает на то, что открыто на самом деле.
    /// </remarks>
    public static string? OfHandle(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);

        if (OperatingSystem.IsWindows()) return FinalPath(handle);

        if (!OperatingSystem.IsLinux()) return null;

        var link = new FileInfo($"/proc/self/fd/{handle.DangerousGetHandle()}");

        return link.LinkTarget;
    }

    /// <summary>Лежит ли реальный путь внутри реального корня (или совпадает с ним).</summary>
    public static bool IsInside(string realPath, string realRoot)
    {
        StringComparison comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string path = Path.TrimEndingDirectorySeparator(realPath);
        string root = Path.TrimEndingDirectorySeparator(realRoot);

        if (string.Equals(path, root, comparison)) return true;

        string head = Path.EndsInDirectorySeparator(root) ? root : root + Path.DirectorySeparatorChar;

        return path.StartsWith(head, comparison);
    }

    /// <summary>
    /// Проход пути по частям с раскрытием каждой ссылки; работает на любой ОС.
    /// </summary>
    public static string Walk(string fullPath)
    {
        string root = Path.GetPathRoot(fullPath) ?? throw Unverifiable(fullPath, "у пути нет корня");
        var pending = new Stack<string>();

        Push(pending, fullPath[root.Length..]);

        string current = root;
        int hops = 0;

        while (pending.Count > 0)
        {
            string name = pending.Pop();

            if (name == "..")
            {
                current = Path.GetDirectoryName(current) ?? Path.GetPathRoot(current)!;
                continue;
            }

            string next = Path.Combine(current, name);
            string? target = new FileInfo(next).LinkTarget;

            if (target == null)
            {
                current = next;
                continue;
            }

            if (++hops > MaxHops) throw Unverifiable(fullPath, "ссылки образуют петлю");

            // Цель ссылки сама может идти через ссылки и «..», поэтому она не склеивается
            // строкой, а проходится тем же циклом.
            string? targetRoot = Path.IsPathRooted(target) ? Path.GetPathRoot(Path.GetFullPath(target)) : null;

            if (targetRoot != null)
            {
                current = targetRoot;
                Push(pending, Path.GetFullPath(target)[targetRoot.Length..]);
            }
            else
            {
                Push(pending, target);
            }
        }

        return Path.TrimEndingDirectorySeparator(current);
    }

    private static void Push(Stack<string> pending, string tail)
    {
        string[] parts = tail.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);

        for (int index = parts.Length - 1; index >= 0; index--)
        {
            if (parts[index] != ".") pending.Push(parts[index]);
        }
    }

    private static string OfExistingAncestor(string full)
    {
        var tail = new Stack<string>();
        string current = full;

        while (true)
        {
            string? real = TryFinalPath(current, out int error);

            if (real != null) return tail.Count == 0 ? real : Path.Combine([real, .. tail]);

            if (error is not (ErrorFileNotFound or ErrorPathNotFound))
            {
                throw Unverifiable(full, new Win32Exception(error).Message);
            }

            // Объект не открылся, но запись в папке есть — это ссылка без цели.
            if (IsDanglingLink(current)) throw Unverifiable(full, "ссылка в пути никуда не ведёт");

            string? parent = Path.GetDirectoryName(current);

            if (string.IsNullOrEmpty(parent)) throw Unverifiable(full, "диск или сетевая папка недоступны");

            tail.Push(Path.GetFileName(current));
            current = parent;
        }
    }

    private static bool IsDanglingLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
    }

    private static string? TryFinalPath(string path, out int error)
    {
        // Доступ 0: нужен только сам объект, права на чтение содержимого не требуются, чужая
        // блокировка файла не мешает. BACKUP_SEMANTICS позволяет открыть папку.
        using SafeFileHandle handle = CreateFileW(Extended(path), 0, ShareAll, IntPtr.Zero, OpenExisting, FileFlagBackupSemantics, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            error = Marshal.GetLastWin32Error();

            return null;
        }

        error = 0;

        return FinalPath(handle) ?? throw Unverifiable(path, "система не сообщила путь открытого объекта");
    }

    private static string? FinalPath(SafeFileHandle handle)
    {
        char[] buffer = new char[512];

        // 0 = FILE_NAME_NORMALIZED | VOLUME_NAME_DOS; том без буквы даёт ошибку — тогда имя тома по GUID.
        foreach (uint flags in new uint[] { 0, 1 })
        {
            uint length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, flags);

            if (length > buffer.Length)
            {
                buffer = new char[length];
                length = GetFinalPathNameByHandleW(handle, buffer, (uint)buffer.Length, flags);
            }

            if (length > 0 && length <= buffer.Length) return Plain(new string(buffer, 0, (int)length));
        }

        return null;
    }

    /// <summary>Путь с приставкой <c>\\?\</c>: без неё система режет длинные пути и разбирает имена устройств.</summary>
    private static string Extended(string path)
    {
        if (path.StartsWith(@"\\?\", StringComparison.Ordinal) || path.StartsWith(@"\\.\", StringComparison.Ordinal)) return path;

        return path.StartsWith(@"\\", StringComparison.Ordinal) ? @"\\?\UNC\" + path[2..] : @"\\?\" + path;
    }

    /// <summary>Обратное к <see cref="Extended"/>: привычный вид <c>C:\папка</c> или <c>\\сервер\папка</c>.</summary>
    internal static string Plain(string path)
    {
        if (path.StartsWith(@"\\?\UNC\", StringComparison.OrdinalIgnoreCase)) return @"\\" + path[8..];

        // Имя тома по GUID остаётся с приставкой: без неё это не путь.
        if (path.StartsWith(@"\\?\Volume{", StringComparison.OrdinalIgnoreCase)) return Path.TrimEndingDirectorySeparator(path);

        string plain = path.StartsWith(@"\\?\", StringComparison.Ordinal) ? path[4..] : path;

        return Path.TrimEndingDirectorySeparator(plain);
    }

    private static ScriptError Unverifiable(string path, string reason) =>
        new(DiagnosticCodes.SandboxDenied,
            $"не удалось проверить, где на самом деле лежит '{Path.GetFileName(path)}': {reason}",
            "путь должен вести к обычному файлу или папке внутри рабочей папки прогона");

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(
        string fileName, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, ExactSpelling = true, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle handle, [Out] char[] path, uint length, uint flags);
}
