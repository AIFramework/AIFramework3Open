using System.Text.Json;

namespace AiFramework.Tools.Atlas;

/// <summary>
/// Итоги исполнения по отпечаткам тел: пара исполняется заново, только если изменился код
/// одного из методов, пересобраны сборки или поменялись правила сравнения (<see cref="Version"/>).
/// </summary>
/// <remarks>Файл лежит рядом с индексом в <c>%LOCALAPPDATA%\Atlas</c>; это кэш, его можно удалить.</remarks>
public sealed class BehaviorCache(string path)
{
    /// <summary>Версия правил сравнения: поднять при изменении генераторов, переходников или связей.</summary>
    public const int Version = 4;

    private const int MaxEntries = 20_000;

    private readonly Dictionary<string, BehaviorResult> _results = Load(path);
    private readonly HashSet<string> _touched = [];

    /// <summary>Сколько пар в кэше.</summary>
    public int Count => _results.Count;

    /// <summary>Итог пары, если не менялись ни код методов, ни сборки (<paramref name="build"/>).</summary>
    public bool TryGet(CodeUnit a, CodeUnit b, string build, out BehaviorResult result)
    {
        string key = Key(a, b, build);
        if (!_results.TryGetValue(key, out result!)) return false;
        _touched.Add(key);
        return true;
    }

    /// <summary>Запоминает итог.</summary>
    public void Put(CodeUnit a, CodeUnit b, string build, BehaviorResult result)
    {
        string key = Key(a, b, build);
        _results[key] = result;
        _touched.Add(key);
    }

    /// <summary>Записывает кэш на диск; разросшийся кэш сокращается до пар, тронутых в этом запуске.</summary>
    public void Save()
    {
        if (_results.Count > MaxEntries)
            foreach (string stale in _results.Keys.Where(key => !_touched.Contains(key)).ToList()) _results.Remove(stale);

        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, JsonSerializer.Serialize(new Snapshot(Version, _results)));
    }

    private static Dictionary<string, BehaviorResult> Load(string path)
    {
        try
        {
            Snapshot? snapshot = File.Exists(path) ? JsonSerializer.Deserialize<Snapshot>(File.ReadAllText(path)) : null;
            return snapshot?.Version == Version ? snapshot.Results : [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static string Key(CodeUnit a, CodeUnit b, string build) => $"{a.Project}|{a.Id}|{a.Hash}|{b.Project}|{b.Id}|{b.Hash}|{build}";

    private sealed record Snapshot(int Version, Dictionary<string, BehaviorResult> Results);
}
