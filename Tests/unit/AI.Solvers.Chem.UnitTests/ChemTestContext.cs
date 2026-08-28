using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Общая обвязка тестов химического движка.</summary>
/// <remarks>
/// Движок и справочник создаются один раз на сборку: конструктор поднимает таблицу
/// элементов, справочники и читает базу синтезов с диска, а xunit создаёт по экземпляру
/// класса на каждый тест.
/// </remarks>
internal static class ChemTestContext
{
    private static readonly Lazy<ChemEngine> LazyEngine = new(() => new ChemEngine(VerbosityLevel.Detailed));
    private static readonly Lazy<ChemDatabase> LazyDatabase = new(() =>
    {
        var database = new ChemDatabase();
        database.Initialize();
        return database;
    });

    /// <summary>Движок команд.</summary>
    public static ChemEngine Engine => LazyEngine.Value;

    /// <summary>Справочник элементов и соединений.</summary>
    public static ChemDatabase Database => LazyDatabase.Value;

    /// <summary>
    /// Движок форматирует результаты текущей культурой, а тесты сверяют текст,
    /// поэтому культура сборки фиксируется инвариантной.
    /// </summary>
    [ModuleInitializer]
    internal static void UseInvariantCulture()
    {
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
    }

    /// <summary>Выполняет команду и требует успеха.</summary>
    /// <param name="command">Команда движка.</param>
    public static ChemResult Ok(string command)
    {
        ChemResult result = Engine.Execute(command);
        Assert.True(result.Success, $"команда '{command}' не выполнена: {result.ErrorMessage}");

        return result;
    }

    /// <summary>Выполняет команду и требует отказа.</summary>
    /// <param name="command">Команда движка.</param>
    public static ChemResult Fail(string command)
    {
        ChemResult result = Engine.Execute(command);
        Assert.False(result.Success, $"команда '{command}' неожиданно выполнилась: {result.Result}");

        return result;
    }

    /// <summary>Текст результата вместе с шагами решения.</summary>
    /// <param name="result">Исход выполнения команды.</param>
    public static string FullText(this ChemResult result)
        => (result.Result ?? string.Empty) + " " + string.Join(" ", result.Steps);

    /// <summary>Проверяет, что в тексте результата есть все фрагменты.</summary>
    /// <param name="result">Исход выполнения команды.</param>
    /// <param name="fragments">Ожидаемые подстроки.</param>
    public static void ShouldContain(this ChemResult result, params string[] fragments)
    {
        string text = result.FullText();

        foreach (string fragment in fragments)
            Assert.True(text.Contains(fragment, StringComparison.OrdinalIgnoreCase),
                $"в ответе нет '{fragment}': {text.Replace("\n", " ⏎ ")}");
    }
}
