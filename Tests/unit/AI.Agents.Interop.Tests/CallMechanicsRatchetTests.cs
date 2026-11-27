using System.Text.RegularExpressions;
using Xunit;

namespace AI.Agents.Interop.Tests;

/// <summary>
/// Храповик механики вызова модели (этап 3, блок Б, С2б): сколько файлов в <c>src</c> несут свою копию механизма.
/// Порог только опускается: число выросло — появилась вторая реализация, число упало — опустите порог здесь же.
/// </summary>
/// <remarks>
/// Признак каждого механизма — текст, без которого копию не написать (заголовок <c>Bearer</c> у своего ключа, цикл с
/// паузой у своего повтора и т. п.). Ищется по исходникам <c>src</c>, без каталогов сборки и тестов. Место, где механизм
/// живет по праву, указано в <see cref="Mechanism.Home"/> и в счет не входит.
/// </remarks>
public sealed class CallMechanicsRatchetTests
{
    /// <summary>Механизм, его признак и порог.</summary>
    public sealed record Mechanism(string Name, string Pattern, int Threshold, string? Home = null, string[]? Scope = null)
    {
        public override string ToString() => Name;
    }

    /// <summary>Механизмы и пороги.</summary>
    public static readonly Mechanism[] Mechanisms =
    [
        // 1. Ключ провайдера: свой заголовок Authorization со своим ключом.
        new("keys", "\"Bearer", Threshold: 7),

        // 3. Повторы: свой цикл попыток с паузой.
        new("retry-loops", @"(?s)\battempt\b.*Task\.Delay\(|Task\.Delay\(.*\battempt\b", Threshold: 7),

        // 3. Повторы: свой разбор Retry-After.
        new("retry-after", @"[Rr]etryAfter\?\.Delta", Threshold: 3),

        // 3. Повторы: своё правило «временный ли статус».
        new("transient-status", @"(==|!=)\s*HttpStatusCode\.TooManyRequests|\b429 or\b", Threshold: 4),

        // 5. HTTP-клиент провайдера: свой HttpClient или обработчик.
        new("http-clients", @"new HttpClient\b|new SocketsHttpHandler\b|new HttpClientHandler\b", Threshold: 11),

        // 5. Прокси: свой обработчик с прокси.
        new("proxy-handlers", @"UseProxy = true", Threshold: 2),

        // 6. Сбой хоста модели выглядит как успех или как «неверный формат».
        new("model-failure-as-success", @"(?s)catch \(Exception[^)]*\)\s*\{(?:(?!catch \().){0,400}?(Malformed\(null\)|return true;)", Threshold: 3,
            Scope: ["src/AI.LLM/Agents", "src/AI.Agents.Interop"]),

        // 7. Вид действия и повторяемость инструмента — два словаря вместо одного описания.
        new("split-effects", @"Dictionary<string, ToolIdempotency>", Threshold: 2),

        // 4. Выбор модели по имени в слое агентов: движки берут модель по роли через IModelPort.
        new("model-by-name", @"\bModelName\b|new ChatLLMApi\(|ModelApi\(", Threshold: 0,
            Scope: ["src/AI.Agents.Interop", "src/AI.Script.Llm", "src/AI.LLM/Agents"]),
    ];

    public static TheoryData<Mechanism> All()
    {
        var data = new TheoryData<Mechanism>();
        foreach (Mechanism mechanism in Mechanisms)
            data.Add(mechanism);
        return data;
    }

    [Theory]
    [MemberData(nameof(All))]
    public void Copies_DoNotGrow(Mechanism mechanism)
    {
        string[] found = Offenders(mechanism);

        Assert.True(found.Length <= mechanism.Threshold,
            $"{mechanism.Name}: {found.Length} мест при пороге {mechanism.Threshold} — появилась ещё одна копия механизма, " +
            "используйте общую реализацию: " + string.Join(", ", found));
    }

    [Theory]
    [MemberData(nameof(All))]
    public void Threshold_FollowsTheCount(Mechanism mechanism)
    {
        string[] found = Offenders(mechanism);

        Assert.True(found.Length >= mechanism.Threshold,
            $"{mechanism.Name}: мест стало {found.Length}, порог {mechanism.Threshold} — опустите порог до {found.Length} " +
            "в CallMechanicsRatchetTests, чтобы копия не вернулась незаметно.");
    }

    private static string[] Offenders(Mechanism mechanism)
    {
        var pattern = new Regex(mechanism.Pattern, RegexOptions.CultureInvariant);
        string[] scope = mechanism.Scope ?? ["src"];

        return [.. scope
            .Select(dir => Path.Combine(Repository.Root, dir))
            .Where(Directory.Exists)
            .SelectMany(dir => Directory.EnumerateFiles(dir, "*.cs", SearchOption.AllDirectories))
            .Select(Repository.Relative)
            .Where(path => !IsExcluded(path) && path != mechanism.Home)
            .Where(path => pattern.IsMatch(File.ReadAllText(Path.Combine(Repository.Root, path))))
            .Distinct()
            .OrderBy(path => path, StringComparer.Ordinal)];
    }

    private static bool IsExcluded(string path) =>
        path.Split('/').Any(part => part is "bin" or "obj" or "bin-light" or "obj-light" or "Tests" or "artifacts");
}
