using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>
/// Цветные изображения: цвет переживает фильтры и сохранение.
/// </summary>
/// <remarks>
/// Картинка собирается в самом скрипте из трёх каналов с известными значениями: так видно, что
/// каналы не перепутаны местами и не слились в серый по дороге.
/// </remarks>
public sealed class ColorImageTests
{
    /// <summary>Картинка 8×8: красный 200, зелёный 50, синий 10.</summary>
    private const string Orange = """
        let r = mat.zeros(8, cols: 8) + 200
        let g = mat.zeros(8, cols: 8) + 50
        let b = mat.zeros(8, cols: 8) + 10
        let img = cv.merge(r, g, b)
        """;

    private static RunResult Run(string source, MemorySandbox? store = null) =>
        Script.RunWith(Script.FullHost(), source, new RunOptions { Sandbox = store ?? new MemorySandbox() });

    private static RunResult RunOk(string source, MemorySandbox? store = null)
    {
        RunResult result = Run(source, store);

        Assert.True(result.Success, Script.Report(result));

        return result;
    }

    [Fact]
    public void Merge_ThenChannel_KeepsValues()
    {
        RunResult result = RunOk($"""
            {Orange}
            emit красный = cv.channel(img, "red")[0, 0]
            emit синий = cv.channel(img, "blue")[3, 3]
            """);

        Assert.Equal(200.0, result.Emitted["красный"]);
        Assert.Equal(10.0, result.Emitted["синий"]);
    }

    /// <summary>Файл и обратно: io.load отдаёт цветной снимок, а не серую матрицу.</summary>
    [Fact]
    public void SaveThenLoad_KeepsColor()
    {
        RunResult result = RunOk($"""
            {Orange}
            cv.save(img, "фото.png")

            let назад = io.load("фото.png")

            emit красный = cv.channel(назад, "red")[2, 2]
            emit зелёный = cv.channel(назад, "green")[2, 2]
            """);

        Assert.Equal(200.0, result.Emitted["красный"]);
        Assert.Equal(50.0, result.Emitted["зелёный"]);
    }

    /// <summary>Фильтр пикселей идёт по каналам: однородная картинка после размытия та же.</summary>
    [Fact]
    public void PixelFilter_WorksPerChannel()
    {
        RunResult result = RunOk($"""
            {Orange}
            let меньше = cv.resize(img, width: 4, height: 4)

            emit тип = type(меньше)
            emit строк = mat.rows(cv.channel(меньше, "red"))
            emit красный = cv.channel(меньше, "red")[1, 1]
            """);

        Assert.Equal("handle", result.Emitted["тип"]);
        Assert.Equal(4.0, result.Emitted["строк"]);
        Assert.Equal(200.0, (double)result.Emitted["красный"]!, 1);
    }

    /// <summary>Признак считается по яркости: гистограмме цветной снимок подходит как есть.</summary>
    [Fact]
    public void Feature_UsesBrightness()
    {
        RunResult result = RunOk($"""
            {Orange}
            emit столбцов = len(cv.histogram(img))
            emit контуры = mat.rows(cv.sobel(img).edges)
            """);

        Assert.Equal(256.0, result.Emitted["столбцов"]);
        Assert.Equal(8.0, result.Emitted["контуры"]);
    }

    /// <summary>Матрица яркостей по-прежнему работает везде, где работала.</summary>
    [Fact]
    public void Matrix_StillAccepted()
    {
        RunResult result = RunOk("""
            let m = mat.zeros(6, cols: 6) + 100

            emit тип = type(cv.blur(m))
            """);

        Assert.Equal("mat", result.Emitted["тип"]);
    }

    [Fact]
    public void Merge_DifferentSizes_IsRefused()
    {
        RunResult result = Run("emit r = cv.merge(mat.zeros(2, cols: 2), mat.zeros(3, cols: 3), mat.zeros(2, cols: 2))");

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.SizeMismatch, result.Error!.Code);
    }

    [Fact]
    public void Channel_Unknown_ListsKnown()
    {
        RunResult result = Run($"{Orange}\nemit r = cv.channel(img, \"alpha\")");

        Assert.False(result.Success);
        Assert.Contains("red", result.Error!.Hint, StringComparison.Ordinal);
    }

    /// <summary>Снимок вставляется в документ как есть: изображение умеет нарисовать себя.</summary>
    [Fact]
    public void Image_GoesIntoDocument()
    {
        var store = new MemorySandbox();

        RunOk($"""
            {Orange}
            doc.new(title: "Снимок") |> doc.figure(img, caption: "оранжевый") |> doc.save("снимок.docx")
            """, store);

        Assert.NotNull(store.Get("снимок.docx"));
    }

    // --- найденное при проверке ---

    /// <summary>Увеличение считается по размеру результата: потолок памяти срабатывает до растяжения.</summary>
    [Fact]
    public void Resize_CountsTargetSize()
    {
        var options = new RunOptions { Sandbox = new MemorySandbox() };

        options.Limits.Allocations = 100_000;

        RunResult result = Script.RunWith(Script.FullHost(), "emit m = mat.rows(cv.resize(mat.zeros(10, cols: 10), width: 3000, height: 3000))", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.MemoryLimit, result.Error!.Code);
    }
}
