using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Хозяин рабочего процесса: отправляет пары на исполнение и жёстко снимает процесс по
/// времени и по памяти. Снятый или упавший процесс поднимается заново при следующем запросе.
/// </summary>
/// <remarks>
/// Рабочий процесс запускается в пустой временной папке и без переменных окружения, в
/// имени которых есть KEY, TOKEN, SECRET или PASSWORD: исполняемому коду библиотеки ключ
/// OpenRouter ни к чему.
/// </remarks>
public sealed class BehaviorHost(string root, TimeSpan? timeout = null, long memoryLimit = 1L << 30) : IDisposable
{
    private static readonly string[] SecretMarkers = ["KEY", "TOKEN", "SECRET", "PASSWORD"];

    private readonly string _sandbox = Directory.CreateTempSubdirectory("atlas-worker-").FullName;
    private Process? _worker;
    private string _stderr = "";

    /// <summary>Сколько ждать итога одной пары.</summary>
    public TimeSpan Timeout { get; } = timeout ?? TimeSpan.FromSeconds(20);

    /// <summary>Исполняет пару в рабочем процессе.</summary>
    public async Task<BehaviorResult> CompareAsync(MethodRef a, MethodRef b, int seed = 1, int cases = 40, CancellationToken cancellation = default)
    {
        Process worker = _worker ??= Start();

        try
        {
            await worker.StandardInput.WriteLineAsync(JsonSerializer.Serialize(new BehaviorRequest(a, b, seed, cases)));
            await worker.StandardInput.FlushAsync(cancellation);
        }
        catch (IOException)
        {
            Stop();
            return BehaviorResult.Unchecked("исполнитель завершился до запроса");
        }

        Task<string?> reply = worker.StandardOutput.ReadLineAsync(cancellation).AsTask();
        var clock = Stopwatch.StartNew();

        while (!reply.IsCompleted)
        {
            await Task.WhenAny(reply, Task.Delay(50, cancellation));
            if (reply.IsCompleted) break;

            worker.Refresh();
            string? stop = worker.HasExited ? "исполнитель завершился"
                : clock.Elapsed > Timeout ? $"снят по времени: дольше {Timeout.TotalSeconds:F0} с"
                : worker.PrivateMemorySize64 > memoryLimit ? $"снят по памяти: больше {memoryLimit >> 20} МБ"
                : null;

            if (stop != null)
            {
                Stop();
                return BehaviorResult.Unchecked(stop);
            }
        }

        string? line = await reply;
        if (line != null) return JsonSerializer.Deserialize<BehaviorResult>(line)!;

        string why = "исполнитель завершился" + (_stderr.Length > 0 ? ": " + _stderr : "");
        Stop();
        return BehaviorResult.Unchecked(why);
    }

    /// <summary>Снимает рабочий процесс и удаляет временную папку.</summary>
    public void Dispose()
    {
        Stop();
        try
        {
            Directory.Delete(_sandbox, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private Process Start()
    {
        string exe = Path.Combine(AppContext.BaseDirectory, OperatingSystem.IsWindows() ? "atlas.exe" : "atlas");
        var info = new ProcessStartInfo(exe)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = _sandbox,
            StandardInputEncoding = new UTF8Encoding(false),
            StandardOutputEncoding = Encoding.UTF8,
        };
        info.ArgumentList.Add("worker");
        info.ArgumentList.Add(root);

        foreach (string name in info.Environment.Keys.Where(name => SecretMarkers.Any(marker => name.Contains(marker, StringComparison.OrdinalIgnoreCase))).ToList())
            info.Environment.Remove(name);

        _stderr = "";
        Process process = Process.Start(info) ?? throw new InvalidOperationException("Не удалось запустить исполнитель: " + exe);
        process.ErrorDataReceived += (_, e) =>
        {
            if (!string.IsNullOrWhiteSpace(e.Data) && _stderr.Length < 300) _stderr += e.Data.Trim() + " ";
        };
        process.BeginErrorReadLine();
        return process;
    }

    private void Stop()
    {
        if (_worker is not { } worker) return;
        _worker = null;

        try
        {
            if (!worker.HasExited) worker.Kill(entireProcessTree: true);
            worker.WaitForExit(5000);
        }
        catch (InvalidOperationException)
        {
        }

        worker.Dispose();
    }
}
