using AI.DataStructs.Algebraic;
using AI.LLM.Core.Abstractions;
using AI.LLM.Services.Embeddings.Base;
using System.Security.Cryptography;
using System.Text;

namespace AI.LLM.Services.Embeddings.Caching;

/// <summary>
/// Эмбеддер с кэшем на диске: вектор одного и того же текста запрашивается у модели один раз.
/// </summary>
/// <remarks>
/// <para>
/// Ключ — отпечаток текста (SHA-256), а не его место в корпусе: переименованный файл,
/// переехавший метод или повторный прогон индексации не стоят ни одного запроса к модели.
/// Вопросы и документы хранятся под разными ключами: у многих моделей вопрос кодируется с
/// инструкцией, и вектор одного и того же текста в двух ролях разный.
/// </para>
/// <para>
/// Файл — журнал записей «ключ + вектор» за заголовком с именем модели и размерностью.
/// Новые векторы дописываются в конец, поэтому сбой посреди записи теряет только последнюю
/// неполную запись. Файл другой модели не открывается: смешанные векторы разных моделей
/// дают бессмысленные косинусы без единой ошибки.
/// </para>
/// </remarks>
public sealed class CachedEmbedder : EmbedderServiceBase, IDisposable
{
    private const int Magic = 0x43454941; // "AIEC"
    private const int FormatVersion = 1;

    private readonly IEmbedderService _inner;
    private readonly Dictionary<(ulong, ulong), float[]> _vectors = new();
    private readonly FileStream _file;
    private readonly object _gate = new();
    private int _dimension;

    /// <summary>Создаёт кэширующую обёртку.</summary>
    /// <param name="inner">Эмбеддер, к которому идут запросы при промахе.</param>
    /// <param name="cachePath">Файл кэша; создаётся, если его нет.</param>
    /// <exception cref="InvalidOperationException">Файл принадлежит другой модели или повреждён заголовок.</exception>
    public CachedEmbedder(IEmbedderService inner, string cachePath)
    {
        _inner = inner ?? throw new ArgumentNullException(nameof(inner));
        if (string.IsNullOrWhiteSpace(cachePath)) throw new ArgumentNullException(nameof(cachePath));

        ModelName = (inner as EmbedderServiceBase)?.ModelName ?? inner.GetType().Name;

        string folder = Path.GetDirectoryName(Path.GetFullPath(cachePath));
        if (folder != null) Directory.CreateDirectory(folder);

        _file = new FileStream(cachePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);

        try
        {
            Load();
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    /// <summary>Сколько векторов в кэше.</summary>
    public int Count
    {
        get { lock (_gate) return _vectors.Count; }
    }

    /// <summary>Сколько текстов ушло в модель за время жизни обёртки.</summary>
    public int Misses { get; private set; }

    /// <inheritdoc/>
    public override async Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        string[] items = texts?.ToArray() ?? throw new ArgumentNullException(nameof(texts));

        string[] missing;
        lock (_gate) missing = items.Where(text => !_vectors.ContainsKey(Key("d", text))).Distinct().ToArray();

        if (missing.Length > 0) Store("d", missing, await _inner.EncodeAsync(missing, cancellationToken));

        lock (_gate) return items.Select(text => ToVector(_vectors[Key("d", text)])).ToArray();
    }

    /// <inheritdoc/>
    public override async Task<Vector> EncodeQuestionAsync(string question, CancellationToken cancellationToken = default)
    {
        (ulong, ulong) key = Key("q", question);

        lock (_gate)
        {
            if (_vectors.TryGetValue(key, out float[] cached)) return ToVector(cached);
        }

        Store("q", [question], [await _inner.EncodeQuestionAsync(question, cancellationToken)]);

        lock (_gate) return ToVector(_vectors[key]);
    }

    /// <inheritdoc/>
    public override string GetDetailedInstruct(string question) =>
        (_inner as EmbedderServiceBase)?.GetDetailedInstruct(question) ?? question;

    /// <inheritdoc/>
    public override double TanhCosineNormalize(double cosine) => _inner.TanhCosineNormalize(cosine);

    /// <inheritdoc/>
    public void Dispose() => _file.Dispose();

    private void Store(string role, string[] texts, Vector[] vectors)
    {
        if (vectors == null || vectors.Length != texts.Length)
            throw new InvalidOperationException($"Модель вернула {vectors?.Length ?? 0} векторов на {texts.Length} текстов.");

        lock (_gate)
        {
            using var writer = new BinaryWriter(_file, Encoding.UTF8, leaveOpen: true);
            _file.Seek(0, SeekOrigin.End);

            if (_dimension == 0)
            {
                _dimension = vectors[0].Count;
                _file.Seek(0, SeekOrigin.Begin);
                WriteHeader(writer);
            }

            for (int i = 0; i < texts.Length; i++)
            {
                if (vectors[i].Count != _dimension)
                    throw new InvalidOperationException($"Размерность вектора {vectors[i].Count}, в кэше {_dimension}.");

                (ulong high, ulong low) = Key(role, texts[i]);
                float[] values = vectors[i].Select(value => (float)value).ToArray();

                writer.Write(high);
                writer.Write(low);
                foreach (float value in values) writer.Write(value);

                _vectors[(high, low)] = values;
            }

            writer.Flush();
            Misses += texts.Length;
        }
    }

    private void Load()
    {
        if (_file.Length == 0) return;

        using var reader = new BinaryReader(_file, Encoding.UTF8, leaveOpen: true);

        if (reader.ReadInt32() != Magic || reader.ReadInt32() != FormatVersion)
            throw new InvalidOperationException("Файл не является кэшем эмбеддингов или записан другой версией.");

        string model = reader.ReadString();
        if (model != ModelName)
            throw new InvalidOperationException($"Кэш записан моделью «{model}», а эмбеддер — «{ModelName}». Возьмите другой файл кэша.");

        _dimension = reader.ReadInt32();
        long record = 16 + 4L * _dimension;

        while (_file.Length - _file.Position >= record)
        {
            ulong high = reader.ReadUInt64();
            ulong low = reader.ReadUInt64();
            var values = new float[_dimension];
            for (int i = 0; i < values.Length; i++) values[i] = reader.ReadSingle();
            _vectors[(high, low)] = values;
        }

        // Хвост неполной записи — след сбоя посреди дописывания: следующая запись ляжет поверх.
        _file.SetLength(_file.Position);
    }

    private void WriteHeader(BinaryWriter writer)
    {
        writer.Write(Magic);
        writer.Write(FormatVersion);
        writer.Write(ModelName);
        writer.Write(_dimension);
        _file.Seek(0, SeekOrigin.End);
    }

    private static (ulong, ulong) Key(string role, string text)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(role + "\n" + text));
        return (BitConverter.ToUInt64(hash, 0), BitConverter.ToUInt64(hash, 8));
    }

    private static Vector ToVector(float[] values) => new(values);
}
