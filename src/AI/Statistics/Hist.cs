using AI.DataStructs.Algebraic;
using System;
using System.ComponentModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AI.Statistics;

/// <summary>
/// Структура гистограммы: X — позиции (левые края бинов), Y —
/// значения плотности. Поддерживает сериализацию через JSON
/// (безопасная замена устаревшему BinaryFormatter).
/// </summary>
[Serializable]
public class Histogramm
{
    private Vector _x;
    private Vector _y;
    private string _name = "Гистограмма";
    private string _description = "нет";
    private string _xLabel = "x";
    private string _yLabel = "P(x)";

    /// <summary>Пустая гистограмма (для десериализации).</summary>
    public Histogramm()
    {
    }

    /// <summary>Гистограмма с <paramref name="bins"/> бинами.</summary>
    public Histogramm(int bins)
    {
        _x = new Vector(bins);
        _y = new Vector(bins);
    }

    /// <summary>Позиции бинов.</summary>
    public Vector X { get => _x; set => _x = value; }

    /// <summary>Значения плотности по бинам.</summary>
    public Vector Y { get => _y; set => _y = value; }

    /// <summary>Название гистограммы.</summary>
    public string Name { get => _name; set => _name = value; }

    /// <summary>Описание гистограммы.</summary>
    public string Info { get => _description; set => _description = value; }

    /// <summary>Подпись оси X.</summary>
    public string XLabel { get => _xLabel; set => _xLabel = value; }

    /// <summary>Подпись оси Y.</summary>
    public string YLabel { get => _yLabel; set => _yLabel = value; }

    /// <summary>
    /// Старое имя с опечаткой. Оставлено для обратной совместимости —
    /// делегирует в <see cref="XLabel"/>.
    /// </summary>
    [Obsolete("Используйте XLabel.", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [JsonIgnore]
    public string XLable { get => XLabel; set => XLabel = value; }

    /// <summary>
    /// Старое имя с опечаткой. Оставлено для обратной совместимости —
    /// делегирует в <see cref="YLabel"/>.
    /// </summary>
    [Obsolete("Используйте YLabel.", false)]
    [EditorBrowsable(EditorBrowsableState.Never)]
    [JsonIgnore]
    public string YLables { get => YLabel; set => YLabel = value; }

    #region Сериализация

    private static readonly JsonSerializerOptions s_jsonOptions = new JsonSerializerOptions
    {
        WriteIndented = false,
        IncludeFields = false,
    };

    /// <summary>
    /// Сохраняет гистограмму в JSON. Потокобезопасно: каждый вызов
    /// открывает свой файловый поток.
    /// </summary>
    public void Save(string path)
    {
        try
        {
            var dto = new HistogrammDto
            {
                X = _x == null ? Array.Empty<double>() : _x.ToArray(),
                Y = _y == null ? Array.Empty<double>() : _y.ToArray(),
                Name = _name,
                Info = _description,
                XLabel = _xLabel,
                YLabel = _yLabel,
            };

            using FileStream fs = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
            JsonSerializer.Serialize(fs, dto, s_jsonOptions);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Не удалось сохранить гистограмму: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Загружает гистограмму из JSON. Поддерживает как новый формат,
    /// так и пустой файл (вернёт незаполненный экземпляр).
    /// </summary>
    public void Open(string path)
    {
        try
        {
            HistogrammDto dto;
            using (FileStream fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                dto = JsonSerializer.Deserialize<HistogrammDto>(fs, s_jsonOptions)
                      ?? new HistogrammDto();
            }

            _x = dto.X != null ? new Vector(dto.X) : new Vector();
            _y = dto.Y != null ? new Vector(dto.Y) : new Vector();
            _name = dto.Name ?? _name;
            _description = dto.Info ?? _description;
            _xLabel = dto.XLabel ?? _xLabel;
            _yLabel = dto.YLabel ?? _yLabel;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Не удалось загрузить гистограмму: {ex.Message}", ex);
        }
    }

    private sealed class HistogrammDto
    {
        public double[] X { get; set; } = Array.Empty<double>();
        public double[] Y { get; set; } = Array.Empty<double>();
        public string Name { get; set; }
        public string Info { get; set; }
        public string XLabel { get; set; }
        public string YLabel { get; set; }
    }

    #endregion

    #region Интегральные характеристики матриц

    // Эти методы — утилиты для обработки изображений/карт активации.
    // Оставлены как были, но объединены через один проход по матрице.

    /// <summary>Сумма значений по каждой строке.</summary>
    public static Vector IntegralValueH(Matrix matrix)
    {
        Vector v = new Vector(matrix.Height);
        for (int i = 0; i < matrix.Height; i++)
            for (int j = 0; j < matrix.Width; j++)
                v[i] += matrix[i, j];
        return v;
    }

    /// <summary>Среднее значение по каждой строке.</summary>
    public static Vector IntegralValueHMean(Matrix matrix)
        => IntegralValueH(matrix) / matrix.Width;

    /// <summary>Сумма значений по каждому столбцу.</summary>
    public static Vector IntegralValueW(Matrix matrix)
    {
        Vector v = new Vector(matrix.Width);
        for (int i = 0; i < matrix.Width; i++)
            for (int j = 0; j < matrix.Height; j++)
                v[i] += matrix[j, i];
        return v;
    }

    /// <summary>Среднее значение по каждому столбцу.</summary>
    public static Vector IntegralValueWMean(Matrix matrix)
        => IntegralValueW(matrix) / matrix.Height;

    /// <summary>Карта яркости через произведение (эквивалент «И»).</summary>
    public static Matrix HarAnd(Matrix matrix)
    {
        Vector v1 = IntegralValueHMean(1 - matrix).Minimax();
        Vector v2 = IntegralValueWMean(1 - matrix).Minimax();
        return 1 - Matrix.Mul2Vec(v1, v2);
    }

    /// <summary>Карта яркости через сумму (эквивалент «ИЛИ»).</summary>
    public static Matrix HarSumm(Matrix matrix)
    {
        Vector v1 = IntegralValueHMean(matrix).Minimax();
        Vector v2 = IntegralValueWMean(matrix).Minimax();
        return Matrix.Sum2Vec(v1, v2) / 2;
    }

    /// <summary>Карта яркости через норму.</summary>
    public static Matrix HarNorm(Matrix matrix)
    {
        Vector v1 = IntegralValueHMean(matrix).Minimax();
        Vector v2 = IntegralValueWMean(matrix).Minimax();
        return Matrix.Norm2Vec(v1, v2) / 2;
    }

    #endregion
}
