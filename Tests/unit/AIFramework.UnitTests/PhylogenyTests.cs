using AI.Biology.Phylogeny;
using Xunit;

namespace AIFramework.UnitTests;

/// <summary>
/// Филогения проверяется обратными задачами и перебором: расстояния снимаются со случайного дерева
/// и по ним дерево восстанавливается; экономия и правдоподобие сверяются с перебором всех состояний
/// внутренних узлов, причём вероятности перехода в эталоне считаются экспонентой матрицы скоростей,
/// а не формулой из библиотеки.
/// </summary>
public class PhylogenyTests
{
    #region Расстояния

    [Fact]
    public void Distances_KnownValuesAndIdentities()
    {
        // −¾·ln(1 − 0,4) = 0,383119…
        Assert.Equal(0.3831192, EvolutionaryDistance.JukesCantor(0.3), 6);
        Assert.Equal(double.PositiveInfinity, EvolutionaryDistance.JukesCantor(0.75));

        // Кимура совпадает с Джуксом — Кантором, когда транзиции — ровно треть различий
        for (double p = 0.05; p < 0.7; p += 0.05)
            Assert.Equal(EvolutionaryDistance.JukesCantor(p), EvolutionaryDistance.Kimura(p / 3, 2 * p / 3), 12);

        SiteComparison comparison = EvolutionaryDistance.CompareNucleotides("ACGT-N", "GCTTAA");
        Assert.Equal(new SiteComparison(4, 2, 1, 1), comparison);

        // У белков N — аспарагин, а не неизвестный нуклеотид
        Assert.Equal(-Math.Log(0.5), EvolutionaryDistance.Poisson("NNXX", "NDAA"), 12);
        _ = Assert.Throws<ArgumentException>(() => EvolutionaryDistance.JukesCantor("ACGE", "ACGT"));
    }

    [Fact]
    public void SimulatedSequences_GiveTreeDistances()
    {
        // Смоделированная эволюция и формула Джукса — Кантора должны вернуть длины путей по дереву
        PhylogeneticTree tree = PhylogeneticTree.ParseNewick("((A:0.1,B:0.25):0.05,C:0.3,D:0.15);");
        IReadOnlyDictionary<string, string> alignment = SequenceEvolution.Simulate(tree, 40_000, new Random(3));
        DistanceMatrix truth = tree.PatristicDistances();

        foreach (string a in truth.Labels)
            foreach (string b in truth.Labels.Where(b => string.CompareOrdinal(a, b) < 0))
                Assert.Equal(truth[a, b], EvolutionaryDistance.JukesCantor(alignment[a], alignment[b]), 0.02);
    }

    [Fact]
    public void Kimura_CorrectsForTransitionBias()
    {
        var tree = PhylogeneticTree.ParseNewick("(A:0.2,B:0.2);");
        IReadOnlyDictionary<string, string> alignment =
            SequenceEvolution.Simulate(tree, 60_000, new Random(8), NucleotideModel.Kimura(6));

        double kimura = EvolutionaryDistance.Kimura(alignment["A"], alignment["B"]);
        double jukesCantor = EvolutionaryDistance.JukesCantor(alignment["A"], alignment["B"]);

        Assert.Equal(0.4, kimura, 0.02);
        Assert.True(jukesCantor < kimura - 0.005, $"JC {jukesCantor} должен занижать при избытке транзиций, K80 {kimura}");
    }

    #endregion

    #region Построение деревьев

    [Fact]
    public void NeighborJoining_RecoversTreeFromAdditiveDistances()
    {
        var rng = new Random(11);

        for (int trial = 0; trial < 40; trial++)
        {
            PhylogeneticTree truth = RandomUnrooted(rng, 4 + rng.Next(9));
            DistanceMatrix distances = truth.PatristicDistances();

            Assert.True(distances.IsAdditive(1e-9));

            PhylogeneticTree built = TreeBuilder.NeighborJoining(distances);

            Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(truth, built));
            AssertSameDistances(distances, built.PatristicDistances(), 1e-9);
        }
    }

    [Fact]
    public void Upgma_RecoversUltrametricTree()
    {
        var rng = new Random(12);

        for (int trial = 0; trial < 40; trial++)
        {
            PhylogeneticTree truth = RandomUltrametric(rng, 3 + rng.Next(10));
            DistanceMatrix distances = truth.PatristicDistances();

            Assert.True(distances.IsUltrametric(1e-9));

            PhylogeneticTree built = TreeBuilder.Upgma(distances);

            Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(truth, built));
            AssertSameDistances(distances, built.PatristicDistances(), 1e-9);
        }
    }

    [Fact]
    public void Upgma_IsFooledByUnequalRates_NeighborJoiningIsNot()
    {
        // У B и D длинные ветви: по расстоянию A ближе к C, чем к своему соседу B
        PhylogeneticTree truth = PhylogeneticTree.ParseNewick("((A:1,B:10):1,(C:1,D:10):1);");
        DistanceMatrix distances = truth.PatristicDistances();

        Assert.False(distances.IsUltrametric());
        Assert.Equal(2, PhylogeneticTree.RobinsonFoulds(truth, TreeBuilder.Upgma(distances)));
        Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(truth, TreeBuilder.NeighborJoining(distances)));
    }

    [Fact]
    public void SaturatedDistances_AreRejected()
    {
        DistanceMatrix saturated = DistanceMatrix.FromSequences(["a", "b", "c"], ["AAAA", "CCCC", "AACC"]);

        Assert.False(saturated.IsFinite);
        _ = Assert.Throws<ArgumentException>(() => TreeBuilder.NeighborJoining(saturated));
    }

    #endregion

    #region Newick и сравнение деревьев

    [Fact]
    public void Newick_RoundTripsNamesLengthsAndComments()
    {
        const string text = "((A:0.1,B:0.2)ab:0.05,(C:0.3,'D e':0.4)[комментарий]:0.06,E:0.7);";
        PhylogeneticTree tree = PhylogeneticTree.ParseNewick(text);

        Assert.Equal(["A", "B", "C", "D e", "E"], tree.LeafNames);
        Assert.NotNull(tree.Find("ab"));
        Assert.Equal(1.81, tree.TotalLength, 12);

        PhylogeneticTree again = PhylogeneticTree.ParseNewick(tree.ToNewick());

        Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(tree, again));
        AssertSameDistances(tree.PatristicDistances(), again.PatristicDistances(), 1e-12);

        _ = Assert.Throws<FormatException>(() => PhylogeneticTree.ParseNewick("((A,B);"));
        _ = Assert.Throws<FormatException>(() => PhylogeneticTree.ParseNewick("(A,B)C) ;"));
    }

    [Fact]
    public void RobinsonFoulds_IgnoresRootAndChildOrder()
    {
        PhylogeneticTree first = PhylogeneticTree.ParseNewick("((A,B),(C,D),E);");
        PhylogeneticTree second = PhylogeneticTree.ParseNewick("(E,((D,C),(B,A)));");
        PhylogeneticTree third = PhylogeneticTree.ParseNewick("((A,C),(B,D),E);");

        Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(first, second));
        Assert.Equal(4, PhylogeneticTree.RobinsonFoulds(first, third));
    }

    #endregion

    #region Экономия и правдоподобие

    [Fact]
    public void Parsimony_MatchesExhaustiveSearch()
    {
        var rng = new Random(21);

        for (int trial = 0; trial < 30; trial++)
        {
            PhylogeneticTree tree = trial % 2 == 0 ? RandomUnrooted(rng, 4 + rng.Next(3)) : RandomUltrametric(rng, 4 + rng.Next(3));
            var alignment = tree.LeafNames.ToDictionary(n => n, _ => RandomColumnText(rng, 6, "ACGT-"));
            int[] scores = Parsimony.SiteScores(tree, alignment);

            for (int site = 0; site < 6; site++)
                Assert.Equal(BruteParsimony(tree, alignment, site), scores[site]);
        }
    }

    [Theory]
    [InlineData(1.0)]
    [InlineData(4.0)]
    public void Likelihood_MatchesSumOverAllInternalStates(double kappa)
    {
        var rng = new Random(31);
        NucleotideModel model = kappa == 1 ? NucleotideModel.JukesCantor : NucleotideModel.Kimura(kappa);

        for (int trial = 0; trial < 8; trial++)
        {
            PhylogeneticTree tree = trial % 2 == 0 ? RandomUnrooted(rng, 5) : RandomUltrametric(rng, 4);
            var alignment = tree.LeafNames.ToDictionary(n => n, _ => RandomColumnText(rng, 12, "ACGTTN-"));
            double[] sites = TreeLikelihood.SiteLogLikelihoods(tree, alignment, model);

            for (int site = 0; site < 12; site++)
                Assert.Equal(Math.Log(BruteSiteLikelihood(tree, alignment, site, kappa)), sites[site], 10);
        }
    }

    [Fact]
    public void Likelihood_TwoTaxaMatchClosedForm_AndIgnoresRootPosition()
    {
        PhylogeneticTree pair = PhylogeneticTree.ParseNewick("(A:0.1,B:0.2);");
        double decay = Math.Exp(-4 * 0.3 / 3);
        double[] sites = TreeLikelihood.SiteLogLikelihoods(pair, new Dictionary<string, string> { ["A"] = "AC", ["B"] = "AG" });

        Assert.Equal(Math.Log(0.25 * (0.25 + (0.75 * decay))), sites[0], 12);
        Assert.Equal(Math.Log(0.25 * (0.25 - (0.25 * decay))), sites[1], 12);

        // Принцип шкива: при обратимой модели корень можно перенести в любой внутренний узел
        var rng = new Random(41);
        PhylogeneticTree tree = RandomUnrooted(rng, 7);
        var alignment = tree.LeafNames.ToDictionary(n => n, _ => RandomColumnText(rng, 50, "ACGT"));
        double before = TreeLikelihood.LogLikelihood(tree, alignment, NucleotideModel.Kimura(3));

        foreach (TreeNode inner in tree.Clone().PreOrder().Where(n => !n.IsLeaf).Skip(1).Take(3).ToList())
        {
            PhylogeneticTree rerooted = tree.Clone();
            TreeNode target = rerooted.PreOrder().First(n => !n.IsLeaf && SameLeaves(n, inner));
            rerooted.RootAt(target);

            Assert.Equal(before, TreeLikelihood.LogLikelihood(rerooted, alignment, NucleotideModel.Kimura(3)), 9);
            Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(tree, rerooted));
        }
    }

    [Fact]
    public void MaximumLikelihood_RecoversBranchLengthsAndPrefersTrueTopology()
    {
        PhylogeneticTree truth = PhylogeneticTree.ParseNewick("((A:0.1,B:0.2):0.08,C:0.15,D:0.3);");
        IReadOnlyDictionary<string, string> alignment = SequenceEvolution.Simulate(truth, 20_000, new Random(51));

        PhylogeneticTree start = PhylogeneticTree.ParseNewick("((A:0.5,B:0.5):0.5,C:0.5,D:0.5);");
        BranchLengthFit fit = TreeLikelihood.OptimizeBranchLengths(start, alignment);

        foreach (string leaf in new[] { "A", "B", "C", "D" })
            Assert.Equal(truth.Find(leaf)!.BranchLength, fit.Tree.Find(leaf)!.BranchLength, 0.02);

        Assert.Equal(0.08, fit.Tree.Root.Children[0].BranchLength, 0.02);

        foreach (string wrong in new[] { "((A,C),B,D);", "((A,D),B,C);" })
        {
            BranchLengthFit other = TreeLikelihood.OptimizeBranchLengths(PhylogeneticTree.ParseNewick(wrong), alignment);
            Assert.True(fit.LogLikelihood > other.LogLikelihood + 10, $"{wrong}: {other.LogLikelihood} против {fit.LogLikelihood}");
        }
    }

    [Fact]
    public void Bootstrap_FullySupportsTrueSplitsOnLongAlignment()
    {
        PhylogeneticTree truth = PhylogeneticTree.ParseNewick("(((A:0.1,B:0.1):0.06,C:0.15):0.06,(D:0.1,E:0.12):0.06,F:0.2);");
        IReadOnlyDictionary<string, string> alignment = SequenceEvolution.Simulate(truth, 1500, new Random(61));
        string[] labels = alignment.Keys.ToArray();

        BootstrapResult result = Bootstrap.DistanceTree(
            labels, labels.Select(l => alignment[l]).ToArray(), TreeBuilder.NeighborJoining,
            DistanceModel.JukesCantor, 100, new Random(62));

        Assert.Equal(0, PhylogeneticTree.RobinsonFoulds(truth, result.Tree));
        Assert.All(result.SplitSupport.Values, support => Assert.True(support >= 0.9, $"поддержка {support}"));
        Assert.Contains(result.Interpret().Warnings, w => w.Contains("не вероятность", StringComparison.Ordinal));
    }

    #endregion

    #region Инструменты

    private static double Length(Random rng) => 0.02 + (rng.NextDouble() * 0.5);

    private static PhylogeneticTree RandomUnrooted(Random rng, int leaves)
    {
        List<string> names = Enumerable.Range(0, leaves).Select(i => $"t{i}").OrderBy(_ => rng.Next()).ToList();
        int first = rng.Next(1, leaves - 1);
        int second = rng.Next(first + 1, leaves);
        var root = new TreeNode();

        root.AddChild(Subtree(rng, names[..first]), Length(rng));
        root.AddChild(Subtree(rng, names[first..second]), Length(rng));
        root.AddChild(Subtree(rng, names[second..]), Length(rng));

        return new PhylogeneticTree(root);
    }

    private static TreeNode Subtree(Random rng, List<string> names)
    {
        if (names.Count == 1)
            return new TreeNode(names[0]);

        List<string> shuffled = names.OrderBy(_ => rng.Next()).ToList();
        int cut = rng.Next(1, shuffled.Count);
        var node = new TreeNode();

        node.AddChild(Subtree(rng, shuffled[..cut]), Length(rng));
        node.AddChild(Subtree(rng, shuffled[cut..]), Length(rng));

        return node;
    }

    private static PhylogeneticTree RandomUltrametric(Random rng, int leaves)
    {
        var clusters = Enumerable.Range(0, leaves).Select(i => (Node: new TreeNode($"u{i}"), Height: 0.0)).ToList();
        double height = 0;

        while (clusters.Count > 1)
        {
            int i = rng.Next(clusters.Count);
            int j = (i + 1 + rng.Next(clusters.Count - 1)) % clusters.Count;
            height += 0.05 + (rng.NextDouble() * 0.3);

            var node = new TreeNode();
            node.AddChild(clusters[i].Node, height - clusters[i].Height);
            node.AddChild(clusters[j].Node, height - clusters[j].Height);

            clusters.RemoveAt(Math.Max(i, j));
            clusters.RemoveAt(Math.Min(i, j));
            clusters.Add((node, height));
        }

        return new PhylogeneticTree(clusters[0].Node);
    }

    private static string RandomColumnText(Random rng, int length, string alphabet)
        => new(Enumerable.Range(0, length).Select(_ => alphabet[rng.Next(alphabet.Length)]).ToArray());

    private static bool SameLeaves(TreeNode a, TreeNode b)
        => Below(a).SetEquals(Below(b));

    private static HashSet<string> Below(TreeNode node)
    {
        var names = new HashSet<string>();
        var stack = new Stack<TreeNode>([node]);

        while (stack.Count > 0)
        {
            TreeNode current = stack.Pop();

            if (current.IsLeaf)
                names.Add(current.Name!);

            foreach (TreeNode child in current.Children)
                stack.Push(child);
        }

        return names;
    }

    private static void AssertSameDistances(DistanceMatrix expected, DistanceMatrix actual, double tolerance)
    {
        foreach (string a in expected.Labels)
            foreach (string b in expected.Labels)
                Assert.Equal(expected[a, b], actual[a, b], tolerance);
    }

    /// <summary>Перебор состояний внутренних узлов: наименьшее число рёбер с разными концами</summary>
    private static int BruteParsimony(PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, int site)
    {
        List<TreeNode> internals = tree.PreOrder().Where(n => !n.IsLeaf).ToList();
        List<char> states = alignment.Values.Select(s => s[site]).Where(c => c != '-').Distinct().ToList();

        if (states.Count == 0)
            return 0;

        int combinations = (int)Math.Pow(states.Count, internals.Count);
        var assigned = new Dictionary<TreeNode, char>();
        int best = int.MaxValue;

        for (int code = 0; code < combinations; code++)
        {
            int rest = code;

            foreach (TreeNode node in internals)
            {
                assigned[node] = states[rest % states.Count];
                rest /= states.Count;
            }

            int cost = 0;

            foreach (TreeNode node in tree.PreOrder().Where(n => n.Parent is not null))
            {
                char parent = assigned[node.Parent!];
                char own = node.IsLeaf ? alignment[node.Name!][site] : assigned[node];

                if (own != '-' && own != parent)
                    cost++;
            }

            best = Math.Min(best, cost);
        }

        return best;
    }

    /// <summary>Сумма по всем состояниям внутренних узлов с вероятностями из экспоненты матрицы скоростей</summary>
    private static double BruteSiteLikelihood(PhylogeneticTree tree, IReadOnlyDictionary<string, string> alignment, int site, double kappa)
    {
        List<TreeNode> internals = tree.PreOrder().Where(n => !n.IsLeaf).ToList();
        var matrices = tree.PreOrder().Where(n => n.Parent is not null)
            .ToDictionary(n => n, n => Exponential(kappa, n.BranchLength));

        var assigned = new Dictionary<TreeNode, int>();
        int combinations = 1 << (2 * internals.Count);
        double total = 0;

        for (int code = 0; code < combinations; code++)
        {
            for (int k = 0; k < internals.Count; k++)
                assigned[internals[k]] = (code >> (2 * k)) & 3;

            double product = 0.25;

            foreach (TreeNode node in tree.PreOrder().Where(n => n.Parent is not null))
            {
                int from = assigned[node.Parent!];

                if (node.IsLeaf)
                {
                    int to = "ACGT".IndexOf(alignment[node.Name!][site]);

                    if (to >= 0)
                        product *= matrices[node][from, to];
                }
                else
                {
                    product *= matrices[node][from, assigned[node]];
                }
            }

            total += product;
        }

        return total;
    }

    /// <summary>exp(Q·t) масштабированием и возведением в квадрат ряда Тейлора</summary>
    private static double[,] Exponential(double kappa, double time)
    {
        double beta = 1 / (kappa + 2);
        double alpha = kappa / (kappa + 2);
        var rate = new double[4, 4];

        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                rate[x, y] = x == y ? -(alpha + (2 * beta)) : (x ^ y) == 2 ? alpha : beta;

        int squarings = 0;
        double scaled = time;

        while (scaled * 2 > 0.01)
        {
            scaled /= 2;
            squarings++;
        }

        var result = Identity();
        var term = Identity();

        for (int k = 1; k <= 14; k++)
        {
            term = Multiply(term, rate);

            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    term[x, y] *= scaled / k;

            for (int x = 0; x < 4; x++)
                for (int y = 0; y < 4; y++)
                    result[x, y] += term[x, y];
        }

        for (int s = 0; s < squarings; s++)
            result = Multiply(result, result);

        return result;
    }

    private static double[,] Identity()
    {
        var identity = new double[4, 4];

        for (int x = 0; x < 4; x++)
            identity[x, x] = 1;

        return identity;
    }

    private static double[,] Multiply(double[,] a, double[,] b)
    {
        var product = new double[4, 4];

        for (int x = 0; x < 4; x++)
            for (int y = 0; y < 4; y++)
                for (int k = 0; k < 4; k++)
                    product[x, y] += a[x, k] * b[k, y];

        return product;
    }

    #endregion
}
