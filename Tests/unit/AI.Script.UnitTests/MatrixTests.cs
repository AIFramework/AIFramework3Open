using AI.DataStructs.Algebraic;
using AI.Script.Hosting;
using AI.Script.Semantics;

namespace AI.Script.UnitTests;

/// <summary>Матрицы: индексация, срезы, арифметика, разложения.</summary>
public sealed class MatrixTests
{
    private const string M = "let m = mat.of([<1, 2, 3>, <4, 5, 6>])\n";

    [Theory]
    [InlineData("mat.rows(m)", 2)]
    [InlineData("mat.cols(m)", 3)]
    [InlineData("m[0, 0]", 1)]
    [InlineData("m[1, 2]", 6)]
    [InlineData("m[-1, -1]", 6)]
    [InlineData("len(m[0, :])", 3)]
    [InlineData("len(m[:, 0])", 2)]
    [InlineData("m[:, 1][1]", 5)]
    [InlineData("mat.rows(m[0..1, :])", 1)]
    [InlineData("mat.cols(m[:, 1..3])", 2)]
    [InlineData("mat.rows(m[0..2, 0..2])", 2)]
    [InlineData("len(m[0])", 3)]
    public void Matrix_Indexing(string expression, double expected) =>
        Assert.Equal(expected, Script.Number(expression, prelude: M), 9);

    [Fact]
    public void Matrix_SliceKeepsMatrixType()
    {
        Assert.Equal("mat", Script.Text("type(m[0..1, :])", prelude: M));
        Assert.Equal("vec", Script.Text("type(m[0, :])", prelude: M));
        Assert.Equal("num", Script.Text("type(m[0, 0])", prelude: M));
    }

    [Fact]
    public void Matrix_OutOfRange_IsReported()
    {
        Assert.Equal(DiagnosticCodes.IndexOutOfRange, Script.FailsWith(M + "emit r = m[5, 0]").Code);
    }

    [Fact]
    public void Matrix_ScalarArithmetic()
    {
        Assert.Equal(42.0, Script.Number("mat.sum(m * 2)[0] + mat.sum(m * 2)[1] + mat.sum(m * 2)[2]", prelude: M), 9);
    }

    [Fact]
    public void Matrix_ElementwiseAddition()
    {
        Assert.Equal(2.0, Script.Number("(m + m)[0, 0]", prelude: M), 9);
    }

    /// <summary>
    /// Для матриц <c>*</c> — матричное умножение, для векторов — поэлементное.
    /// Различие намеренное (DESIGN.md §7.1) и потому проверяется явно.
    /// </summary>
    [Fact]
    public void Matrix_StarIsMatrixProduct_VectorStarIsElementwise()
    {
        const string source = """
            let a = mat.of([<1, 2>, <3, 4>])
            let b = mat.of([<1, 0>, <0, 1>])
            emit product = (a * b)[1, 0]
            emit hadamard = mat.hadamard(a, a)[1, 0]
            emit vectors = (<2, 3> * <4, 5>)[0]
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(3.0, result.Emitted["product"]);
        Assert.Equal(9.0, result.Emitted["hadamard"]);
        Assert.Equal(8.0, result.Emitted["vectors"]);
    }

    [Fact]
    public void Matrix_IncompatibleProduct_IsReported()
    {
        const string source = """
            let a = mat.of([<1, 2, 3>])
            let b = mat.of([<1, 2, 3>])
            emit r = a * b
            """;

        Diagnostic error = Script.FailsWith(source);

        Assert.Equal(DiagnosticCodes.SizeMismatch, error.Code);
        Assert.Contains("hadamard", error.Hint, StringComparison.Ordinal);
    }

    [Fact]
    public void Matrix_TimesVector()
    {
        const string source = """
            let a = mat.of([<1, 2>, <3, 4>])
            emit r = (a * <1, 1>)[1]
            """;

        Assert.Equal(7.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Matrix_Transpose()
    {
        Assert.Equal(3.0, Script.Number("mat.rows(mat.transpose(m))", prelude: M), 9);
    }

    [Fact]
    public void Matrix_EyeAndDeterminant()
    {
        Assert.Equal(1.0, Script.Number("mat.det(mat.eye(4))"), 9);
    }

    [Fact]
    public void Matrix_Determinant_RequiresSquare()
    {
        Assert.Equal(DiagnosticCodes.SizeMismatch, Script.FailsWith(M + "emit r = mat.det(m)").Code);
    }

    [Fact]
    public void Matrix_SolveLinearSystem()
    {
        const string source = """
            let a = mat.of([<2, 1>, <1, 3>])
            let x = mat.solve(a, <5, 10>)
            emit x0 = core.round(x[0], digits: 6)
            emit x1 = core.round(x[1], digits: 6)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(1.0, result.Emitted["x0"]);
        Assert.Equal(3.0, result.Emitted["x1"]);
    }

    [Fact]
    public void Matrix_Inverse()
    {
        const string source = """
            let a = mat.of([<4, 7>, <2, 6>])
            let i = a * mat.inv(a)
            emit r = core.round(i[0, 0], digits: 6)
            """;

        Assert.Equal(1.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Matrix_Svd_ReconstructsSingularValues()
    {
        const string source = """
            let a = mat.of([<3, 0>, <0, 4>])
            let s = mat.svd(a)
            emit largest = core.round(stat.max(s.sigma), digits: 6)
            """;

        Assert.Equal(4.0, Script.RunOk(source).Emitted["largest"]);
    }

    [Fact]
    public void Matrix_Eig_OnSymmetric()
    {
        const string source = """
            let a = mat.of([<2, 0>, <0, 5>])
            emit r = core.round(stat.max(mat.eig(a).values), digits: 6)
            """;

        Assert.Equal(5.0, Script.RunOk(source).Emitted["r"]);
    }

    [Fact]
    public void Matrix_QrAndCholesky()
    {
        const string source = """
            let a = mat.of([<4, 2>, <2, 3>])
            emit q = mat.rows(mat.qr(a).q)
            emit l = core.round(mat.cholesky(a)[0, 0], digits: 6)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(2.0, result.Emitted["q"]);
        Assert.Equal(2.0, result.Emitted["l"]);
    }

    [Fact]
    public void Matrix_ColumnStatistics()
    {
        const string source = """
            let m = mat.of([<1, 10>, <3, 30>])
            emit mean = mat.mean(m)[1]
            emit min = mat.min(m)[0]
            emit max = mat.max(m)[1]
            emit sum = mat.sum(m)[0]
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(20.0, result.Emitted["mean"]);
        Assert.Equal(1.0, result.Emitted["min"]);
        Assert.Equal(30.0, result.Emitted["max"]);
        Assert.Equal(4.0, result.Emitted["sum"]);
    }

    [Fact]
    public void Matrix_ZScore_NormalisesEachColumnSeparately()
    {
        // По столбцам, а не по всей матрице: столбец — это признак со своей шкалой.
        const string source = """
            let m = mat.of([<1, 100>, <3, 300>])
            let z = mat.zscore(m)
            emit a = core.round(mat.mean(z)[0], digits: 9)
            emit b = core.round(mat.mean(z)[1], digits: 9)
            """;

        RunResult result = Script.RunOk(source);

        Assert.Equal(0.0, result.Emitted["a"]);
        Assert.Equal(0.0, result.Emitted["b"]);
    }

    [Fact]
    public void Matrix_MapAppliesFunction()
    {
        Assert.Equal(4.0, Script.Number("(m |> mat.map(x => x * 2))[0, 1]", prelude: M), 9);
    }

    [Fact]
    public void Matrix_ConcatRowsAndCols()
    {
        Assert.Equal(4.0, Script.Number("mat.rows(mat.concat_rows(m, m))", prelude: M), 9);
        Assert.Equal(6.0, Script.Number("mat.cols(mat.concat_cols(m, m))", prelude: M), 9);
    }

    [Fact]
    public void Matrix_AllocationLimit_Applies()
    {
        var options = new RunOptions();
        options.Limits.Allocations = 100;

        RunResult result = Script.Run("emit r = mat.zeros(10000, cols: 10000)", options);

        Assert.False(result.Success);
        Assert.Equal(DiagnosticCodes.MemoryLimit, result.Error!.Code);
    }

    [Fact]
    public void Matrix_ReturnsToHostAsFrameworkType()
    {
        var matrix = Assert.IsType<Matrix>(Script.Eval("mat.eye(3)"));

        Assert.Equal(3, matrix.Height);
        Assert.Equal(3, matrix.Width);
    }
}
