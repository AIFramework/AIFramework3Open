using AI.Solvers.Chem.Structures;

namespace AI.Solvers.Chem.UnitTests;

// Тесты лежат в пространстве AI.Solvers.*, где простое имя Math разрешается
// в соседнее пространство AI.Solvers.Math, а не в системный класс
using Math = System.Math;

/// <summary>Геометрия структур: векторы, ячейка, симметрия, форматы файлов.</summary>
public class StructureTests
{
    private const double NaClEdge = 5.6402;

    /// <summary>Векторное произведение ортов даёт третий орт.</summary>
    [Fact]
    public void Vector3_CrossProductFollowsRightHandRule()
    {
        var x = new Vector3(1, 0, 0);
        var y = new Vector3(0, 1, 0);

        Vector3 z = x.Cross(y);

        Assert.Equal(0, z.X, 12);
        Assert.Equal(0, z.Y, 12);
        Assert.Equal(1, z.Z, 12);
        Assert.Equal(90, x.AngleTo(y), 10);
        Assert.Equal(0, x.Dot(y), 12);
    }

    /// <summary>Длина, нормировка и расстояние согласованы между собой.</summary>
    [Fact]
    public void Vector3_LengthAndNormalization()
    {
        var v = new Vector3(3, 4, 12);

        Assert.Equal(13, v.Length, 12);
        Assert.Equal(1, v.Normalized.Length, 12);
        Assert.Equal(13, v.DistanceTo(Vector3.Zero), 12);
    }

    /// <summary>Объём кубической ячейки равен кубу ребра.</summary>
    [Fact]
    public void UnitCell_CubicVolume()
    {
        UnitCell cell = UnitCell.Cubic(5);

        Assert.Equal(125, cell.Volume, 9);
        Assert.Equal(CrystalSystem.Cubic, cell.System);
    }

    /// <summary>Объём гексагональной ячейки равен a^2·c·sin(120).</summary>
    [Fact]
    public void UnitCell_HexagonalVolume()
    {
        UnitCell cell = UnitCell.Hexagonal(3.0, 5.0);
        double expected = 3.0 * 3.0 * 5.0 * Math.Sin(120 * Math.PI / 180);

        Assert.Equal(expected, cell.Volume, 9);
        Assert.Equal(CrystalSystem.Hexagonal, cell.System);
    }

    /// <summary>Межплоскостные расстояния кубической решётки: d = a/sqrt(h^2+k^2+l^2).</summary>
    [Theory]
    [InlineData(1, 0, 0, 1)]
    [InlineData(1, 1, 0, 2)]
    [InlineData(1, 1, 1, 3)]
    [InlineData(3, 1, 1, 11)]
    public void UnitCell_InterplanarSpacingOfCubicLattice(int h, int k, int l, int squaredSum)
    {
        UnitCell cell = UnitCell.Cubic(NaClEdge);

        Assert.Equal(NaClEdge / Math.Sqrt(squaredSum), cell.InterplanarSpacing(h, k, l), 9);
    }

    /// <summary>Отражение 200 хлорида натрия на медном излучении лежит около 31.7 градуса.</summary>
    [Fact]
    public void UnitCell_BraggAngleOfSodiumChloride()
    {
        UnitCell cell = UnitCell.Cubic(NaClEdge);

        Assert.Equal(15.86, cell.BraggAngle(2, 0, 0, 1.5406), 1);
        Assert.True(double.IsNaN(cell.BraggAngle(8, 8, 8, 1.5406)), "Недостижимое отражение должно давать NaN");
    }

    /// <summary>Плотность хлорида натрия при Z = 4 составляет 2.16 г/см3.</summary>
    [Fact]
    public void UnitCell_DensityOfSodiumChloride()
    {
        UnitCell cell = UnitCell.Cubic(NaClEdge);

        Assert.Equal(2.163, cell.Density(58.44, 4), 2);
    }

    /// <summary>Переход в дробные координаты и обратно возвращает ту же точку.</summary>
    [Fact]
    public void UnitCell_FractionalRoundTripInTriclinicCell()
    {
        var cell = new UnitCell(7.1, 8.3, 9.7, 82, 97, 105);
        var point = new Vector3(1.3, -2.7, 4.1);

        Vector3 back = cell.ToCartesian(cell.ToFractional(point));

        Assert.Equal(point.X, back.X, 9);
        Assert.Equal(point.Y, back.Y, 9);
        Assert.Equal(point.Z, back.Z, 9);
        Assert.Equal(CrystalSystem.Triclinic, cell.System);
    }

    /// <summary>Кратчайший образ учитывает периодичность: 0.9 ячейки это 0.1 в обратную сторону.</summary>
    [Fact]
    public void UnitCell_MinimumImageCrossesBoundary()
    {
        UnitCell cell = UnitCell.Cubic(10);

        Vector3 image = cell.MinimumImage(new Vector3(0.5, 0, 0), new Vector3(9.5, 0, 0));

        Assert.Equal(-1, image.X, 9);
        Assert.Equal(1, image.Length, 9);
    }

    /// <summary>Угол в молекуле воды по построенным координатам равен 104.5 градуса.</summary>
    [Fact]
    public void MolecularStructure_WaterGeometry()
    {
        double half = 104.5 / 2 * Math.PI / 180;
        var water = new MolecularStructure();

        water.Add("O", 0, 0, 0);
        water.Add("H", 0.9572 * Math.Sin(half), 0.9572 * Math.Cos(half), 0);
        water.Add("H", -0.9572 * Math.Sin(half), 0.9572 * Math.Cos(half), 0);

        Assert.Equal(0.9572, water.Distance(0, 1), 6);
        Assert.Equal(104.5, water.Angle(1, 0, 2), 6);
        Assert.Equal("H2O", water.Formula);
    }

    /// <summary>Плоский зигзаг даёт торсионный угол 180 градусов.</summary>
    [Fact]
    public void MolecularStructure_TorsionOfPlanarChain()
    {
        var chain = new MolecularStructure();

        chain.Add("C", 0, 0, 0);
        chain.Add("C", 1.5, 0, 0);
        chain.Add("C", 2.0, 1.4, 0);
        chain.Add("C", 3.5, 1.4, 0);

        Assert.Equal(180, Math.Abs(chain.Torsion(0, 1, 2, 3)), 6);
    }

    /// <summary>Радиус инерции квадрата со стороной 2 равен корню из двух.</summary>
    [Fact]
    public void MolecularStructure_RadiusOfGyration()
    {
        var square = new MolecularStructure();

        square.Add("C", 1, 1, 0);
        square.Add("C", -1, 1, 0);
        square.Add("C", -1, -1, 0);
        square.Add("C", 1, -1, 0);

        Assert.Equal(Math.Sqrt(2), square.RadiusOfGyration(), 9);
        Assert.Equal(0, square.Centroid.Length, 9);
    }

    /// <summary>Операция симметрии разбирается и применяется по-разному внутри и вне ячейки.</summary>
    [Fact]
    public void SymmetryOperation_ParsesAndApplies()
    {
        SymmetryOperation operation = SymmetryOperation.Parse("-x,y+1/2,-z");
        var point = new Vector3(0.1, 0.2, 0.3);

        Vector3 direct = operation.Apply(point);
        Vector3 wrapped = operation.ApplyWrapped(point);

        Assert.Equal(-0.1, direct.X, 9);
        Assert.Equal(0.7, direct.Y, 9);
        Assert.Equal(-0.3, direct.Z, 9);

        Assert.Equal(0.9, wrapped.X, 9);
        Assert.Equal(0.7, wrapped.Y, 9);
        Assert.Equal(0.7, wrapped.Z, 9);
    }

    /// <summary>Неразбираемая запись симметрии отвергается, а не даёт тождественную операцию.</summary>
    [Fact]
    public void SymmetryOperation_RejectsNonsense()
    {
        Assert.False(SymmetryOperation.TryParse("x,y", out _));
        Assert.False(SymmetryOperation.TryParse("q,y,z", out _));
    }

    /// <summary>Запись и чтение XYZ возвращают ту же структуру.</summary>
    [Fact]
    public void StructureFormats_XyzRoundTrip()
    {
        var methane = new MolecularStructure { Name = "метан" };

        methane.Add("C", 0, 0, 0);
        methane.Add("H", 0.629, 0.629, 0.629);
        methane.Add("H", -0.629, -0.629, 0.629);
        methane.Add("H", -0.629, 0.629, -0.629);
        methane.Add("H", 0.629, -0.629, -0.629);

        MolecularStructure read = StructureFormats.ReadXyz(StructureFormats.WriteXyz(methane));

        Assert.Equal(methane.Count, read.Count);
        Assert.Equal("CH4", read.Formula);
        Assert.Equal(1.0894, read.Distance(0, 1), 3);
    }

    /// <summary>Многокадровый XYZ читается покадрово вместе с ячейкой из заголовка.</summary>
    [Fact]
    public void StructureFormats_ReadsXyzTrajectory()
    {
        string text = string.Join("\n",
            "2",
            "Lattice=\"10.0 0.0 0.0 0.0 10.0 0.0 0.0 0.0 10.0\"",
            "Ar 0.0 0.0 0.0",
            "Ar 3.0 0.0 0.0",
            "2",
            "Lattice=\"10.0 0.0 0.0 0.0 10.0 0.0 0.0 0.0 10.0\"",
            "Ar 0.5 0.0 0.0",
            "Ar 3.5 0.0 0.0");

        var frames = StructureFormats.ReadXyzTrajectory(text);

        Assert.Equal(2, frames.Count);
        Assert.Equal(2, frames[0].Count);
        Assert.NotNull(frames[0].Cell);
        Assert.Equal(1000, frames[0].Cell.Volume, 6);
        Assert.Equal(0.5, frames[1].Atoms[0].Position.X, 9);
    }

    /// <summary>Файл PDB читается по колонкам вместе с параметрами ячейки.</summary>
    [Fact]
    public void StructureFormats_ReadsPdb()
    {
        string text = string.Join("\n",
            "CRYST1   10.000   10.000   10.000  90.00  90.00  90.00 P 1           1",
            "ATOM      1  N   ALA A   1       1.000   2.000   3.000  1.00 20.00           N",
            "ATOM      2  CA  ALA A   1       2.000   2.000   3.000  1.00 25.00           C",
            "END");

        MolecularStructure structure = StructureFormats.ReadPdb(text);

        Assert.Equal(2, structure.Count);
        Assert.Equal("N", structure.Atoms[0].Element);
        Assert.Equal(1.0, structure.Atoms[0].Position.X, 6);
        Assert.Equal(20.0, structure.Atoms[0].ThermalParameter, 6);
        Assert.Equal(1.0, structure.Distance(0, 1), 6);
        Assert.NotNull(structure.Cell);
    }

    /// <summary>CIF читается вместе с симметрией, группой и погрешностями в скобках.</summary>
    [Fact]
    public void StructureFormats_ReadsCif()
    {
        CifContent content = StructureFormats.ReadCif(SodiumChlorideCif);

        Assert.NotNull(content.AsymmetricUnit.Cell);
        Assert.Equal(NaClEdge, content.AsymmetricUnit.Cell.A, 6);
        Assert.Equal(2, content.AsymmetricUnit.Count);
        Assert.Equal(4, content.Symmetry.Count);
        Assert.Equal("F m -3 m", content.SpaceGroup);
        Assert.Equal(225, content.SpaceGroupNumber);
    }

    /// <summary>CIF хлорида натрия с гранецентрированными трансляциями.</summary>
    internal const string SodiumChlorideCif = """
        data_NaCl
        _chemical_name_common 'галит'
        _cell_length_a 5.6402(2)
        _cell_length_b 5.6402(2)
        _cell_length_c 5.6402(2)
        _cell_angle_alpha 90
        _cell_angle_beta 90
        _cell_angle_gamma 90
        _symmetry_space_group_name_H-M 'F m -3 m'
        _symmetry_Int_Tables_number 225
        loop_
        _symmetry_equiv_pos_as_xyz
        'x,y,z'
        'x,y+1/2,z+1/2'
        'x+1/2,y,z+1/2'
        'x+1/2,y+1/2,z'
        loop_
        _atom_site_label
        _atom_site_type_symbol
        _atom_site_fract_x
        _atom_site_fract_y
        _atom_site_fract_z
        Na1 Na 0.0 0.0 0.0
        Cl1 Cl 0.5 0.0 0.0
        """;
}
