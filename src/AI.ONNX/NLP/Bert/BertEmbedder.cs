using AI.DataPrepaire.Backends.BertTokenizers;
using AI.DataPrepaire.DataLoader.NNWBlockLoader;
using AI.DataPrepaire.Tokenizers.TextTokenizers.HFTokenizers;
using AI.DataStructs.Algebraic;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace AI.ONNX.NLP.Bert;

/// <summary>
/// Эмбеддер последовательностей на базе Bert
/// </summary>
public class BertEmbedder
{
    public BertInfer BertInference { get; set; }
    public BertTokenizer Tokenizer { get; set; }
    public BertConfig Config { get; set; } = new BertConfig();
    public List<INNWBlockV2V> V2VBlocks { get; set; } = new List<INNWBlockV2V>();

    /// <summary>
    /// Очистка строки
    /// </summary>
    public Func<string, string> Cleaner { get; set; }

    /// <summary>
    /// Используется в методе ForwardSBert.
    /// Нарезать ли текст на блоки (увеличивает контекст и скорость, ухудшает качество)
    /// </summary>
    public bool IsCutting { get; set; } = true;

    /// <summary>
    /// Используется в методе ForwardSBert.
    /// Размер блока, при включенной нарезке (чем меньше блок, тем выше скорость, но хуже качество)
    /// </summary>
    public int BlockSize { get; set; } = 512;

    /// <summary>
    /// Очистка строки
    /// </summary>
    public static string CleanString(string text)
    {
        string seq = Regex.Replace(text, @"\r?\n", " ");
        seq = Regex.Replace(seq, @"[^A-zА-яЁё0-9\"": ]", " ");
        seq = Regex.Replace(seq, @"\s+", " ");
        return seq.Trim().ToLower();
    }

    /// <summary>
    /// Эмбеддер последовательностей на базе Bert
    /// </summary>
    /// <param name="tokenizer">Токенизатор</param>
    /// <param name="model">Модель</param>
    public BertEmbedder(BertTokenizer tokenizer, BertInfer model)
    {
        Cleaner = CleanString;
        BertInference = model;
        Tokenizer = tokenizer;
    }

    /// <summary>
    /// Эмбеддер последовательностей на базе Bert
    /// </summary>
    public BertEmbedder() { Cleaner = CleanString; }

    /// <summary>
    /// Прямой проход, преобразует всю последовательность (текст) в эмбеддинг
    /// </summary>
    /// <param name="text">Текст для векторизации</param>
    public Vector ForwardSBert(string text)
        => IsCutting ? ForwardSBertBlocks(text) : ForwardSBertWithoutBlocs(text);

    /// <summary>
    /// Поблочная векторизация текста с учетом контекста
    /// </summary>
    /// <param name="texts">Тексты (блоки)</param>
    /// <returns>Векторизованные представления блоков</returns>
    public Vector[] ForwardBlockPooling(IEnumerable<string> texts)
    {
        if (!texts.Any())
            return Array.Empty<Vector>();

        TokenizeResult[] tokenizeResults = BlockTokenize(texts.ToArray());
        TokenizeResult tokens = JoinTokens(tokenizeResults);
        var output = BertInference.Forward(tokens.InputIds, tokens.AttentionMask, tokens.TypeIds)[0];
        Vector[] embeddings = Vector2Vectors(output);
        List<Vector> results = new List<Vector>(tokenizeResults.Length);

        int indexInEmbeddings = 0;
        foreach (var tokenizeResult in tokenizeResults)
        {
            int blockLength = tokenizeResult.AttentionMask.Length;
            Vector blockVector = new Vector(Config.HiddenSize);

            for (int j = 0; j < blockLength; j++)
                blockVector += embeddings[indexInEmbeddings++];

            blockVector /= blockLength + AISettings.GlobalEps;
            blockVector = OutpTransform(blockVector);
            results.Add(blockVector);
        }

        return results.ToArray();
    }

    /// <summary>
    /// Векторизация текста с учетом весов блоков
    /// </summary>
    /// <param name="texts">Тексты (блоки)</param>
    /// <param name="blockWeights">Веса для каждого блока</param>
    /// <returns>Агрегированный вектор</returns>
    public Vector ForwardBlockPooling(IEnumerable<string> texts, IEnumerable<double> blockWeights)
    {
        if (!texts.Any() || !blockWeights.Any())
            throw new ArgumentException("Тексты и веса блоков не должны быть пустыми.");

        Vector[] vectors = ForwardBlockPooling(texts);
        if (vectors.Length != blockWeights.Count())
            throw new ArgumentException("Количество весов должно соответствовать количеству текстовых блоков.");

        Vector weights = new Vector(blockWeights.ToArray());
        weights /= weights.Sum();

        Vector output = new Vector(vectors[0].Count);

        for (int i = 0; i < vectors.Length; i++)
            output += weights[i] * vectors[i];

        return output;
    }

    /// <summary>
    /// Прямой проход, преобразует каждый токен в эмбеддинг
    /// </summary>
    public IEnumerable<Vector> ForwardBert(string text)
    {
        var tokens = Tokenizer.Encode(Cleaner(text));
        var output = BertInference.Forward(tokens.InputIds, tokens.AttentionMask, tokens.TypeIds)[0];
        return Vector2Vectors(output);
    }

    /// <summary>
    /// Загрузка пред. обученного эмбедера
    /// </summary>
    public static BertEmbedder FromPretrained(string pathToFolder)
    {
        BertTokenizer tokenizer = new BertTokenizer(Path.Combine(pathToFolder, "vocab.txt"));
        tokenizer.TokenizerConfig = BertTokenizerConfig.FromJson(Path.Combine(pathToFolder, "tokenizer_config.json"));
        BertInfer model = new BertInfer(Path.Combine(pathToFolder, "model.onnx"));
        BertEmbedder embedder = new BertEmbedder(tokenizer, model);
        embedder.Config = BertConfig.FromJson(Path.Combine(pathToFolder, "config.json"));
        return embedder;
    }

    private Vector ForwardSBertBlocks(string text)
    {
        if (text.Length <= BlockSize)
            return ForwardSBertWithoutBlocs(text);

        int nBlocs = text.Length / BlockSize;
        int mod = text.Length % BlockSize;

        Vector output = new Vector(Config.HiddenSize);
        int blockCount = 0;

        for (int i = 0; i < nBlocs; i++)
        {
            output += ForwardSBertWithoutBlocs(text.Substring(i * BlockSize, BlockSize));
            blockCount++;
        }

        if (mod != 0)
        {
            output += ForwardSBertWithoutBlocs(text.Substring(nBlocs * BlockSize, mod));
            blockCount++;
        }

        output /= blockCount;
        return output;
    }

    private Vector ForwardSBertWithoutBlocs(string text)
    {
        var outputBert = ForwardBert(text);
        var output = Vector.Mean(outputBert.ToArray());
        return OutpTransform(output);
    }

    private Vector OutpTransform(Vector output)
    {
        foreach (var block in V2VBlocks)
            output = block.Forward(output);

        return output;
    }

    private Vector[] Vector2Vectors(Vector outputBert)
    {
        int numVectors = outputBert.Count / Config.HiddenSize;
        Vector[] vectors = new Vector[numVectors];

        for (int i = 0; i < numVectors; i++)
        {
            vectors[i] = new Vector(Config.HiddenSize);
            int offset = i * Config.HiddenSize;

            for (int j = 0; j < Config.HiddenSize; j++)
                vectors[i][j] = outputBert[offset + j];
        }

        return vectors;
    }

    private TokenizeResult[] BlockTokenize(string[] texts)
    {
        TokenizeResult[] tokenizeResults = new TokenizeResult[texts.Length];

        for (int k = 0; k < texts.Length; k++)
        {
            string textWithSp = texts[k].Trim() + " ";
            tokenizeResults[k] = Tokenizer.Encode(Cleaner(textWithSp));

            bool isFirstBlock = k == 0;
            bool isLastBlock = k == tokenizeResults.Length - 1;

            if (tokenizeResults.Length > 1)
                AdjustTokenizeResult(tokenizeResults[k], isFirstBlock, isLastBlock);
        }

        return tokenizeResults;
    }

    private static void AdjustTokenizeResult(TokenizeResult result, bool isFirstBlock, bool isLastBlock)
    {
        int startOffset = !isFirstBlock ? 1 : 0;
        int newLength = !(isFirstBlock || isLastBlock) ? result.InputIds.Length - 2 : result.InputIds.Length - 1;

        int[] newAttentionMask = new int[newLength];
        int[] newInputIds = new int[newLength];
        int[] newTypeIds = new int[newLength];

        for (int i = 0; i < newLength; i++)
        {
            newAttentionMask[i] = result.AttentionMask[i + startOffset];
            newInputIds[i] = result.InputIds[i + startOffset];
            newTypeIds[i] = result.TypeIds[i + startOffset];
        }

        result.AttentionMask = newAttentionMask;
        result.InputIds = newInputIds;
        result.TypeIds = newTypeIds;
    }

    private static TokenizeResult JoinTokens(TokenizeResult[] tokenizeResults)
    {
        var totalLength = tokenizeResults.Sum(tr => tr.InputIds.Length);

        var joinedTokens = new TokenizeResult
        {
            AttentionMask = new int[totalLength],
            InputIds = new int[totalLength],
            TypeIds = new int[totalLength]
        };

        int currentPosition = 0;
        foreach (var tokenizeResult in tokenizeResults)
        {
            Array.Copy(tokenizeResult.InputIds, 0, joinedTokens.InputIds, currentPosition, tokenizeResult.InputIds.Length);
            Array.Copy(tokenizeResult.AttentionMask, 0, joinedTokens.AttentionMask, currentPosition, tokenizeResult.AttentionMask.Length);
            Array.Copy(tokenizeResult.TypeIds, 0, joinedTokens.TypeIds, currentPosition, tokenizeResult.TypeIds.Length);

            currentPosition += tokenizeResult.InputIds.Length;
        }

        return joinedTokens;
    }
}
