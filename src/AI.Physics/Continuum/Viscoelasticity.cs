using AI.Physics.Fluids;
using AI.Units;

namespace AI.Physics.Continuum;

/// <summary>Вид вязкоупругой модели</summary>
public enum ViscoelasticKind
{
    /// <summary>Максвелл: пружина и демпфер последовательно — жидкость, течёт при постоянной нагрузке</summary>
    Maxwell,

    /// <summary>Кельвин — Фойгт: пружина и демпфер параллельно — твёрдое тело с запаздывающей упругостью</summary>
    KelvinVoigt,

    /// <summary>Стандартное линейное тело Зинера: пружина параллельно ветви Максвелла — релаксирует до конечного модуля</summary>
    StandardLinearSolid
}

/// <summary>Комплексный модуль при гармоническом нагружении</summary>
/// <param name="Storage">Модуль накопления E′ — упругая часть</param>
/// <param name="Loss">Модуль потерь E″ — рассеяние энергии</param>
/// <param name="LossTangent">Тангенс угла потерь E″/E′</param>
public readonly record struct ComplexModulus(Quantity Storage, Quantity Loss, double LossTangent);

/// <summary>
/// Линейная вязкоупругость: модели Максвелла, Кельвина — Фойгта и Зинера.
/// </summary>
/// <remarks>
/// <para>
/// Полимеры, биологические ткани, асфальт помнят историю нагружения. Под постоянным напряжением
/// деформация растёт — ползучесть, её описывает податливость J(t); при постоянной деформации
/// напряжение спадает — релаксация, её описывает модуль E(t). Две функции не независимы: их
/// свёртка <c>∫₀ᵗ E(t − s)·J(s) ds = t</c> при любом t.
/// </para>
/// <para>
/// Максвелл релаксирует до нуля и ползёт неограниченно — это жидкость. Кельвин — Фойгт не релаксирует
/// вовсе: мгновенная деформация невозможна, его модуль в момент нагружения содержит δ-функцию,
/// и здесь при t &gt; 0 возвращается его равновесная часть. Зинер — простейшая модель твёрдого
/// полимера: мгновенный модуль E∞ + E₁ спадает до E∞.
/// </para>
/// </remarks>
public sealed class ViscoelasticModel
{
    private readonly double _spring;
    private readonly double _armSpring;
    private readonly double _viscosity;

    private ViscoelasticModel(ViscoelasticKind kind, double spring, double armSpring, double viscosity)
    {
        if (!(spring > 0) || !(viscosity > 0) || (kind == ViscoelasticKind.StandardLinearSolid && !(armSpring > 0)))
            throw new ArgumentOutOfRangeException(nameof(spring), "Модули и вязкость должны быть положительными");

        Kind = kind;
        _spring = spring;
        _armSpring = armSpring;
        _viscosity = viscosity;
    }

    /// <summary>Модель Максвелла</summary>
    /// <param name="modulus">Модуль пружины</param>
    /// <param name="viscosity">Вязкость демпфера</param>
    public static ViscoelasticModel Maxwell(Quantity modulus, Quantity viscosity)
        => new(ViscoelasticKind.Maxwell, Modulus(modulus), 0, Viscosity(viscosity));

    /// <summary>Модель Кельвина — Фойгта</summary>
    /// <param name="modulus">Модуль пружины</param>
    /// <param name="viscosity">Вязкость демпфера</param>
    public static ViscoelasticModel KelvinVoigt(Quantity modulus, Quantity viscosity)
        => new(ViscoelasticKind.KelvinVoigt, Modulus(modulus), 0, Viscosity(viscosity));

    /// <summary>Стандартное линейное тело: пружина E∞ параллельно ветви Максвелла (E₁, η₁)</summary>
    /// <param name="equilibriumModulus">Равновесный модуль E∞</param>
    /// <param name="armModulus">Модуль пружины ветви E₁</param>
    /// <param name="armViscosity">Вязкость демпфера ветви η₁</param>
    public static ViscoelasticModel StandardLinearSolid(Quantity equilibriumModulus, Quantity armModulus, Quantity armViscosity)
        => new(ViscoelasticKind.StandardLinearSolid, Modulus(equilibriumModulus), Modulus(armModulus), Viscosity(armViscosity));

    /// <summary>Вид модели</summary>
    public ViscoelasticKind Kind { get; }

    /// <summary>Время релаксации напряжения при постоянной деформации</summary>
    public Quantity RelaxationTime => new(RelaxationTimeSi, Dimension.TimeDim);

    /// <summary>Время запаздывания деформации при постоянном напряжении</summary>
    public Quantity RetardationTime => new(Kind switch
    {
        ViscoelasticKind.Maxwell => double.PositiveInfinity,
        ViscoelasticKind.KelvinVoigt => _viscosity / _spring,
        _ => RelaxationTimeSi * (_spring + _armSpring) / _spring
    }, Dimension.TimeDim);

    /// <summary>Модуль релаксации E(t): напряжение на единицу мгновенно приложенной деформации</summary>
    /// <param name="time">Время после нагружения</param>
    public Quantity RelaxationModulus(Quantity time)
    {
        double t = Time(time);

        double value = Kind switch
        {
            ViscoelasticKind.Maxwell => _spring * Math.Exp(-t / RelaxationTimeSi),
            ViscoelasticKind.KelvinVoigt => _spring,
            _ => _spring + (_armSpring * Math.Exp(-t / RelaxationTimeSi))
        };

        return new Quantity(value, Dimension.Pressure);
    }

    /// <summary>Податливость ползучести J(t): деформация на единицу мгновенно приложенного напряжения</summary>
    /// <param name="time">Время после нагружения</param>
    public Quantity CreepCompliance(Quantity time)
    {
        double t = Time(time);

        double value = Kind switch
        {
            ViscoelasticKind.Maxwell => (1 / _spring) + (t / _viscosity),
            ViscoelasticKind.KelvinVoigt => (1 - Math.Exp(-t * _spring / _viscosity)) / _spring,
            _ => (1 / _spring) - (((1 / _spring) - (1 / (_spring + _armSpring))) * Math.Exp(-t / RetardationTime.SiValue))
        };

        return new Quantity(value, Dimension.None / Dimension.Pressure);
    }

    /// <summary>Комплексный модуль при круговой частоте ω</summary>
    /// <param name="angularFrequency">Круговая частота</param>
    public ComplexModulus Dynamic(Quantity angularFrequency)
    {
        double w = angularFrequency.RequireSi(Dimension.Frequency, nameof(angularFrequency));

        if (!(w >= 0))
            throw new ArgumentOutOfRangeException(nameof(angularFrequency), "Частота не может быть отрицательной");

        double storage, loss;

        if (Kind == ViscoelasticKind.KelvinVoigt)
        {
            storage = _spring;
            loss = _viscosity * w;
        }
        else
        {
            double wt = w * RelaxationTimeSi;
            double arm = Kind == ViscoelasticKind.Maxwell ? _spring : _armSpring;
            double rest = Kind == ViscoelasticKind.Maxwell ? 0 : _spring;

            storage = rest + (arm * wt * wt / (1 + (wt * wt)));
            loss = arm * wt / (1 + (wt * wt));
        }

        return new ComplexModulus(
            new Quantity(storage, Dimension.Pressure),
            new Quantity(loss, Dimension.Pressure),
            storage > 0 ? loss / storage : double.PositiveInfinity);
    }

    private double RelaxationTimeSi => Kind switch
    {
        ViscoelasticKind.Maxwell => _viscosity / _spring,
        ViscoelasticKind.KelvinVoigt => 0,
        _ => _viscosity / _armSpring
    };

    private static double Modulus(Quantity value) => value.RequireSi(Dimension.Pressure, nameof(value));

    private static double Viscosity(Quantity value) => value.RequireSi(FlowDynamics.ViscosityDimension, nameof(value));

    private static double Time(Quantity time)
    {
        double t = time.RequireSi(Dimension.TimeDim, nameof(time));

        return t >= 0 ? t : throw new ArgumentOutOfRangeException(nameof(time), "Время после нагружения не может быть отрицательным");
    }
}
