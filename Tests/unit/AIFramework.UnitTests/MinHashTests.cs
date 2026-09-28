using AI.NLP.Similarity;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// MinHash оценивает коэффициент Жаккара, а LSH находит похожие пары без перебора всех пар.
/// </summary>
public class MinHashTests
{
    private static HashSet<ulong> Set(int from, int to) => [.. Enumerable.Range(from, to - from).Select(i => MinHash.Hash("элемент " + i))];

    [Fact]
    public void IdenticalSetsHaveSimilarityOne()
    {
        var minHash = new MinHash();

        Assert.Equal(1.0, MinHash.Similarity(minHash.Signature(Set(0, 50)), minHash.Signature(Set(0, 50))));
    }

    [Theory]
    [InlineData(0, 100, 50, 150)]   // Жаккар 1/3
    [InlineData(0, 100, 20, 100)]   // 0.8
    [InlineData(0, 100, 100, 200)]  // 0
    public void EstimateIsCloseToExactJaccard(int a0, int a1, int b0, int b1)
    {
        var minHash = new MinHash(size: 256);
        HashSet<ulong> a = Set(a0, a1), b = Set(b0, b1);

        double exact = MinHash.Jaccard(a, b);
        double estimate = MinHash.Similarity(minHash.Signature(a), minHash.Signature(b));

        Assert.InRange(estimate, exact - 0.1, exact + 0.1);
    }

    [Fact]
    public void LshPairsSimilarSetsAndSkipsDissimilar()
    {
        var minHash = new MinHash();
        var lsh = new MinHashLsh(bands: 32, rows: 4);

        lsh.Add(0, minHash.Signature(Set(0, 100)));
        lsh.Add(1, minHash.Signature(Set(10, 110)));   // Жаккар 0.82 с первым
        lsh.Add(2, minHash.Signature(Set(1000, 1100))); // ни с кем

        IReadOnlyCollection<(int A, int B)> pairs = lsh.CandidatePairs();

        Assert.Contains((0, 1), pairs);
        Assert.DoesNotContain(pairs, pair => pair.A == 2 || pair.B == 2);
        Assert.Contains(1, lsh.Query(minHash.Signature(Set(5, 105))));
        Assert.InRange(lsh.Threshold, 0.4, 0.45);
    }

    [Fact]
    public void HashCombinesOrderSensitively()
    {
        ulong a = MinHash.Hash("for"), b = MinHash.Hash("ID");

        Assert.NotEqual(MinHash.Combine([a, b]), MinHash.Combine([b, a]));
        Assert.Equal(MinHash.Combine([a, b]), MinHash.Combine([a, b]));
    }

    [Fact]
    public void MismatchedSignaturesAreRejected()
    {
        Assert.Throws<ArgumentException>(() => MinHash.Similarity(new ulong[4], new ulong[5]));
        Assert.Throws<ArgumentException>(() => new MinHashLsh(32, 4).Add(0, new ulong[10]));
    }
}
