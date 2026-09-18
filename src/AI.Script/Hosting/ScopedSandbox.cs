namespace AI.Script.Hosting;

/// <summary>
/// Хранилище, суженное до папки внутри другого: так работает <c>options.workdir</c>.
/// </summary>
/// <remarks>
/// Не «сменить корень», а «углубиться»: путь из скрипта сначала нормализуется сам по себе
/// (<see cref="ScriptPaths.Normalize"/>), и только потом к нему приписывается папка. Поэтому
/// <c>..</c> не может увести выше папки, а сама папка не может увести выше корня — оба
/// отказа приходят из одного места. Работает над любым хранилищем, не только над диском.
/// </remarks>
public sealed class ScopedSandbox : IScriptSandbox
{
    private readonly IScriptSandbox _inner;
    private readonly string _prefix;

    /// <summary>Сужает хранилище до папки.</summary>
    /// <param name="inner">Исходное хранилище.</param>
    /// <param name="directory">Папка относительно его корня.</param>
    public ScopedSandbox(IScriptSandbox inner, string directory)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        _prefix = ScriptPaths.Normalize(directory);
    }

    /// <inheritdoc/>
    public bool Enabled => _inner.Enabled;

    /// <inheritdoc/>
    public string Root => _prefix == ScriptPaths.RootPath ? _inner.Root : $"{_inner.Root}/{_prefix}";

    /// <inheritdoc/>
    public bool IsReadOnly => _inner.IsReadOnly;

    /// <inheritdoc/>
    public async Task<IReadOnlyList<ScriptFileInfo>> ListAsync(string directory, string mask, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<ScriptFileInfo> files = await _inner.ListAsync(Inner(directory), mask, cancellationToken).ConfigureAwait(false);

        return [.. files.Select(Outer)];
    }

    /// <inheritdoc/>
    public async Task<ScriptFileInfo?> InfoAsync(string path, CancellationToken cancellationToken = default)
    {
        ScriptFileInfo? info = await _inner.InfoAsync(Inner(path), cancellationToken).ConfigureAwait(false);

        return info == null ? null : Outer(info);
    }

    /// <inheritdoc/>
    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(Inner(path), cancellationToken);

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(string path, CancellationToken cancellationToken = default) =>
        _inner.OpenReadAsync(Inner(path), cancellationToken);

    /// <inheritdoc/>
    public Task WriteAsync(string path, byte[] data, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(Inner(path), data, cancellationToken);

    private string Inner(string path)
    {
        string relative = ScriptPaths.Normalize(string.IsNullOrWhiteSpace(path) ? ScriptPaths.RootPath : path);

        if (_prefix == ScriptPaths.RootPath) return relative;

        return relative == ScriptPaths.RootPath ? _prefix : $"{_prefix}/{relative}";
    }

    private ScriptFileInfo Outer(ScriptFileInfo info)
    {
        string head = _prefix + "/";

        return _prefix == ScriptPaths.RootPath || !info.Path.StartsWith(head, StringComparison.OrdinalIgnoreCase)
            ? info
            : info with { Path = info.Path[head.Length..] };
    }
}
