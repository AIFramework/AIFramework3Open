using AI.Script.Hosting;
using AI.Script.Runtime;

namespace AI.Script.Binding;

/// <summary>
/// То, что функция модуля может попросить у прогона: отмену, вывод, ГСЧ, обратный вызов.
/// </summary>
/// <remarks>
/// Параметр этого типа в сигнатуре C#-метода подставляет рантайм, и в сигнатуре языка он не
/// виден: контекст нужен реализации, а не тому, кто пишет скрипт.
/// </remarks>
public interface IScriptContext
{
    /// <summary>Отмена прогона.</summary>
    CancellationToken Cancellation { get; }

    /// <summary>
    /// Генератор случайных чисел прогона, засеянный из <c>options.seed</c>.
    /// </summary>
    /// <remarks>
    /// Свой на прогон, а не глобальная статика: два прогона в одном процессе не должны
    /// влиять друг на друга, иначе воспроизводимость держится на порядке запуска.
    /// </remarks>
    Random Random { get; }

    /// <summary>Зерно прогона.</summary>
    int Seed { get; }

    /// <summary>
    /// Доступ к файловой системе.
    /// </summary>
    /// <remarks>
    /// Модуль не открывает файл сам: он просит песочницу выдать путь. Так «нельзя выйти за
    /// рабочую папку» перестаёт зависеть от аккуратности каждой отдельной функции.
    /// </remarks>
    IScriptSandbox Sandbox { get; }

    /// <summary>
    /// Разрешение обращаться к сети.
    /// </summary>
    /// <remarks>
    /// Модуль не решает сам, можно ли ему в сеть: он спрашивает у прогона — так же, как просит
    /// у песочницы путь к файлу. Иначе «сеть выключена» зависело бы от аккуратности каждой
    /// отдельной функции.
    /// </remarks>
    Hosting.NetworkPolicy Network { get; }

    /// <summary>
    /// Заявляет о намерении сделать платный вызов; бросает, если потолок вызовов исчерпан.
    /// </summary>
    /// <remarks>
    /// Вызывается перед обращением к службе, <see cref="CountExternal"/> — после. Порядок
    /// важен: потолок вызовов обязан запретить лишний запрос, а не сообщить о нём задним
    /// числом, когда он уже оплачен.
    /// </remarks>
    void BeginExternalCall();

    /// <summary>
    /// Учитывает расход состоявшегося вызова; бросает при выходе за потолок токенов или стоимости.
    /// </summary>
    /// <param name="tokens">Израсходовано токенов; ноль, если служба их не сообщает.</param>
    /// <param name="cost">Стоимость вызова; ноль, если служба её не сообщает.</param>
    void CountExternal(long tokens = 0, decimal cost = 0);

    /// <summary>Сколько прогон уже израсходовал на платные вызовы.</summary>
    ExternalUsage Usage { get; }

    /// <summary>Печатает строку в транскрипт.</summary>
    void Print(string line);

    /// <summary>Показывает значение пользователю.</summary>
    void Show(ScriptValue value);

    /// <summary>Считает шаг интерпретатора; бросает при выходе за потолок.</summary>
    void CountStep();

    /// <summary>Учитывает выделение элементов данных; бросает при выходе за потолок памяти.</summary>
    void CountAllocation(long elements);

    /// <summary>Вызывает значение-функцию: нужно функциям высшего порядка вроде <c>core.map</c>.</summary>
    ValueTask<ScriptValue> CallAsync(ScriptValue callable, params ScriptValue[] arguments);

    /// <summary>
    /// Вызывает функцию для каждого элемента; порядок результата сохраняется.
    /// </summary>
    /// <param name="callable">Вызываемое значение.</param>
    /// <param name="items">Элементы — по одному аргументу на вызов.</param>
    /// <param name="parallelism">Сколько ветвей одновременно; единица — последовательно.</param>
    /// <remarks>
    /// Параллелизм живёт здесь, а не в модуле: потоки, отмена, счётчики лимитов и потоки
    /// случайных чисел — забота рантайма. Модулю остаётся сказать, сколько ветвей он просит.
    /// </remarks>
    ValueTask<ScriptValue[]> CallEachAsync(ScriptValue callable, IReadOnlyList<ScriptValue> items, int parallelism);

    /// <summary>
    /// Сколько ветвей разрешено прогону: <c>options.parallel</c>.
    /// </summary>
    /// <remarks>
    /// Функция, у которой есть аргумент <c>parallel</c>, передаёт сюда это число при
    /// <c>true</c> и единицу при <c>false</c>: сколько именно потоков поднимать — решение
    /// прогона, а не отдельного вызова.
    /// </remarks>
    int Parallelism { get; }

    /// <summary>Ищет функцию по полному имени; <c>null</c>, если её нет.</summary>
    ScriptFunction? FindFunction(string fullName);

    /// <summary>Все зарегистрированные модули.</summary>
    IReadOnlyList<IScriptModule> Modules { get; }
}
