using AI.Script.Binding;
using AI.Script.Hosting;
using AI.Script.Runtime;
using AI.Script.Semantics;

namespace AI.Script.Media;

/// <summary>Общее у медиа-функций: отказ без службы и учет внешнего вызова.</summary>
internal static class MediaCalls
{
    /// <summary>Служба или внятный отказ с тем же кодом, что у неподключенной модели.</summary>
    public static T Require<T>(T? service, string what, string reason) where T : class =>
        service ?? throw new ScriptError(
            DiagnosticCodes.UnknownFunction,
            $"{what}: {reason}",
            "служба не подключена хостом; в справке такие функции помечены «не подключено»");

    /// <summary>
    /// Вызов платной службы по правилам прогона: сеть, потолок вызовов до запроса, расход после.
    /// </summary>
    public static async Task<T> ExternalAsync<T>(
        IScriptContext context, string what, Func<CancellationToken, Task<T>> call, Func<T, (long Tokens, decimal Cost)> usage)
    {
        context.Network.Require(what);
        context.BeginExternalCall();

        T result = await call(context.Cancellation).ConfigureAwait(false);
        (long tokens, decimal cost) = usage(result);

        context.CountExternal(tokens, cost);

        return result;
    }

    /// <summary>Медиатип изображения по расширению файла.</summary>
    public static string ImageType(string path) => ScriptFileKinds.Extension(path) switch
    {
        "jpg" or "jpeg" => "image/jpeg",
        "webp" => "image/webp",
        "gif" => "image/gif",
        "bmp" => "image/bmp",
        _ => "image/png",
    };
}
