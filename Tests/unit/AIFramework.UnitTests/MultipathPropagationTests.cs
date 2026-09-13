using System.Numerics;
using AI.HighLevelFunctions;
using Vector3 = AI.Geometry.Primitives.Vector3;
using AI.Microwave.Propagation;
using AI.Microwave.Safety;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Многолучевое распространение проверяется ответами, полученными независимо: функция Бесселя —
/// рядом и асимптотикой, интегралы Френеля — квадратурой, коэффициенты отражения — законом сохранения
/// энергии, трассировка — двухлучевой моделью и аналитическим рядом изображений в коридоре, замирания —
/// автокорреляцией J₀ и формулами Райса для пересечений уровня, профили TR 38.901 — своей нормировкой.
/// </summary>
public class MultipathPropagationTests
{
    private const double Frequency = 2e9;

    #region Специальные функции

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    [InlineData(2.404825557695773)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void BesselJ0_MatchesPowerSeries(double x)
    {
        double series = 0, term = 1;

        for (int k = 0; k < 80; k++)
        {
            series += term;
            term *= -(x / 2) * (x / 2) / ((k + 1.0) * (k + 1.0));
        }

        Assert.Equal(series, SpecialFunctions.BesselJ0(x), 12);
    }

    [Fact]
    public void BesselJ0_KnownValuesAndAsymptote()
    {
        Assert.Equal(0.7651976865579666, SpecialFunctions.BesselJ0(1), 14);
        Assert.Equal(0, SpecialFunctions.BesselJ0(2.404825557695773), 13);

        // Ханкелева асимптотика с двумя поправками точна до 10⁻⁹ при x = 100
        foreach (double x in new[] { 100.0, 1000.0 })
        {
            double chi = x - (Math.PI / 4);
            double asymptote = Math.Sqrt(2 / (Math.PI * x))
                * (((1 - (9 / (128 * x * x))) * Math.Cos(chi)) + (((1 / (8 * x)) - (75 / (1024 * x * x * x))) * Math.Sin(chi)));

            Assert.Equal(asymptote, SpecialFunctions.BesselJ0(x), 8);
        }
    }

    [Theory]
    [InlineData(0.3)]
    [InlineData(1.0)]
    [InlineData(1.5)]
    [InlineData(1.6)]
    [InlineData(2.5)]
    [InlineData(4.0)]
    [InlineData(7.0)]
    public void Fresnel_MatchesQuadrature(double x)
    {
        (double c, double s) = SpecialFunctions.Fresnel(x);

        Assert.Equal(Simpson(t => Math.Cos(Math.PI * t * t / 2), x), c, 9);
        Assert.Equal(Simpson(t => Math.Sin(Math.PI * t * t / 2), x), s, 9);
        Assert.Equal((-c, -s), SpecialFunctions.Fresnel(-x));
    }

    [Fact]
    public void Fresnel_ApproachesOneHalf()
    {
        foreach (double x in new[] { 20.0, 50.0, 300.0 })
        {
            (double c, double s) = SpecialFunctions.Fresnel(x);

            Assert.InRange(Math.Abs(c - 0.5), 0, (1 / (Math.PI * x)) + 1e-12);
            Assert.InRange(Math.Abs(s - 0.5), 0, (1 / (Math.PI * x)) + 1e-12);
        }
    }

    #endregion

    #region Отражение, двухлучевая модель, дифракция

    [Fact]
    public void Fresnel_Reflection_ConservesEnergy_AndHasBrewsterAngle()
    {
        const double Epsilon = 4;
        double n2 = Math.Sqrt(Epsilon);

        for (double psi = 0.01; psi < Math.PI / 2; psi += 0.05)
        {
            ReflectionCoefficients r = FresnelReflection.Coefficients(Epsilon, psi);

            // Прохождение считается независимо: закон Снеллиуса и формулы для t
            double cosIncidence = Math.Sin(psi);
            double cosTransmitted = Math.Sqrt(1 - (Math.Cos(psi) * Math.Cos(psi) / Epsilon));
            double ts = 2 * cosIncidence / (cosIncidence + (n2 * cosTransmitted));
            double tp = 2 * cosIncidence / ((n2 * cosIncidence) + cosTransmitted);
            double ratio = n2 * cosTransmitted / cosIncidence;

            Assert.Equal(1, (r.Perpendicular.Magnitude * r.Perpendicular.Magnitude) + (ratio * ts * ts), 12);
            Assert.Equal(1, (r.Parallel.Magnitude * r.Parallel.Magnitude) + (ratio * tp * tp), 12);
        }

        double brewster = FresnelReflection.BrewsterGrazingAngleRad(Epsilon);
        Assert.Equal(0, FresnelReflection.Coefficients(Epsilon, brewster).Parallel.Magnitude, 12);

        ReflectionCoefficients normal = FresnelReflection.Coefficients(Epsilon, Math.PI / 2);
        Assert.Equal((1 - n2) / (1 + n2), normal.Perpendicular.Real, 12);
        Assert.Equal(-normal.Perpendicular.Real, normal.Parallel.Real, 12);

        ReflectionCoefficients grazing = FresnelReflection.Coefficients(RadioMaterial.MediumDryGround.ComplexPermittivity(Frequency), 0);
        Assert.Equal(-1, grazing.Perpendicular.Real, 12);
        Assert.Equal(-1, grazing.Parallel.Real, 12);

        Assert.Equal(new ReflectionCoefficients(-1, 1), FresnelReflection.Coefficients(RadioMaterial.PerfectConductor, Frequency, 0.3));
        _ = Assert.Throws<ArgumentOutOfRangeException>(() => RadioMaterial.Concrete.ComplexPermittivity(500e6));
    }

    [Fact]
    public void TwoRay_FallsFortyDecibelsPerDecadeBeyondBreakpoint()
    {
        const double Tx = 30, Rx = 1.5;
        double breakpoint = TwoRayGround.BreakpointDistanceM(Tx, Rx, Frequency);
        RadioMaterial ground = RadioMaterial.PerfectConductor;

        double near = TwoRayGround.PathLossDb(30 * breakpoint, Tx, Rx, Frequency, ground, Polarization.Horizontal);
        double far = TwoRayGround.PathLossDb(300 * breakpoint, Tx, Rx, Frequency, ground, Polarization.Horizontal);

        Assert.Equal(40, far - near, 0.1);
        Assert.Equal(TwoRayGround.PlaneEarthLossDb(300 * breakpoint, Tx, Rx), far, 0.05);

        // В точке перелома лучи последний раз складываются в фазе: на 6 дБ лучше свободного пространства
        double atBreakpoint = TwoRayGround.PathLossDb(breakpoint, Tx, Rx, Frequency, ground, Polarization.Horizontal);
        Assert.Equal(PathLoss.FreeSpaceDb(Frequency, breakpoint) - (20 * Math.Log10(2)), atBreakpoint, 0.1);

        // Прямой луч — ровно свободное пространство существующей модели потерь
        MultipathChannel channel = TwoRayGround.Channel(1000, Tx, Rx, Frequency, ground);
        double direct = Math.Sqrt((1000 * 1000) + ((Tx - Rx) * (Tx - Rx)));
        Assert.Equal(-PathLoss.FreeSpaceDb(Frequency, direct), channel.Paths[0].PowerDb, 9);
    }

    [Fact]
    public void KnifeEdge_ExactAndItuApproximation()
    {
        Assert.Equal(20 * Math.Log10(2), KnifeEdgeDiffraction.LossDb(0), 9);

        for (double nu = -0.7; nu <= 10; nu += 0.1)
            Assert.InRange(Math.Abs(KnifeEdgeDiffraction.LossDb(nu) - KnifeEdgeDiffraction.ItuApproximationLossDb(nu)), 0, 0.6);

        // Глубокая тень: |F| → 1/(π·ν·√2)
        Assert.Equal(20 * Math.Log10(Math.PI * 10 * Math.Sqrt(2)), KnifeEdgeDiffraction.LossDb(10), 0.05);

        // Просвет больше первой зоны Френеля: поле колеблется около свободного пространства
        for (double nu = -3; nu <= -1; nu += 0.1)
            Assert.InRange(KnifeEdgeDiffraction.LossDb(nu), -1.5, 1.5);
    }

    #endregion

    #region Трассировка лучей

    [Theory]
    [InlineData(Polarization.Horizontal)]
    [InlineData(Polarization.Vertical)]
    public void RayTracing_OverGround_ReproducesTwoRayModel(Polarization polarization)
    {
        const double Tx = 25, Rx = 2, Distance = 400;
        Vector3 field = polarization == Polarization.Horizontal ? new Vector3(0, 1, 0) : new Vector3(0, 0, 1);

        var scene = new RayTracingScene(Frequency).Add(PlanarReflector.Infinite(Vector3.Zero, new Vector3(0, 0, 1), RadioMaterial.MediumDryGround, "земля"));
        MultipathChannel traced = scene.Trace(
            new RadioEndpoint(new Vector3(0, 0, Tx)) { Polarization = field },
            new RadioEndpoint(new Vector3(Distance, 0, Rx)) { Polarization = field });

        MultipathChannel model = TwoRayGround.Channel(Distance, Tx, Rx, Frequency, RadioMaterial.MediumDryGround, polarization);

        Assert.Equal(2, traced.Count);

        for (int i = 0; i < 2; i++)
        {
            Assert.Equal(model.Paths[i].DelaySeconds, traced.Paths[i].DelaySeconds, 15);
            Assert.True((model.Paths[i].Gain - traced.Paths[i].Gain).Magnitude < 1e-9 * model.Paths[i].Gain.Magnitude,
                $"путь {i}: {model.Paths[i].Gain} против {traced.Paths[i].Gain}");
        }
    }

    [Fact]
    public void RayTracing_Corridor_MatchesAnalyticImages()
    {
        // Приёмник не посередине: иначе пути через разные стены совпадают по длине
        const double Width = 4, TxY = 1, RxY = 2.5, Length = 30, Height = 1.5;
        double wavelength = 299792458.0 / Frequency;

        var scene = new RayTracingScene(Frequency)
            .Add(PlanarReflector.Infinite(new Vector3(0, 0, 0), new Vector3(0, 1, 0), RadioMaterial.PerfectConductor, "левая стена"))
            .Add(PlanarReflector.Infinite(new Vector3(0, Width, 0), new Vector3(0, 1, 0), RadioMaterial.PerfectConductor, "правая стена"));

        MultipathChannel traced = scene.Trace(new RadioEndpoint(new Vector3(0, TxY, Height)), new RadioEndpoint(new Vector3(Length, RxY, Height)), 3);

        // Изображения передатчика в двух стенах: порядок — число отражений
        (double Y, int Order)[] images =
        [
            (TxY, 0), (-TxY, 1), ((2 * Width) - TxY, 1), ((2 * Width) + TxY, 2), ((-2 * Width) + TxY, 2),
            ((-2 * Width) - TxY, 3), ((4 * Width) - TxY, 3)
        ];

        Assert.Equal(images.Length, traced.Count);

        foreach ((double y, int order) in images)
        {
            double path = Math.Sqrt((Length * Length) + ((y - RxY) * (y - RxY)));
            PropagationPath found = Assert.Single(traced.Paths, p => Math.Abs(p.LengthMetres - path) < 1e-9);

            // Вертикальная поляризация у вертикальной стены перпендикулярна плоскости падения: −1 на каждое отражение
            Complex expected = Math.Pow(-1, order) * (wavelength / (4 * Math.PI * path)) * Complex.FromPolarCoordinates(1, -2 * Math.PI * path / wavelength);

            Assert.Equal(order, found.Order);
            Assert.True((found.Gain - expected).Magnitude < 1e-9 * expected.Magnitude, $"порядок {order}");
        }
    }

    [Fact]
    public void RayTracing_WallBlocksLineOfSight_AndFiniteReflectorNeedsHitPoint()
    {
        var tx = new RadioEndpoint(new Vector3(0, 0, 1.5));
        var rx = new RadioEndpoint(new Vector3(20, 0, 1.5));

        // Стена поперёк трассы на x = 10 шириной 4 м и высотой 3 м
        var blocking = new RayTracingScene(Frequency)
            .Add(new PlanarReflector(new Vector3(10, -2, 0), new Vector3(0, 4, 0), new Vector3(0, 0, 3), RadioMaterial.Concrete, "стена"));

        Assert.DoesNotContain(blocking.Trace(tx, rx).Paths, p => p.Kind == PathKind.LineOfSight);

        // Та же стена в стороне не мешает прямому лучу и отражает: точка отражения внутри неё
        var aside = new RayTracingScene(Frequency)
            .Add(new PlanarReflector(new Vector3(0, 5, 0), new Vector3(20, 0, 0), new Vector3(0, 0, 3), RadioMaterial.Concrete, "стена"));

        MultipathChannel channel = aside.Trace(tx, rx);
        Assert.Contains(channel.Paths, p => p.Kind == PathKind.LineOfSight);
        Assert.Contains(channel.Paths, p => p.Kind == PathKind.Reflection);

        // Короткая стена, до точки отражения (x = 10) не достающая, не отражает
        var shortWall = new RayTracingScene(Frequency)
            .Add(new PlanarReflector(new Vector3(0, 5, 0), new Vector3(4, 0, 0), new Vector3(0, 0, 3), RadioMaterial.Concrete, "стена"));

        Assert.Single(shortWall.Trace(tx, rx).Paths);
    }

    [Fact]
    public void RayTracing_DopplerAndAntennaPattern()
    {
        const double Speed = 30;
        double wavelength = 299792458.0 / Frequency;

        var scene = new RayTracingScene(Frequency)
            .Add(new PlanarReflector(new Vector3(60, -10, 0), new Vector3(0, 20, 0), new Vector3(0, 0, 10), RadioMaterial.Concrete, "стена за приёмником"));

        var tx = new RadioEndpoint(new Vector3(0, 0, 2));
        var rx = new RadioEndpoint(new Vector3(50, 0, 2)) { Velocity = new Vector3(-Speed, 0, 0) };
        MultipathChannel channel = scene.Trace(tx, rx);

        // Приёмник едет к передатчику: прямой луч укорачивается, отражённый от стены за спиной — удлиняется
        Assert.Equal(Speed / wavelength, channel.Paths.Single(p => p.Kind == PathKind.LineOfSight).DopplerHz, 9);
        Assert.Equal(-Speed / wavelength, channel.Paths.Single(p => p.Kind == PathKind.Reflection).DopplerHz, 9);
        Assert.Equal(Speed / wavelength, channel.MaxDopplerHz, 9);

        // Направленная антенна, повёрнутая спиной к приёмнику, ослабляет прямой луч на отношение вперёд/назад
        var turned = tx with { Pattern = new GaussianPattern { FrontToBackDb = 25 }, BoresightAzimuthDeg = 180 };
        double isotropic = scene.Trace(tx, rx).Paths[0].PowerDb;
        double directional = scene.Trace(turned, rx).Paths[0].PowerDb;

        Assert.Equal(isotropic - 25, directional, 9);
    }

    #endregion

    #region Характеристики канала

    [Fact]
    public void Channel_TwoEqualPaths_HaveKnownSpreadAndCoherence()
    {
        const double Delay = 200e-9, Doppler = 40;
        var channel = new MultipathChannel(
        [
            new PropagationPath(0, 1, Doppler),
            new PropagationPath(Delay, 1, -Doppler)
        ]);

        Assert.Equal(Delay / 2, channel.RmsDelaySpreadSeconds, 15);
        Assert.Equal(Delay / 2, channel.MeanDelaySeconds, 15);

        // |R(Δf)| = |cos(π·Δf·τ)| падает до 0,5 при Δf = 1/(3τ); |R(Δt)| = |cos(2π·f·Δt)| — при Δt = 1/(6f)
        Assert.Equal(1 / (3 * Delay), channel.CoherenceBandwidthHz(), 1e-6 / Delay);
        Assert.Equal(1 / (6 * Doppler), channel.CoherenceTimeSeconds(), 1e-9);

        // Один путь в десять раз сильнее: корреляция не опускается ниже 0,82, канал плоский
        var dominated = new MultipathChannel([new PropagationPath(0, 1), new PropagationPath(Delay, Math.Sqrt(0.1))]);
        Assert.Equal(double.PositiveInfinity, dominated.CoherenceBandwidthHz());
        Assert.Contains(channel.Interpret().Findings, f => f.Contains("частотно-избирательные", StringComparison.Ordinal));
    }

    [Fact]
    public void Channel_FrequencyResponse_IsTheTransformOfImpulseResponse()
    {
        const double SampleRate = 20e6;
        const int Length = 64;
        var channel = new MultipathChannel(
        [
            new PropagationPath(0, new Complex(0.8, 0.1)),
            new PropagationPath(3 / SampleRate, new Complex(-0.3, 0.4)),
            new PropagationPath(11 / SampleRate, new Complex(0.05, -0.2))
        ]);

        Complex[] h = channel.ImpulseResponse(SampleRate, Length);

        for (int m = -Length / 2; m < Length / 2; m++)
        {
            double f = m * SampleRate / Length;
            Complex dft = Complex.Zero;

            for (int n = 0; n < Length; n++)
                dft += h[n] * Complex.FromPolarCoordinates(1, -2 * Math.PI * f * n / SampleRate);

            Assert.True((dft - channel.FrequencyResponse(f)).Magnitude < 1e-12, $"f = {f}");
        }
    }

    [Fact]
    public void Channel_Apply_DelaysRotatesAndFilters()
    {
        const double SampleRate = 1e6;
        var rng = new Random(5);
        Complex[] noise = Enumerable.Range(0, 200).Select(_ => new Complex(rng.NextDouble() - 0.5, rng.NextDouble() - 0.5)).ToArray();

        // Целая задержка — чистый сдвиг
        var delayed = new MultipathChannel([new PropagationPath(3 / SampleRate, new Complex(0, 2))]);
        Complex[] shifted = delayed.Apply(noise, SampleRate, relativeToFirstArrival: false);

        for (int n = 3; n < noise.Length; n++)
            Assert.True((shifted[n] - (new Complex(0, 2) * noise[n - 3])).Magnitude < 1e-12);

        // Доплер вращает фазу постоянного сигнала
        var moving = new MultipathChannel([new PropagationPath(0, 1, 1000)]);
        Complex[] rotated = moving.Apply(Enumerable.Repeat(Complex.One, 100).ToArray(), SampleRate);

        for (int n = 0; n < 100; n++)
            Assert.True((rotated[n] - Complex.FromPolarCoordinates(1, 2 * Math.PI * 1000 * n / SampleRate)).Magnitude < 1e-12);

        // Дробные задержки: гармоника в полосе проходит с множителем H(f)
        var multipath = new MultipathChannel(
        [
            new PropagationPath(0, 1),
            new PropagationPath(2.37 / SampleRate, new Complex(0.4, -0.3)),
            new PropagationPath(7.81 / SampleRate, new Complex(-0.2, 0.1))
        ]);

        double tone = 0.13 * SampleRate;
        Complex[] input = Enumerable.Range(0, 400).Select(n => Complex.FromPolarCoordinates(1, 2 * Math.PI * tone * n / SampleRate)).ToArray();
        Complex[] output = multipath.Apply(input, SampleRate);
        Complex response = multipath.FrequencyResponse(tone);

        for (int n = 64; n < 336; n++)
            Assert.True((output[n] - (response * input[n])).Magnitude < 2e-3, $"n = {n}");
    }

    #endregion

    #region Замирания

    [Fact]
    public void Clarke_EnsembleAutocorrelation_IsBesselJ0()
    {
        const double Doppler = 50;
        const int Processes = 3000;
        double[] lags = [0, 0.002, 0.005, 0.01, 0.02];
        var sums = new Complex[lags.Length];
        var rng = new Random(11);

        for (int p = 0; p < Processes; p++)
        {
            FadingProcess fading = FadingProcess.Rayleigh(Doppler, rng, 16);
            Complex start = fading.Gain(0);

            for (int i = 0; i < lags.Length; i++)
                sums[i] += Complex.Conjugate(start) * fading.Gain(lags[i]);
        }

        for (int i = 0; i < lags.Length; i++)
        {
            Complex mean = sums[i] / Processes;

            Assert.Equal(FadingProcess.Autocorrelation(Doppler, lags[i]), mean.Real, 0.06);
            Assert.Equal(0, mean.Imaginary, 0.06);
        }
    }

    [Fact]
    public void Clarke_Envelope_IsRayleigh_WithRiceLevelCrossings()
    {
        const double Doppler = 10, SampleRate = 1000;
        Complex[] g = FadingProcess.Rayleigh(Doppler, new Random(12), 32).Sample(SampleRate, 500_000);
        double[] power = g.Select(x => (x.Real * x.Real) + (x.Imaginary * x.Imaginary)).ToArray();

        Assert.Equal(1, power.Average(), 0.05);
        Assert.Equal(1 - Math.Exp(-0.1), power.Count(p => p < 0.1) / (double)power.Length, 0.015);
        Assert.Equal(1 - Math.Exp(-1), power.Count(p => p < 1) / (double)power.Length, 0.03);

        double rms = Math.Sqrt(power.Average());
        double duration = power.Length / SampleRate;

        foreach (double rho in new[] { 0.3, 1.0 })
        {
            double level = rho * rms;
            int upCrossings = 0, below = 0;

            for (int i = 1; i < g.Length; i++)
            {
                if (g[i - 1].Magnitude < level && g[i].Magnitude >= level)
                    upCrossings++;

                if (g[i].Magnitude < level)
                    below++;
            }

            double rate = upCrossings / duration;
            Assert.Equal(FadingProcess.LevelCrossingRate(Doppler, rho), rate, 0.07 * FadingProcess.LevelCrossingRate(Doppler, rho));

            double fade = below / SampleRate / upCrossings;
            Assert.Equal(FadingProcess.AverageFadeDurationSeconds(Doppler, rho), fade, 0.1 * FadingProcess.AverageFadeDurationSeconds(Doppler, rho));
        }
    }

    [Fact]
    public void Rician_CarriesLineOfSightAtSevenTenthsOfDoppler()
    {
        const double Doppler = 20, K = 4, SampleRate = 500;
        Complex[] g = FadingProcess.Rician(K, Doppler, new Random(13)).Sample(SampleRate, 200_000);

        // Прямой луч выделяется синхронным накоплением на частоте 0,7·f_D
        Complex los = Complex.Zero;

        for (int n = 0; n < g.Length; n++)
            los += g[n] * Complex.FromPolarCoordinates(1, -2 * Math.PI * 0.7 * Doppler * n / SampleRate);

        Assert.Equal(Math.Sqrt(K / (K + 1)), (los / g.Length).Magnitude, 0.02);
        Assert.Equal(1, g.Average(x => (x.Real * x.Real) + (x.Imaginary * x.Imaginary)), 0.05);
        Assert.All(FadingProcess.LineOfSight(5, new Random(1)).Sample(100, 50), x => Assert.Equal(1, x.Magnitude, 12));
    }

    #endregion

    #region Профили TR 38.901

    [Fact]
    public void TdlProfiles_AreNormalizedToUnitDelaySpread()
    {
        (TappedDelayLineModel Model, int Taps)[] profiles =
        [
            (TappedDelayLineModel.TdlA, 23), (TappedDelayLineModel.TdlB, 23), (TappedDelayLineModel.TdlC, 24),
            (TappedDelayLineModel.TdlD, 14), (TappedDelayLineModel.TdlE, 15)
        ];

        foreach ((TappedDelayLineModel model, int taps) in profiles)
        {
            Assert.Equal(taps, model.Taps.Count);
            Assert.Equal(1, model.NormalizedRmsDelaySpread, 0.01);
            Assert.Equal(300e-9 * model.NormalizedRmsDelaySpread, model.AveragePowerDelayProfile(300e-9).RmsDelaySpreadSeconds, 1e-18);
            Assert.Equal(1, model.AveragePowerDelayProfile(1e-7).TotalPower, 12);
        }

        Assert.Equal(13.3, TappedDelayLineModel.TdlD.FirstTapKFactorDb, 9);
        Assert.Equal(22.0, TappedDelayLineModel.TdlE.FirstTapKFactorDb, 9);
        Assert.True(double.IsNaN(TappedDelayLineModel.TdlA.FirstTapKFactorDb));
    }

    [Fact]
    public void TdlRealization_PreservesAveragePowerProfile()
    {
        TappedDelayLineModel model = TappedDelayLineModel.TdlC;
        var rng = new Random(21);
        int taps = model.Taps.Count;
        var power = new double[taps];
        const int Realizations = 400;

        for (int r = 0; r < Realizations; r++)
        {
            // Пути мгновенного канала и среднего профиля упорядочены по задержке одинаково
            MultipathChannel snapshot = model.Realize(100e-9, 50, rng).Snapshot(0.37);

            for (int i = 0; i < taps; i++)
                power[i] += snapshot.Paths[i].Power;
        }

        MultipathChannel average = model.AveragePowerDelayProfile(100e-9);

        Assert.Equal(1, power.Sum() / Realizations, 0.05);

        // Самый сильный отвод: средняя мощность по реализациям сходится к табличной
        int strongest = Enumerable.Range(0, taps).MaxBy(i => average.Paths[i].Power);
        Assert.Equal(average.Paths[strongest].Power, power[strongest] / Realizations, 0.2 * average.Paths[strongest].Power);
    }

    [Fact]
    public void TdlChannel_Apply_FollowsInstantaneousResponse()
    {
        const double SampleRate = 30.72e6;
        TappedDelayLineChannel channel = TappedDelayLineModel.TdlA.Realize(50e-9, 100, new Random(31));

        double tone = 1.5e6;
        Complex[] input = Enumerable.Range(0, 3000).Select(n => Complex.FromPolarCoordinates(1, 2 * Math.PI * tone * n / SampleRate)).ToArray();
        Complex[] output = channel.Apply(input, SampleRate);

        for (int n = 100; n < 2900; n += 97)
        {
            Complex expected = channel.Snapshot(n / SampleRate).FrequencyResponse(tone) * input[n];
            Assert.True((output[n] - expected).Magnitude < 5e-3, $"n = {n}: {output[n]} против {expected}");
        }
    }

    #endregion

    private static double Simpson(Func<double, double> f, double upper)
    {
        const int Intervals = 20000;
        double h = upper / Intervals;
        double sum = f(0) + f(upper);

        for (int i = 1; i < Intervals; i++)
            sum += (i % 2 == 1 ? 4 : 2) * f(i * h);

        return sum * h / 3;
    }
}
