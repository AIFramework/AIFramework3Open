using AiFrameworkDemo.Core;

namespace AiFrameworkDemo.Modules.Geometry;

public sealed class GeometryModule : LibraryModuleBase
{
    public override string Id => "geom";
    public override string Name => "AI.Geometry";
    public override string Description => "Геометрия: примитивы, пересечения, кватернионы, кривые, подгонка, линейная алгебра";
    public override string Color => "sky";
    public override string TutorialFolder => "Geometry";

    public override string IconSvg => """
        <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round">
          <circle cx="12" cy="12" r="9"/>
          <line x1="12" y1="3" x2="12" y2="21"/>
          <line x1="3" y1="12" x2="21" y2="12"/>
          <path d="M5.6 5.6l12.8 12.8"/>
          <path d="M18.4 5.6L5.6 18.4"/>
        </svg>
        """;

    public override IReadOnlyList<CategoryDef> Categories { get; } =
    [
        // -- Векторы и нормы ------------------------------------------------
        new CategoryDef("vectors", "Векторы и нормы",
            "Базовые операции: нормы, интерполяция, отражение, тройное произведение",
            [
                new AlgoDef("vector_norms", "Нормы L1 / L2 / L∞",
                    "Вычисление норм и нормализация вектора",
                    "Vector.NormL1, NormL2, MaxAbs",
                    "vector_norms.md",
                    [
                        new AlgoParam("dim", "Размерность", 2, 8, 3, 1, "", "Число компонент вектора"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("lerp_slerp", "Lerp / Slerp",
                    "Линейная и сферическая интерполяция векторов",
                    "Vector.Lerp, Vector.Slerp",
                    "lerp_slerp.md",
                    [
                        new AlgoParam("steps", "Шагов", 5, 30, 12, 1, "", "Число точек интерполяции"),
                    ]),
                new AlgoDef("reflect", "Отражение вектора",
                    "Отражение от плоскости: r = v − 2(v·n̂)n̂",
                    "Vector.Reflect",
                    "reflect.md",
                    [
                        new AlgoParam("angle", "Угол падения (°)", 10, 80, 45, 5, "°", "Угол вектора к нормали"),
                    ]),
                new AlgoDef("triple_product", "Тройное произведение",
                    "Объём параллелепипеда: V = |a · (b × c)|",
                    "Vector.TripleProduct",
                    "triple_product.md",
                    [
                        new AlgoParam("seed", "Seed", 0, 100, 7, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Преобразования -------------------------------------------------
        new CategoryDef("transforms", "Преобразования",
            "Кватернионы, аффинные и проективные преобразования",
            [
                new AlgoDef("quaternion_demo", "Кватернионы",
                    "Поворот точек кватернионом, визуализация Slerp",
                    "AI.Geometry.Transforms.Quaternion",
                    "quaternion_basics.md",
                    [
                        new AlgoParam("angle", "Угол поворота (°)", 10, 350, 90, 10, "°", "Угол вращения вокруг оси"),
                        new AlgoParam("steps", "Шагов slerp", 5, 30, 12, 1, "", "Число точек интерполяции"),
                    ]),
                new AlgoDef("affine_2d_demo", "Аффинные 2D",
                    "Перенос, масштаб, поворот, сдвиг в однородных координатах",
                    "AI.Geometry.Transforms.Affine2D",
                    "affine_2d.md",
                    [
                        new AlgoParam("tx", "Перенос X", -3, 3, 1, 0.5, "", "Смещение по X"),
                        new AlgoParam("ty", "Перенос Y", -3, 3, 0.5, 0.5, "", "Смещение по Y"),
                        new AlgoParam("angle", "Поворот (°)", 0, 360, 30, 5, "°", "Угол поворота"),
                        new AlgoParam("scale", "Масштаб", 0.2, 3, 1.5, 0.1, "", "Коэффициент масштабирования"),
                    ]),
                new AlgoDef("homography_demo", "Гомография",
                    "Проективное отображение плоскости по 4 точкам (DLT)",
                    "AI.Geometry.Transforms.Homography",
                    "homography.md",
                    [
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Расстояния и пересечения ---------------------------------------
        new CategoryDef("intersections", "Расстояния и пересечения",
            "Геометрические расстояния и тесты пересечения примитивов",
            [
                new AlgoDef("point_to_line_demo", "Точка -> Прямая",
                    "Расстояние от точки до прямой в 2D и 3D",
                    "AI.Geometry.Distances.PointLine",
                    "point_to_line.md",
                    [
                        new AlgoParam("n", "Число точек", 5, 50, 15, 1, "шт.", "Случайных точек для визуализации"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("ray_triangle_demo", "Луч -> Треугольник",
                    "Пересечение по алгоритму Möller–Trumbore (визуализация 2D-проекции)",
                    "AI.Geometry.Intersections.RayTriangleIntersection",
                    "ray_triangle.md",
                    [
                        new AlgoParam("rays", "Число лучей", 3, 20, 8, 1, "шт.", "Лучи из разных позиций"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("aabb_obb_demo", "AABB / OBB тест",
                    "Slab-метод для луч–AABB и SAT для OBB–OBB",
                    "AI.Geometry.Intersections",
                    "aabb_obb_sat.md",
                    [
                        new AlgoParam("n", "Число боксов", 3, 12, 6, 1, "шт.", "Случайных боксов"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Полигоны -------------------------------------------------------
        new CategoryDef("polygons", "Полигоны",
            "Площадь, принадлежность, центроид, ближайшая точка",
            [
                new AlgoDef("shoelace_demo", "Площадь (Shoelace)",
                    "Площадь полигона по формуле шнурков",
                    "AI.Geometry.Polygons.ShoelaceArea",
                    "shoelace.md",
                    [
                        new AlgoParam("n", "Вершин полигона", 3, 12, 6, 1, "шт.", "Число вершин случайного выпуклого полигона"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("point_in_polygon_demo", "Точка в полигоне",
                    "Ray casting и winding number",
                    "AI.Geometry.Polygons.PointInPolygon",
                    "point_in_polygon.md",
                    [
                        new AlgoParam("n", "Тестовых точек", 20, 200, 80, 10, "шт.", "Число случайных точек"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("closest_triangle_demo", "Ближайшая в ^",
                    "Ближайшая точка в треугольнике через барицентрические зоны",
                    "AI.Geometry.Polygons.ClosestInTriangle",
                    "closest_in_triangle.md",
                    [
                        new AlgoParam("n", "Тестовых точек", 10, 60, 25, 5, "шт.", "Число случайных точек"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Подгонка -------------------------------------------------------
        new CategoryDef("fitting", "Подгонка",
            "Линейная и окружностная подгонка: OLS, TLS, RANSAC",
            [
                new AlgoDef("line_fit_demo", "Прямая: OLS / TLS / RANSAC",
                    "Сравнение трёх методов подгонки прямой на зашумлённых данных с выбросами",
                    "AI.Geometry.Fitting.LineFit",
                    "line_fit_ols_tls_ransac.md",
                    [
                        new AlgoParam("n", "Число точек", 20, 200, 60, 10, "шт.", "Объём выборки"),
                        new AlgoParam("outliers", "Выбросов (%)", 0, 40, 15, 5, "%", "Процент выбросов"),
                        new AlgoParam("noise", "Шум σ", 0, 2, 0.3, 0.1, "", "Стандартное отклонение шума"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("circle_fit_demo", "Окружность: Kåsa / RANSAC",
                    "Подгонка окружности алгебраическим методом и RANSAC",
                    "AI.Geometry.Fitting.CircleFit",
                    "circle_fit.md",
                    [
                        new AlgoParam("n", "Число точек", 15, 150, 50, 5, "шт.", "Объём выборки"),
                        new AlgoParam("outliers", "Выбросов (%)", 0, 40, 10, 5, "%", "Процент выбросов"),
                        new AlgoParam("noise", "Шум σ", 0, 1, 0.15, 0.05, "", "Стандартное отклонение шума"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Кривые ---------------------------------------------------------
        new CategoryDef("curves", "Параметрические кривые",
            "Кривые Безье, Эрмита, Catmull–Rom",
            [
                new AlgoDef("bezier_demo", "Кривая Безье",
                    "De Casteljau для произвольной степени, визуализация контрольного полигона",
                    "AI.Geometry.Curves.BezierCurve",
                    "bezier.md",
                    [
                        new AlgoParam("degree", "Степень", 2, 6, 3, 1, "", "Степень кривой Безье (число контрольных точек = степень + 1)"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("hermite_demo", "Сплайн Эрмита",
                    "Кусочно-кубический Эрмит с заданными касательными",
                    "AI.Geometry.Curves.HermiteCurve",
                    "hermite.md",
                    [
                        new AlgoParam("pts", "Число точек", 3, 8, 5, 1, "шт.", "Число узловых точек"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("catmull_rom_demo", "Catmull–Rom",
                    "Автоматические касательные через соседние точки",
                    "AI.Geometry.Curves.CatmullRomCurve",
                    "catmull_rom.md",
                    [
                        new AlgoParam("pts", "Число точек", 4, 10, 6, 1, "шт.", "Число узловых точек"),
                        new AlgoParam("alpha", "α (0–1)", 0, 1, 0.5, 0.1, "", "Параметр центрипетальности"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Линейная алгебра -----------------------------------------------
        new CategoryDef("linalg", "Линейная алгебра",
            "Матричные разложения: SVD, LU, Холецкий, псевдообратная, Якоби",
            [
                new AlgoDef("svd_demo", "SVD-разложение",
                    "A = UΣVᵀ — визуализация сингулярных значений как осей эллипса",
                    "AI.ClassicMath.MatrixUtils.Svd",
                    "svd.md",
                    [
                        new AlgoParam("rows", "Строк", 2, 6, 3, 1, "", "Число строк матрицы"),
                        new AlgoParam("cols", "Столбцов", 2, 6, 3, 1, "", "Число столбцов"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("lu_demo", "LU-разложение",
                    "A = PLU с частичным выбором главного элемента",
                    "AI.ClassicMath.MatrixUtils.LU",
                    "lu.md",
                    [
                        new AlgoParam("n", "Размер", 2, 6, 3, 1, "", "Размер квадратной матрицы"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("cholesky_demo", "Холецкий",
                    "A = LLᵀ для симметричных положительно определённых матриц",
                    "AI.ClassicMath.MatrixUtils.Cholesky",
                    "cholesky.md",
                    [
                        new AlgoParam("n", "Размер", 2, 6, 3, 1, "", "Размер SPD-матрицы"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("pseudoinverse_demo", "Псевдообратная",
                    "A⁺ = VΣ⁺Uᵀ через SVD-разложение",
                    "AI.ClassicMath.MatrixUtils.Pseudoinverse",
                    "pseudoinverse.md",
                    [
                        new AlgoParam("rows", "Строк", 2, 6, 4, 1, "", "Число строк матрицы"),
                        new AlgoParam("cols", "Столбцов", 2, 6, 3, 1, "", "Число столбцов"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
                new AlgoDef("jacobi_eigen_demo", "Якоби (eigen)",
                    "Собственные значения и векторы симметричной матрицы методом вращений",
                    "AI.ClassicMath.MatrixUtils.JacobiEigen",
                    "jacobi_eigen.md",
                    [
                        new AlgoParam("n", "Размер", 2, 6, 3, 1, "", "Размер симметричной матрицы"),
                        new AlgoParam("seed", "Seed", 0, 100, 42, 1, "", "Инициализация генератора"),
                    ]),
            ]),

        // -- Коники ---------------------------------------------------------
        new CategoryDef("conics", "Кривые 2-го порядка",
            "Классификация конических сечений по общему уравнению",
            [
                new AlgoDef("conic_demo", "Классификация коники",
                    "Ax²+Bxy+Cy²+Dx+Ey+F=0 -> эллипс / парабола / гипербола / окружность",
                    "AI.Geometry.Conics.ConicSection",
                    "conic_classification.md",
                    [
                        new AlgoParam("A", "A", -5, 5, 1, 0.5, "", "Коэффициент при x²"),
                        new AlgoParam("B", "B", -5, 5, 0, 0.5, "", "Коэффициент при xy"),
                        new AlgoParam("C", "C", -5, 5, 1, 0.5, "", "Коэффициент при y²"),
                        new AlgoParam("D", "D", -5, 5, 0, 0.5, "", "Коэффициент при x"),
                        new AlgoParam("E", "E", -5, 5, 0, 0.5, "", "Коэффициент при y"),
                        new AlgoParam("F", "F", -10, 10, -4, 0.5, "", "Свободный член"),
                    ]),
            ]),
    ];

    protected override DemoResult RunCore(
        string algoKey,
        IReadOnlyDictionary<string, double> numericParams,
        IReadOnlyDictionary<string, string> textParams,
        DemoSettings settings) =>
        GeometryDemoRunner.Run(algoKey, numericParams, settings);
}
