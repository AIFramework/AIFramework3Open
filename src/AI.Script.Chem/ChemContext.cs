using AI.Solvers.Chem.Core;
using AI.Solvers.Chem.Database;

namespace AI.Script.Chem;

/// <summary>
/// Общие для модуля объекты химического движка
/// </summary>
/// <remarks>
/// Движок и база создаются один раз: конструктор <see cref="ChemEngine"/> поднимает
/// таблицу элементов, справочники и читает базу синтезов с диска, и делать это
/// на каждый вызов функции скрипта незачем.
/// </remarks>
internal static class ChemContext
{
    private static readonly Lazy<ChemEngine> LazyEngine = new(() => new ChemEngine(VerbosityLevel.Normal));
    private static readonly Lazy<ChemDatabase> LazyDatabase = new(() =>
    {
        var database = new ChemDatabase();
        database.Initialize();
        return database;
    });

    /// <summary>Движок команд</summary>
    public static ChemEngine Engine => LazyEngine.Value;

    /// <summary>Справочник элементов и соединений</summary>
    public static ChemDatabase Database => LazyDatabase.Value;
}
