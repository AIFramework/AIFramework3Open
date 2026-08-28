namespace AI.Script.Hosting;

/// <summary>
/// Объект, который умеет представить себя артефактом прогона.
/// </summary>
/// <remarks>
/// Контракт объявлен в ядре, а реализуют его модули: иначе ядру пришлось бы знать про
/// графики, изображения и всё остальное, что кто-нибудь захочет показать. Здесь ядро знает
/// только «есть вид, есть текст, есть содержимое» — и этого хватает, чтобы хост нарисовал
/// то, чего ядро в глаза не видело.
/// </remarks>
public interface IScriptArtifactSource
{
    /// <summary>Вид артефакта: <c>plot</c>, <c>image</c>, <c>table</c>.</summary>
    string ArtifactKind { get; }

    /// <summary>Заголовок; может быть пустым.</summary>
    string ArtifactTitle { get; }

    /// <summary>Содержимое артефакта в том виде, в каком его ждёт хост.</summary>
    object? ArtifactPayload { get; }
}
