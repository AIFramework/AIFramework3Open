namespace AI.Script.Runtime;

/// <summary>
/// Непрозрачная ссылка на объект фреймворка: обученную модель, токенизатор, индекс, сессию.
/// </summary>
/// <remarks>
/// Дескриптор — единственное место, где в языке живёт изменяемое состояние: все остальные
/// значения неизменяемы. Владелец дескриптора — прогон, а не скрипт: освобождение
/// <see cref="IDisposable"/> гарантирует хост даже при срыве, иначе сорвавшийся скрипт
/// оставлял бы за собой открытые файлы и сессии.
/// </remarks>
public sealed class ScriptHandle
{
    /// <summary>Тип-тег значения, которого еще нет: шаг только запланирован (<see cref="Deferred"/>).</summary>
    public const string DeferredType = "deferred";

    private static long s_counter;

    /// <summary>Тип-тег вида <c>ml.kmeans</c>; по нему разрешаются методы дескриптора.</summary>
    public string TypeName { get; }

    /// <summary>Объект фреймворка.</summary>
    public object Target { get; }

    /// <summary>Короткое описание для печати; может быть <c>null</c>.</summary>
    public string? Summary { get; set; }

    /// <summary>Номер дескриптора в пределах процесса; попадает в печать.</summary>
    public long Id { get; }

    /// <summary>Значение, которого еще нет; чтение его поля прерывает прогон, и <c>try</c> это не перехватит.</summary>
    public bool IsDeferred => TypeName == DeferredType;

    /// <summary>Создаёт дескриптор.</summary>
    public ScriptHandle(string typeName, object target, string? summary = null)
    {
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Summary = summary;
        Id = Interlocked.Increment(ref s_counter);
    }

    /// <summary>
    /// Значение шага, который только запланирован: хост показывает план человеку до исполнения.
    /// </summary>
    /// <remarks>
    /// Скрипт, которому нужен итог такого шага, дальше не предсказать: хост останавливает показ
    /// плана на этом месте, исполняет показанное и показывает остаток отдельно.
    /// </remarks>
    public static ScriptValue Deferred(object step) => ScriptValue.Handle(new ScriptHandle(DeferredType, step, "станет известно после исполнения"));

    /// <inheritdoc/>
    public override string ToString() =>
        Summary == null ? $"<{TypeName} #{Id}>" : $"<{TypeName}: {Summary}>";
}
