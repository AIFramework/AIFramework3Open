using AI.NLP.Stemmers;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Стеммер Портера: ожидания — выход эталонной реализации автора на тех же словах.
/// </summary>
public class StemmerEngTests
{
    [Theory]
    [InlineData("caresses", "caress")]
    [InlineData("ponies", "poni")]
    [InlineData("cats", "cat")]
    [InlineData("feed", "feed")]
    [InlineData("agreed", "agre")]
    [InlineData("plastered", "plaster")]
    [InlineData("motoring", "motor")]
    [InlineData("sing", "sing")]
    [InlineData("conflated", "conflat")]
    [InlineData("troubled", "troubl")]
    [InlineData("sized", "size")]
    [InlineData("hopping", "hop")]
    [InlineData("falling", "fall")]
    [InlineData("happy", "happi")]
    [InlineData("relational", "relat")]
    [InlineData("rational", "ration")]
    [InlineData("generalizations", "gener")]
    [InlineData("oscillators", "oscil")]
    [InlineData("hopeful", "hope")]
    [InlineData("goodness", "good")]
    [InlineData("adoption", "adopt")]
    [InlineData("controlling", "control")]
    public void MatchesReferenceImplementation(string word, string stem) =>
        Assert.Equal(stem, StemmerEng.TransformingWord(word));

    [Theory]
    [InlineData("Filter")]
    [InlineData("filters")]
    [InlineData("filtering")]
    [InlineData("filtered")]
    public void WordFormsShareOneStem(string word) =>
        Assert.Equal("filter", StemmerEng.TransformingWord(word));

    [Theory]
    [InlineData("фильтр", "фильтр")]
    [InlineData("x2", "x2")]
    [InlineData("is", "is")]
    [InlineData("", "")]
    public void NonLatinAndShortWordsStayLowercase(string word, string expected) =>
        Assert.Equal(expected, StemmerEng.TransformingWord(word));
}
