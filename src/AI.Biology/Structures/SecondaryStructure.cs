using AI.Geometry.Primitives;
using AI.Insights;

namespace AI.Biology.Structures;

/// <summary>Водородная связь основной цепи: C=O одного остатка и N–H другого</summary>
/// <param name="Acceptor">Номер остатка, чей карбонил принимает связь</param>
/// <param name="Donor">Номер остатка, чья амидная группа её отдаёт</param>
/// <param name="Energy">Электростатическая энергия, ккал/моль; NaN, если связь задана без геометрии</param>
public readonly record struct HydrogenBond(int Acceptor, int Donor, double Energy);

/// <summary>
/// Вторичная структура белка по остаткам в кодах DSSP
/// </summary>
/// <remarks>
/// Коды: H — α-спираль, G — спираль 3₁₀, I — π-спираль, E — остаток β-лестницы, B — изолированный
/// β-мостик, T — поворот, S — изгиб, «-» — всё остальное.
/// </remarks>
public sealed class SecondaryStructureAssignment : IInterpretable
{
    internal SecondaryStructureAssignment(string codes, IReadOnlyList<HydrogenBond> hydrogenBonds)
    {
        Codes = codes;
        HydrogenBonds = hydrogenBonds;
    }

    /// <summary>Коды по остаткам</summary>
    public string Codes { get; }

    /// <summary>Найденные водородные связи основной цепи</summary>
    public IReadOnlyList<HydrogenBond> HydrogenBonds { get; }

    /// <summary>Число остатков</summary>
    public int Count => Codes.Length;

    /// <summary>Доля остатков с заданным кодом</summary>
    /// <param name="code">Код DSSP</param>
    public double Fraction(char code) => Count == 0 ? 0 : (double)Codes.Count(c => c == code) / Count;

    /// <summary>Доля остатков в спиралях всех трёх видов</summary>
    public double HelixFraction => Count == 0 ? 0 : (double)Codes.Count(c => c is 'H' or 'G' or 'I') / Count;

    /// <summary>Доля остатков в β-структуре</summary>
    public double StrandFraction => Count == 0 ? 0 : (double)Codes.Count(c => c is 'E' or 'B') / Count;

    /// <summary>Сплошные участки одного кода, кроме «-»: код, первый и последний остаток</summary>
    public IReadOnlyList<(char Code, int Start, int End)> Segments()
    {
        var segments = new List<(char, int, int)>();
        int start = 0;

        for (int i = 1; i <= Count; i++)
        {
            if (i < Count && Codes[i] == Codes[start])
                continue;

            if (Codes[start] != '-')
                segments.Add((Codes[start], start, i - 1));

            start = i;
        }

        return segments;
    }

    /// <inheritdoc />
    public Interpretation Interpret()
    {
        (char code, int start, int end) = Segments().OrderByDescending(s => s.End - s.Start).FirstOrDefault();
        bool hasSegment = Count > 0 && code != default;

        return new InterpretationBuilder("Вторичная структура (DSSP)")
            .Summary(Count == 0
                ? "Остатков нет."
                : $"Остатков {Count}: в спиралях {Fmt.Pct(HelixFraction)}, в β-структуре {Fmt.Pct(StrandFraction)}; "
                  + $"водородных связей основной цепи {HydrogenBonds.Count}.")
            .Metric("α-спираль (H)", Fmt.Pct(Fraction('H')), null, "две подряд связи i → i+4")
            .Metric("Спираль 3₁₀ (G)", Fmt.Pct(Fraction('G')), null, "две подряд связи i → i+3")
            .Metric("π-спираль (I)", Fmt.Pct(Fraction('I')), null, "две подряд связи i → i+5")
            .Metric("β-лестница (E)", Fmt.Pct(Fraction('E')), null, "два и более смежных β-мостика")
            .Metric("Изолированный мостик (B)", Fmt.Pct(Fraction('B')), null, null)
            .Metric("Поворот (T)", Fmt.Pct(Fraction('T')), null, "одиночная связь i → i+3…5")
            .Metric("Изгиб (S)", Fmt.Pct(Fraction('S')), null, "угол Cα(i−2)–Cα(i)–Cα(i+2) больше 70°")
            .Metric("Водородных связей", HydrogenBonds.Count, null, "с энергией ниже −0,5 ккал/моль",
                MetricQuality.Unknown, 0)
            .FindingIf(hasSegment,
                $"Самый длинный участок — «{code}» из {end - start + 1} остатков, с {start + 1}-го по {end + 1}-й.")
            .Warning("Правила DSSP 1983 года без β-выпячиваний: лестница, разорванная выпячиванием, "
                + "даёт два участка E или отдельные мостики B. Приоритеты классические: H выше B и E, "
                + "а π-спираль ниже 3₁₀; в DSSP 2.1 и новее π-спираль поставлена выше α.")
            .Warning("Водород амидной группы ставится, как в DSSP, по направлению связи C=O предыдущего "
                + "остатка. У пролина и у первого остатка цепи водорода нет, и донорами они не бывают.")
            .Build();
    }

    /// <summary>Коды по остаткам</summary>
    public override string ToString() => Codes;
}

/// <summary>
/// Определение вторичной структуры по водородным связям основной цепи — правила DSSP
/// Кабша и Сандера (1983).
/// </summary>
/// <remarks>
/// <para>
/// Вторичную структуру определяют не углы, а водородные связи: у α-спирали карбонил остатка i
/// связан с амидной группой остатка i + 4, у β-листа связи идут поперёк между цепями. Энергия
/// связи считается по электростатической модели DSSP с зарядами ±0,42e на C=O и ±0,20e на N–H:
/// <c>E = 0,084·332·(1/r(ON) + 1/r(CH) − 1/r(OH) − 1/r(CN))</c> ккал/моль, и связь есть при E &lt; −0,5.
/// </para>
/// <para>
/// n-поворот в остатке i — связь i → i + n при n = 3, 4, 5. Два подряд 4-поворота в i − 1 и i
/// дают α-спираль на остатках i…i + 3, так же 3- и 5-повороты — спирали 3₁₀ и π. Мостик между
/// остатками i и j параллелен, если есть связи (i−1 → j и j → i+1) либо (j−1 → i и i → j+1),
/// и антипараллелен при (i → j и j → i) либо (i−1 → j+1 и j−1 → i+1). Смежные мостики одного вида
/// образуют лестницу — код E; одиночный мостик — B.
/// </para>
/// </remarks>
public static class SecondaryStructure
{
    /// <summary>Порог энергии водородной связи, ккал/моль</summary>
    public const double HydrogenBondCutoff = -0.5;

    private const double Coupling = 0.42 * 0.20 * 332;
    private const double LowestEnergy = -9.9;
    private const double BendAngle = 70;
    private const double NeighbourCutoff = 9.0;

    /// <summary>Определяет вторичную структуру по координатам основной цепи</summary>
    /// <param name="protein">Белок</param>
    public static SecondaryStructureAssignment Assign(ProteinStructure protein)
    {
        ArgumentNullException.ThrowIfNull(protein);

        int n = protein.Count;
        IReadOnlyList<ProteinResidue> residues = protein.Residues;
        var linked = new bool[n];

        for (int i = 0; i < n; i++)
            linked[i] = protein.IsLinked(i);

        var hydrogen = new Vector3?[n];

        for (int i = 1; i < n; i++)
        {
            ProteinResidue current = residues[i];
            ProteinResidue previous = residues[i - 1];

            if (!linked[i - 1] || current.N < 0 || previous.C < 0 || previous.O < 0 || current.Name == "PRO")
                continue;

            Vector3 carbonyl = protein.Position(previous.C) - protein.Position(previous.O);
            hydrogen[i] = protein.Position(current.N) + (carbonyl / carbonyl.Length);
        }

        var bonds = new List<HydrogenBond>();

        for (int acceptor = 0; acceptor < n; acceptor++)
        {
            ProteinResidue a = residues[acceptor];

            if (a.C < 0 || a.O < 0 || a.CAlpha < 0)
                continue;

            for (int donor = 0; donor < n; donor++)
            {
                if (Math.Abs(acceptor - donor) < 2 || hydrogen[donor] is not Vector3 h)
                    continue;

                ProteinResidue d = residues[donor];

                if (d.CAlpha < 0 || protein.Position(a.CAlpha).DistanceTo(protein.Position(d.CAlpha)) > NeighbourCutoff)
                    continue;

                double energy = Energy(protein.Position(a.O), protein.Position(a.C), protein.Position(d.N), h);

                if (energy < HydrogenBondCutoff)
                    bonds.Add(new HydrogenBond(acceptor, donor, energy));
            }
        }

        var bends = new bool[n];

        for (int i = 2; i + 2 < n; i++)
        {
            int before = residues[i - 2].CAlpha;
            int middle = residues[i].CAlpha;
            int after = residues[i + 2].CAlpha;

            if (before < 0 || middle < 0 || after < 0 || !linked[i - 2] || !linked[i - 1] || !linked[i] || !linked[i + 1])
                continue;

            Vector3 incoming = protein.Position(middle) - protein.Position(before);
            Vector3 outgoing = protein.Position(after) - protein.Position(middle);

            bends[i] = incoming.AngleTo(outgoing) > BendAngle;
        }

        return Build(n, bonds, linked, bends);
    }

    /// <summary>
    /// Определяет вторичную структуру по готовому списку водородных связей — например, найденных
    /// в траектории молекулярной динамики
    /// </summary>
    /// <param name="residueCount">Число остатков</param>
    /// <param name="hydrogenBonds">Связи: карбонил какого остатка связан с амидом какого</param>
    /// <param name="linked">Связан ли остаток со следующим; по умолчанию цепь без разрывов</param>
    public static SecondaryStructureAssignment Assign(
        int residueCount, IEnumerable<(int Acceptor, int Donor)> hydrogenBonds, IReadOnlyList<bool>? linked = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(residueCount);
        ArgumentNullException.ThrowIfNull(hydrogenBonds);

        var bonds = new List<HydrogenBond>();

        foreach ((int acceptor, int donor) in hydrogenBonds)
        {
            if (acceptor < 0 || acceptor >= residueCount || donor < 0 || donor >= residueCount || acceptor == donor)
                throw new ArgumentException($"Связь {acceptor} → {donor} выходит за пределы цепи", nameof(hydrogenBonds));

            bonds.Add(new HydrogenBond(acceptor, donor, double.NaN));
        }

        bool[] chain;

        if (linked is null)
        {
            chain = Enumerable.Repeat(true, residueCount).ToArray();

            if (residueCount > 0)
                chain[^1] = false;
        }
        else
        {
            if (linked.Count != residueCount)
                throw new ArgumentException("Признаков связности должно быть по числу остатков", nameof(linked));

            chain = linked.ToArray();
        }

        return Build(residueCount, bonds, chain, null);
    }

    private static double Energy(Vector3 oxygen, Vector3 carbon, Vector3 nitrogen, Vector3 hydrogen)
    {
        double on = oxygen.DistanceTo(nitrogen);
        double ch = carbon.DistanceTo(hydrogen);
        double oh = oxygen.DistanceTo(hydrogen);
        double cn = carbon.DistanceTo(nitrogen);

        if (Math.Min(Math.Min(on, ch), Math.Min(oh, cn)) < 0.5)
            return LowestEnergy;

        return Math.Max(LowestEnergy, Coupling * ((1 / on) + (1 / ch) - (1 / oh) - (1 / cn)));
    }

    private static SecondaryStructureAssignment Build(int n, List<HydrogenBond> bonds, bool[] linked, bool[]? bends)
    {
        var present = new HashSet<(int, int)>(bonds.Select(b => (b.Acceptor, b.Donor)));

        bool Bond(int acceptor, int donor) => acceptor >= 0 && donor >= 0 && acceptor < n && donor < n
            && present.Contains((acceptor, donor));

        bool Contiguous(int from, int to)
        {
            if (from < 0 || to >= n)
                return false;

            for (int k = from; k < to; k++)
            {
                if (!linked[k])
                    return false;
            }

            return true;
        }

        var turns = new bool[6][];

        for (int size = 3; size <= 5; size++)
        {
            turns[size] = new bool[n];

            for (int i = 0; i + size < n; i++)
                turns[size][i] = Contiguous(i, i + size) && Bond(i, i + size);
        }

        var codes = Enumerable.Repeat('-', n).ToArray();

        void Mark(int from, int length, char code)
        {
            for (int k = from; k < from + length && k < n; k++)
            {
                if (codes[k] == '-')
                    codes[k] = code;
            }
        }

        // Приоритет DSSP: H, B, E, G, I, T, S — метка ставится, только если остаток ещё свободен
        for (int i = 1; i < n; i++)
        {
            if (turns[4][i - 1] && turns[4][i])
                Mark(i, 4, 'H');
        }

        var bridges = new HashSet<(int I, int J, bool Parallel)>();

        for (int i = 1; i + 1 < n; i++)
        {
            for (int j = i + 3; j + 1 < n; j++)
            {
                if (!Contiguous(i - 1, i + 1) || !Contiguous(j - 1, j + 1))
                    continue;

                if ((Bond(i - 1, j) && Bond(j, i + 1)) || (Bond(j - 1, i) && Bond(i, j + 1)))
                    bridges.Add((i, j, true));

                if ((Bond(i, j) && Bond(j, i)) || (Bond(i - 1, j + 1) && Bond(j - 1, i + 1)))
                    bridges.Add((i, j, false));
            }
        }

        var ladder = new bool[n];
        var isolated = new bool[n];

        foreach ((int i, int j, bool parallel) in bridges)
        {
            bool neighbour = parallel
                ? bridges.Contains((i + 1, j + 1, true)) || bridges.Contains((i - 1, j - 1, true))
                : bridges.Contains((i + 1, j - 1, false)) || bridges.Contains((i - 1, j + 1, false));

            if (neighbour)
            {
                ladder[i] = true;
                ladder[j] = true;
            }
            else
            {
                isolated[i] = true;
                isolated[j] = true;
            }
        }

        for (int k = 0; k < n; k++)
        {
            if (codes[k] == '-' && isolated[k] && !ladder[k])
                codes[k] = 'B';
        }

        for (int k = 0; k < n; k++)
        {
            if (codes[k] == '-' && ladder[k])
                codes[k] = 'E';
        }

        for (int i = 1; i < n; i++)
        {
            if (turns[3][i - 1] && turns[3][i])
                Mark(i, 3, 'G');
        }

        for (int i = 1; i < n; i++)
        {
            if (turns[5][i - 1] && turns[5][i])
                Mark(i, 5, 'I');
        }

        for (int size = 3; size <= 5; size++)
        {
            for (int i = 0; i < n; i++)
            {
                if (turns[size][i])
                    Mark(i + 1, size - 1, 'T');
            }
        }

        if (bends is not null)
        {
            for (int k = 0; k < n; k++)
            {
                if (codes[k] == '-' && bends[k])
                    codes[k] = 'S';
            }
        }

        return new SecondaryStructureAssignment(new string(codes), bonds);
    }
}
