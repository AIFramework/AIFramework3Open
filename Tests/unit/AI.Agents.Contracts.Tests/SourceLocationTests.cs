using System.Text.Json;
using AI.Agents.Contracts.Citations;
using Xunit;

namespace AI.Agents.Contracts.Tests;

/// <summary>
/// Место в источнике: путь из сегментов, строки, время и область проверяются при создании и при чтении JSON.
/// </summary>
public sealed class SourceLocationTests
{
    /// <summary>Путь из 0, 1 и 2 сегментов проходит круг через JSON; пустой путь остается пустым, а не null.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void Path_OfZeroOneTwoSegments_SurvivesRoundTrip(int count)
    {
        LocationSegment[] segments =
        [
            new LocationSegment { Kind = LocationSegment.Kinds.Attachment, Id = "report.pptx" },
            new LocationSegment { Kind = LocationSegment.Kinds.Slide, Index = 3 },
        ];

        var location = new SourceLocation
        {
            Path = segments.Take(count).ToArray(),
            Rect = new RelativeRect { X = 0.5, Y = 0.5, Width = 0.5, Height = 0.5 },
        };

        SourceLocation back = RoundTrip(location);

        Assert.Empty(ValueComparison.Differences(location, back));
        Assert.Equal(count, back.Path!.Count);
    }

    /// <summary>Без пути место относится к самому документу: Path читается как null.</summary>
    [Fact]
    public void Path_Absent_ReadsAsNull()
    {
        SourceLocation back = JsonSerializer.Deserialize<SourceLocation>("""{"Page":7}""")!;

        Assert.Null(back.Path);
        Assert.Equal(7, back.Page);
    }

    /// <summary>Сегмент без Index и Id отвергается: в пути, при чтении сегмента и при чтении места.</summary>
    [Fact]
    public void Segment_WithoutIndexAndId_IsRejected()
    {
        var bare = new LocationSegment { Kind = LocationSegment.Kinds.Image };

        var inCode = Assert.Throws<ArgumentException>(() => new SourceLocation { Path = [bare] });
        Assert.Contains("Index", inCode.Message);
        Assert.Contains("Id", inCode.Message);

        var alone = Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<LocationSegment>("""{"Kind":"image"}"""));
        Assert.Contains("хотя бы одно", alone.Message);

        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<SourceLocation>("""{"Path":[{"Kind":"slide","Index":1},{"Kind":"image"}]}"""));
        Assert.Throws<ArgumentException>(() => new SourceLocation { Path = [null!] });
    }

    /// <summary>Сегмент принимает номер с 1, непустой идентификатор и непустой вид.</summary>
    [Fact]
    public void Segment_InvalidParts_AreRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LocationSegment { Kind = "slide", Index = 0 });
        Assert.Throws<ArgumentException>(() => new LocationSegment { Kind = "slide", Id = " " });
        Assert.Throws<ArgumentException>(() => new LocationSegment { Kind = "", Index = 1 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new SourceLocation { Page = 0 });
    }

    /// <summary>Известные виды сегментов — строки открытого набора.</summary>
    [Fact]
    public void Segment_KnownKinds_AreLowercaseStrings()
    {
        Assert.Equal(
            ["slide", "image", "sheet", "table", "page", "attachment"],
            [
                LocationSegment.Kinds.Slide, LocationSegment.Kinds.Image, LocationSegment.Kinds.Sheet,
                LocationSegment.Kinds.Table, LocationSegment.Kinds.Page, LocationSegment.Kinds.Attachment,
            ]);

        Assert.Equal("formula", RoundTrip(new LocationSegment { Kind = "formula", Index = 1 }).Kind);
    }

    /// <summary>Строки: конец раньше начала отвергается в любом порядке задания и при чтении; одна строка допустима.</summary>
    [Fact]
    public void Lines_EndBeforeStart_IsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineRange { Start = 5, End = 4 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineRange { End = 4, Start = 5 });
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineRange { Start = 0, End = 4 });
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<LineRange>("""{"Start":5,"End":4}"""));
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<LineRange>("""{"End":4,"Start":5}"""));
        Assert.Throws<ArgumentOutOfRangeException>(() => new LineRange { Start = 1, End = 3 } with { Start = 4 });

        Assert.Equal(5, new LineRange { Start = 5, End = 5 }.End);
    }

    /// <summary>Время: конец раньше начала и отрицательное время отвергаются; момент допустим.</summary>
    [Fact]
    public void Time_EndBeforeStart_IsRejected()
    {
        TimeSpan one = TimeSpan.FromSeconds(1);
        TimeSpan two = TimeSpan.FromSeconds(2);

        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeInterval { Start = two, End = one });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeInterval { End = one, Start = two });
        Assert.Throws<ArgumentOutOfRangeException>(() => new TimeInterval { Start = -one, End = one });
        Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<TimeInterval>("""{"Start":"00:00:02","End":"00:00:01"}"""));

        Assert.Equal(one, new TimeInterval { Start = one, End = one }.End);
    }

    /// <summary>Область вне 0..1 и область за краем отвергаются; во весь объект допустима.</summary>
    [Theory]
    [InlineData(-0.1, 0, 0.5, 0.5)]
    [InlineData(0, 1.1, 0.5, 0.5)]
    [InlineData(0, 0, 1.5, 0.5)]
    [InlineData(0, 0, 0.5, -0.5)]
    [InlineData(double.NaN, 0, 0.5, 0.5)]
    [InlineData(0.6, 0, 0.5, 0.5)]
    [InlineData(0, 0.6, 0.5, 0.5)]
    public void Rect_OutsideUnitSquare_IsRejected(double x, double y, double width, double height)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new RelativeRect { X = x, Y = y, Width = width, Height = height });
        Assert.Throws<ArgumentOutOfRangeException>(() => new RelativeRect { Width = width, Height = height, X = x, Y = y });

        if (!double.IsNaN(x))
        {
            string json = $$"""{"X":{{x.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"Y":{{y.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"Width":{{width.ToString(System.Globalization.CultureInfo.InvariantCulture)}},"Height":{{height.ToString(System.Globalization.CultureInfo.InvariantCulture)}}}""";
            Assert.ThrowsAny<Exception>(() => JsonSerializer.Deserialize<RelativeRect>(json));
        }
    }

    /// <summary>Края области допустимы, и округление double на краю не роняет проверку.</summary>
    [Fact]
    public void Rect_OnEdges_IsAccepted()
    {
        Assert.Equal(1, new RelativeRect { X = 0, Y = 0, Width = 1, Height = 1 }.Width);
        Assert.Equal(0.3, new RelativeRect { X = 0.7, Y = 0.1, Width = 0.3, Height = 0.9 }.Width);
    }

    /// <summary>Подтверждение несет место одним полем и проходит круг вместе с ним.</summary>
    [Fact]
    public void Citation_CarriesLocation()
    {
        var citation = new Citation
        {
            Kind = CitationKind.File,
            Title = "Отчет.pptx",
            Quote = "Выручка выросла на 12%",
            Location = ContractSamples.FullLocation(),
        };

        Citation back = RoundTrip(citation);

        Assert.Empty(ValueComparison.Differences(citation, back));
        Assert.Equal(LocationSegment.Kinds.Image, back.Location!.Path![^1].Kind);
    }

    private static T RoundTrip<T>(T value) =>
        JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
}
