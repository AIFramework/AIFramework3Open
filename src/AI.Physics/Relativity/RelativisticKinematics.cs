using AI.Geometry.Primitives;
using AI.Physics.Internal;
using AI.Units;

namespace AI.Physics.Relativity;

/// <summary>
/// Релятивистская кинематика частиц: четырёхимпульсы, инвариантная масса, двухчастичный
/// распад, порог реакции.
/// </summary>
/// <remarks>
/// <para>
/// Все ответы получаются из одного факта: квадрат полного четырёхимпульса системы <c>s</c> —
/// инвариант. В системе центра масс <c>√s</c> — вся энергия, доступная для рождения новых частиц;
/// в лаборатории её приходится вычислять, и именно здесь интуиция классической механики
/// ошибается сильнее всего.
/// </para>
/// <para>
/// Массы принимаются и как масса, и как энергия покоя, импульсы — и в кг·м/с, и как <c>pc</c>
/// (см. <see cref="MassEnergy"/>). Четырёхимпульсы — в джоулях.
/// </para>
/// </remarks>
public static class RelativisticKinematics
{
    /// <summary>Четырёхимпульс покоящейся частицы <c>(mc², 0)</c></summary>
    /// <param name="mass">Масса или энергия покоя</param>
    public static FourVector AtRest(Quantity mass) => new(RestEnergy(mass, nameof(mass)), Vector3.Zero);

    /// <summary>Четырёхимпульс частицы по массе и импульсу</summary>
    /// <param name="mass">Масса или энергия покоя</param>
    /// <param name="momentum">Модуль импульса: в кг·м/с или как <c>pc</c> в единицах энергии</param>
    /// <param name="direction">Направление движения; длина не важна</param>
    public static FourVector FromMomentum(Quantity mass, Quantity momentum, Vector3 direction)
    {
        double m = RestEnergy(mass, nameof(mass));
        double p = MassEnergy.MomentumJoules(momentum, nameof(momentum));

        if (!(p >= 0) || double.IsInfinity(p))
            throw new ArgumentOutOfRangeException(nameof(momentum), "Модуль импульса — конечное неотрицательное число");

        Vector3 unit = p == 0 ? Vector3.Zero : Direction(direction);

        return new FourVector(Math.Sqrt((p * p) + (m * m)), unit * p);
    }

    /// <summary>Четырёхимпульс частицы по массе и кинетической энергии</summary>
    /// <param name="mass">Масса или энергия покоя</param>
    /// <param name="kineticEnergy">Кинетическая энергия</param>
    /// <param name="direction">Направление движения; длина не важна</param>
    public static FourVector FromKineticEnergy(Quantity mass, Quantity kineticEnergy, Vector3 direction)
    {
        double m = RestEnergy(mass, nameof(mass));
        double t = kineticEnergy.RequireSi(Dimension.Energy, nameof(kineticEnergy));

        if (!(t >= 0) || double.IsInfinity(t))
            throw new ArgumentOutOfRangeException(nameof(kineticEnergy), "Кинетическая энергия — конечное неотрицательное число");

        // pc = √(T(T + 2mc²)) — без вычитания близких чисел
        double p = Math.Sqrt(t * (t + (2 * m)));
        Vector3 unit = p == 0 ? Vector3.Zero : Direction(direction);

        return new FourVector(t + m, unit * p);
    }

    /// <summary>Четырёхимпульс фотона</summary>
    /// <param name="energy">Энергия фотона</param>
    /// <param name="direction">Направление; длина не важна</param>
    public static FourVector Photon(Quantity energy, Vector3 direction)
    {
        double e = energy.RequireSi(Dimension.Energy, nameof(energy));

        if (!(e >= 0) || double.IsInfinity(e))
            throw new ArgumentOutOfRangeException(nameof(energy), "Энергия фотона — конечное неотрицательное число");

        return new FourVector(e, e == 0 ? Vector3.Zero : Direction(direction) * e);
    }

    /// <summary>
    /// Энергия в системе центра масс <c>√s</c> — она же инвариантная масса системы в единицах энергии
    /// </summary>
    /// <remarks>
    /// По продуктам распада так восстанавливают массу распавшейся частицы, по сталкивающимся
    /// частицам — энергию, доступную для рождения новых.
    /// </remarks>
    /// <param name="particles">Четырёхимпульсы частиц</param>
    public static Quantity CentreOfMassEnergy(params FourVector[] particles)
    {
        ArgumentNullException.ThrowIfNull(particles);

        if (particles.Length == 0)
            throw new ArgumentException("Нужна хотя бы одна частица", nameof(particles));

        FourVector total = FourVector.Zero;

        foreach (FourVector particle in particles)
            total += particle;

        // У безмассовой системы округление может дать крошечный отрицательный квадрат
        return new Quantity(Math.Sqrt(Math.Max(0, total.Square)), Dimension.Energy);
    }

    /// <summary>Кинетическая энергия частицы по её четырёхимпульсу</summary>
    /// <param name="momentum">Четырёхимпульс</param>
    public static Quantity KineticEnergy(FourVector momentum)
    {
        double mass = Math.Sqrt(Math.Max(0, momentum.Square));
        double denominator = momentum.T + mass;

        // T = E − mc² = (pc)²/(E + mc²): у медленных частиц без вычитания близких чисел
        return new Quantity(denominator == 0 ? 0 : momentum.Space.LengthSquared / denominator, Dimension.Energy);
    }

    /// <summary>
    /// Двухчастичный распад в системе покоя исходной частицы
    /// </summary>
    /// <param name="parent">Масса распадающейся частицы</param>
    /// <param name="first">Масса первого продукта</param>
    /// <param name="second">Масса второго продукта</param>
    /// <exception cref="ArgumentException">Продукты тяжелее исходной частицы: распад запрещён</exception>
    public static TwoBodyDecay Decay(Quantity parent, Quantity first, Quantity second)
    {
        double big = RestEnergy(parent, nameof(parent));
        double a = RestEnergy(first, nameof(first));
        double b = RestEnergy(second, nameof(second));

        if (big <= 0)
            throw new ArgumentOutOfRangeException(nameof(parent), "Распадаться может только частица с массой");

        if (big < a + b)
            throw new ArgumentException(
                $"Распад запрещён сохранением энергии: продукты ({PhysicsFormat.Energy(a + b)}) тяжелее "
                + $"исходной частицы ({PhysicsFormat.Energy(big)})",
                nameof(parent));

        // Произведение четырёх множителей вместо M² − (a + b)²: разность масс вычисляется один раз
        double product = (big - a - b) * (big + a + b) * (big - a + b) * (big + a - b);
        double momentum = Math.Sqrt(Math.Max(0, product)) / (2 * big);
        double firstEnergy = ((big * big) + (a * a) - (b * b)) / (2 * big);
        double secondEnergy = ((big * big) - (a * a) + (b * b)) / (2 * big);

        return new TwoBodyDecay(
            Joules(big), Joules(a), Joules(b), Joules(momentum), Joules(firstEnergy), Joules(secondEnergy));
    }

    /// <summary>
    /// Порог реакции на неподвижной мишени: наименьшая кинетическая энергия налетающей частицы
    /// </summary>
    /// <remarks>
    /// <c>T = ((Σm_f)² − (m_a + m_b)²)c² / (2m_b)</c>. Формула точная и годится от ядерных реакций,
    /// где классическое приближение <c>−Q(1 + m_a/m_b)</c> совпадает с ней до долей процента,
    /// до рождения частиц, где классика ошибается в разы.
    /// </remarks>
    /// <param name="projectile">Масса налетающей частицы</param>
    /// <param name="target">Масса покоящейся мишени</param>
    /// <param name="products">Массы продуктов реакции</param>
    public static ReactionThreshold Threshold(Quantity projectile, Quantity target, params Quantity[] products)
    {
        ArgumentNullException.ThrowIfNull(products);

        if (products.Length == 0)
            throw new ArgumentException("Нужен хотя бы один продукт реакции", nameof(products));

        double a = RestEnergy(projectile, nameof(projectile));
        double b = RestEnergy(target, nameof(target));

        if (b <= 0)
            throw new ArgumentOutOfRangeException(nameof(target), "Мишень должна иметь массу: безмассовая мишень не бывает неподвижной");

        double final = 0;

        foreach (Quantity product in products)
            final += RestEnergy(product, nameof(products));

        double initial = a + b;
        double kinetic = final > initial ? (final - initial) * (final + initial) / (2 * b) : 0;

        return new ReactionThreshold(Joules(initial), Joules(final), Joules(a), Joules(b), Joules(kinetic));
    }

    private static double RestEnergy(Quantity mass, string paramName)
    {
        double energy = MassEnergy.RestEnergyJoules(mass, paramName);

        if (!(energy >= 0) || double.IsInfinity(energy))
            throw new ArgumentOutOfRangeException(paramName, "Масса — конечное неотрицательное число");

        return energy;
    }

    private static Vector3 Direction(Vector3 direction)
    {
        double length = direction.Length;

        if (!(length > 0) || double.IsInfinity(length))
            throw new ArgumentException("Направление движения должно быть ненулевым конечным вектором", nameof(direction));

        return direction / length;
    }

    private static Quantity Joules(double value) => new(value, Dimension.Energy);
}
