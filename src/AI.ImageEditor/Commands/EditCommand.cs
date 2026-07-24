namespace AI.ImageEditor.Commands;

/// <summary>Типы команд редактора (строки — чтобы протокол переживал версии клиента).</summary>
public static class EditCommandTypes
{
    /// <summary>Мазок кистью или ластиком.</summary>
    public const string Stroke = "stroke";
    /// <summary>Кадрирование документа.</summary>
    public const string Crop = "crop";
    /// <summary>Применение фильтра к слою.</summary>
    public const string Filter = "filter";
    /// <summary>Добавить слой.</summary>
    public const string LayerAdd = "layer.add";
    /// <summary>Удалить слой.</summary>
    public const string LayerRemove = "layer.remove";
    /// <summary>Переместить слой в стопке.</summary>
    public const string LayerMove = "layer.move";
    /// <summary>Изменить свойства слоя (видимость/непрозрачность/имя).</summary>
    public const string LayerProps = "layer.props";
    /// <summary>Выбрать активный слой.</summary>
    public const string LayerSelect = "layer.select";
}

/// <summary>
/// Одна команда редактирования — то, что реально уходит по сети.
/// <para>
/// Намеренно плоская запись без полиморфизма: сериализуется любым JSON-сериализатором
/// без настроек, а главное — <b>компактна</b>. Мазок передаётся как плоский массив
/// координат (x,y,x,y…), поэтому штрих из 50 точек весит сотни байт, а не мегабайты
/// пикселей. Это и есть основа работы на медленном канале.
/// </para>
/// </summary>
public sealed record EditCommand
{
    /// <summary>Тип команды (см. <see cref="EditCommandTypes"/>).</summary>
    public string Type { get; init; } = "";

    /// <summary>Целевой слой (если не задан — активный).</summary>
    public string? LayerId { get; init; }

    // ── Мазок ───────────────────────────────────────────────────────────────

    /// <summary>Координаты мазка подряд: x0,y0,x1,y1,…</summary>
    public float[]? Points { get; init; }

    /// <summary>Радиус кисти в пикселях документа.</summary>
    public float Radius { get; init; } = 8;

    /// <summary>Жёсткость края 0..1.</summary>
    public float Hardness { get; init; } = 1f;

    /// <summary>Непрозрачность мазка 0..1.</summary>
    public float Opacity { get; init; } = 1f;

    /// <summary>Цвет в формате 0xAARRGGBB.</summary>
    public uint Color { get; init; } = 0xFF000000;

    /// <summary>Режим ластика.</summary>
    public bool Erase { get; init; }

    // ── Кадрирование ────────────────────────────────────────────────────────

    /// <summary>Левая граница рамки.</summary>
    public int X { get; init; }
    /// <summary>Верхняя граница рамки.</summary>
    public int Y { get; init; }
    /// <summary>Ширина рамки.</summary>
    public int Width { get; init; }
    /// <summary>Высота рамки.</summary>
    public int Height { get; init; }

    // ── Фильтр ──────────────────────────────────────────────────────────────

    /// <summary>Идентификатор фильтра из <c>FilterRegistry</c>.</summary>
    public string? FilterId { get; init; }

    /// <summary>Параметры фильтра.</summary>
    public Dictionary<string, double>? Params { get; init; }

    // ── Слои ────────────────────────────────────────────────────────────────

    /// <summary>Новая позиция слоя (для перемещения).</summary>
    public int Index { get; init; }

    /// <summary>Имя слоя.</summary>
    public string? Name { get; init; }

    /// <summary>Видимость слоя.</summary>
    public bool? Visible { get; init; }

    /// <summary>Непрозрачность слоя 0..1.</summary>
    public double? LayerOpacity { get; init; }
}
