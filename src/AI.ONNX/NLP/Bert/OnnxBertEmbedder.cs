using AI.DataStructs.Algebraic;
using AI.LLM.Services.Embeddings.Base;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI.ONNX.NLP.Bert;

/// <summary>
/// Эмбеддер для поиска по смыслу без сети и без сервера: модель BERT в ONNX считается прямо в процессе
/// (<see cref="BertEmbedder"/>), а снаружи он выглядит как любой эмбеддер AI.LLM. Векторы приводятся к
/// единичной длине — косинус становится скалярным произведением.
/// </summary>
/// <remarks>
/// Папка модели — как у <see cref="BertEmbedder.FromPretrained"/>: <c>model.onnx</c>, <c>vocab.txt</c>,
/// <c>config.json</c> и <c>tokenizer_config.json</c>. Токенизатор — WordPiece, поэтому подходят модели семейства
/// BERT (rubert-tiny2, all-MiniLM-L6-v2); модели на SentencePiece (e5, bge-m3) им не прочесть.
/// </remarks>
public sealed class OnnxBertEmbedder : EmbedderServiceBase, IDisposable
{
    /// <summary>Файлы, без которых папка — не модель.</summary>
    public static readonly string[] RequiredFiles = ["model.onnx", "vocab.txt", "config.json", "tokenizer_config.json"];

    private readonly BertEmbedder _embedder;

    /// <summary>Длина вектора модели.</summary>
    public int Dimension => _embedder.Config.HiddenSize;

    /// <summary>Загружает модель из папки.</summary>
    /// <param name="modelFolder">Папка с файлами из <see cref="RequiredFiles"/>.</param>
    /// <exception cref="FileNotFoundException">Не хватает файла модели.</exception>
    public OnnxBertEmbedder(string modelFolder)
    {
        string missing = RequiredFiles.FirstOrDefault(f => !File.Exists(Path.Combine(modelFolder ?? "", f)));
        if (missing != null)
            throw new FileNotFoundException($"В папке модели нет {missing}", Path.Combine(modelFolder ?? "", missing));

        _embedder = BertEmbedder.FromPretrained(modelFolder);
        // Блок — сколько влезает в окно модели (не меньше двух знаков на токен): усреднение многих блоков размывает смысл.
        _embedder.BlockSize = Math.Max(_embedder.BlockSize, _embedder.Config.MaxPositionEmbeddings * 2);
        ModelName = Path.GetFileName(Path.TrimEndingDirectorySeparator(modelFolder));
    }

    /// <summary>Есть ли в папке всё, что нужно модели.</summary>
    public static bool IsModelFolder(string folder) =>
        !string.IsNullOrWhiteSpace(folder) && RequiredFiles.All(f => File.Exists(Path.Combine(folder, f)));

    /// <inheritdoc/>
    public override Task<Vector[]> EncodeAsync(IEnumerable<string> texts, CancellationToken cancellationToken = default)
    {
        string[] items = texts?.ToArray() ?? throw new ArgumentNullException(nameof(texts));
        return Task.Run(() => items.Select(text =>
        {
            cancellationToken.ThrowIfCancellationRequested();
            return _embedder.ForwardSBert(text ?? "").GetUnitVector();
        }).ToArray(), cancellationToken);
    }

    /// <summary>Освобождает сессию ONNX Runtime.</summary>
    public void Dispose() => _embedder.BertInference?.Dispose();
}
