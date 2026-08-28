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
    private static long s_counter;

    /// <summary>Тип-тег вида <c>ml.kmeans</c>; по нему разрешаются методы дескриптора.</summary>
    public string TypeName { get; }

    /// <summary>Объект фреймворка.</summary>
    public object Target { get; }

    /// <summary>Короткое описание для печати; может быть <c>null</c>.</summary>
    public string? Summary { get; set; }

    /// <summary>Номер дескриптора в пределах процесса; попадает в печать.</summary>
    public long Id { get; }

    /// <summary>Создаёт дескриптор.</summary>
    public ScriptHandle(string typeName, object target, string? summary = null)
    {
        TypeName = typeName ?? throw new ArgumentNullException(nameof(typeName));
        Target = target ?? throw new ArgumentNullException(nameof(target));
        Summary = summary;
        Id = Interlocked.Increment(ref s_counter);
    }

    /// <inheritdoc/>
    public override string ToString() =>
        Summary == null ? $"<{TypeName} #{Id}>" : $"<{TypeName}: {Summary}>";
}
