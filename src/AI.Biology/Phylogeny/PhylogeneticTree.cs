using System.Globalization;
using System.Text;

namespace AI.Biology.Phylogeny;

/// <summary>Узел филогенетического дерева</summary>
public sealed class TreeNode
{
    private readonly List<TreeNode> _children = [];

    /// <summary>Создаёт узел</summary>
    /// <param name="name">Имя; у листьев — имя таксона</param>
    public TreeNode(string? name = null) => Name = name;

    /// <summary>Имя узла</summary>
    public string? Name { get; set; }

    /// <summary>Длина ветви к родителю — ожидаемое число замен на позицию</summary>
    public double BranchLength { get; set; }

    /// <summary>Родитель; null у корня</summary>
    public TreeNode? Parent { get; private set; }

    /// <summary>Потомки</summary>
    public IReadOnlyList<TreeNode> Children => _children;

    /// <summary>Лист ли это</summary>
    public bool IsLeaf => _children.Count == 0;

    /// <summary>Присоединяет потомка</summary>
    /// <param name="child">Узел без родителя</param>
    /// <param name="branchLength">Длина ветви</param>
    public TreeNode AddChild(TreeNode child, double branchLength = 0)
    {
        ArgumentNullException.ThrowIfNull(child);

        if (child.Parent is not null)
            throw new InvalidOperationException("У узла уже есть родитель");

        for (TreeNode? ancestor = this; ancestor is not null; ancestor = ancestor.Parent)
        {
            if (ReferenceEquals(ancestor, child))
                throw new InvalidOperationException("Узел нельзя сделать потомком собственного потомка");
        }

        child.Parent = this;
        child.BranchLength = branchLength;
        _children.Add(child);

        return child;
    }

    internal void Detach(TreeNode child)
    {
        if (_children.Remove(child))
            child.Parent = null;
    }

    /// <summary>Имя узла</summary>
    public override string ToString() => Name ?? (IsLeaf ? "лист" : "узел");
}

/// <summary>
/// Филогенетическое дерево: топология, длины ветвей, запись Newick.
/// </summary>
/// <remarks>
/// <para>
/// Дерево хранится укоренённым, но большинство методов реконструкции дают некорневое дерево:
/// положение корня данные о заменах не определяют. Такое дерево записывается с корнем в узле
/// степени три — так делают PHYLIP и большинство программ. Сравнение топологий
/// (<see cref="RobinsonFoulds"/>) идёт по разбиениям листьев и от положения корня не зависит.
/// </para>
/// <para>
/// Для разбиений, расстояний и сравнения имена листьев должны быть различными и непустыми.
/// </para>
/// </remarks>
public sealed class PhylogeneticTree
{
    /// <summary>Создаёт дерево</summary>
    /// <param name="root">Корень — узел без родителя</param>
    public PhylogeneticTree(TreeNode root)
    {
        ArgumentNullException.ThrowIfNull(root);

        if (root.Parent is not null)
            throw new ArgumentException("У корня не должно быть родителя", nameof(root));

        Root = root;
    }

    /// <summary>Корень</summary>
    public TreeNode Root { get; private set; }

    /// <summary>Узлы: родитель раньше потомков</summary>
    public IEnumerable<TreeNode> PreOrder()
    {
        var stack = new Stack<TreeNode>();
        stack.Push(Root);

        while (stack.Count > 0)
        {
            TreeNode node = stack.Pop();
            yield return node;

            for (int i = node.Children.Count - 1; i >= 0; i--)
                stack.Push(node.Children[i]);
        }
    }

    /// <summary>Узлы: потомки раньше родителя</summary>
    public IEnumerable<TreeNode> PostOrder()
    {
        var pending = new Stack<TreeNode>();
        var output = new Stack<TreeNode>();
        pending.Push(Root);

        while (pending.Count > 0)
        {
            TreeNode node = pending.Pop();
            output.Push(node);

            foreach (TreeNode child in node.Children)
                pending.Push(child);
        }

        return output;
    }

    /// <summary>Листья слева направо</summary>
    public IReadOnlyList<TreeNode> Leaves => PreOrder().Where(n => n.IsLeaf).ToList();

    /// <summary>Имена листьев слева направо</summary>
    public IReadOnlyList<string> LeafNames => Leaves.Select(l => l.Name ?? string.Empty).ToList();

    /// <summary>Сумма длин всех ветвей</summary>
    public double TotalLength => PreOrder().Where(n => !ReferenceEquals(n, Root)).Sum(n => n.BranchLength);

    /// <summary>Узел по имени</summary>
    /// <param name="name">Имя</param>
    public TreeNode? Find(string name) => PreOrder().FirstOrDefault(n => n.Name == name);

    /// <summary>Независимая копия дерева</summary>
    public PhylogeneticTree Clone()
    {
        var copies = new Dictionary<TreeNode, TreeNode>();

        foreach (TreeNode node in PreOrder())
        {
            var copy = new TreeNode(node.Name);
            copies[node] = copy;

            if (node.Parent is not null)
                copies[node.Parent].AddChild(copy, node.BranchLength);
        }

        return new PhylogeneticTree(copies[Root]);
    }

    /// <summary>
    /// Расстояния между листьями по дереву — суммы длин ветвей на пути между ними
    /// </summary>
    public DistanceMatrix PatristicDistances()
    {
        IReadOnlyList<TreeNode> leaves = Leaves;
        string[] names = RequireLeafNames(leaves);
        int n = leaves.Count;
        var values = new double[n, n];

        for (int i = 0; i < n; i++)
        {
            var distance = new Dictionary<TreeNode, double> { [leaves[i]] = 0 };
            var stack = new Stack<TreeNode>();
            stack.Push(leaves[i]);

            while (stack.Count > 0)
            {
                TreeNode node = stack.Pop();
                double here = distance[node];

                foreach (TreeNode child in node.Children)
                {
                    if (distance.TryAdd(child, here + child.BranchLength))
                        stack.Push(child);
                }

                if (node.Parent is not null && distance.TryAdd(node.Parent, here + node.BranchLength))
                    stack.Push(node.Parent);
            }

            for (int j = 0; j < n; j++)
                values[i, j] = i == j ? 0 : distance[leaves[j]];
        }

        // Суммы по разным путям обхода могут отличаться в последнем знаке
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                double mean = (values[i, j] + values[j, i]) / 2;
                values[i, j] = mean;
                values[j, i] = mean;
            }
        }

        return new DistanceMatrix(names, values);
    }

    /// <summary>
    /// Нетривиальные разбиения листьев, задаваемые внутренними ветвями
    /// </summary>
    /// <remarks>
    /// Каждая внутренняя ветвь делит листья на две части. Разбиение записывается строкой из нулей
    /// и единиц по листьям в алфавитном порядке, где единицами отмечена часть без первого листа, —
    /// так запись не зависит ни от корня, ни от порядка потомков.
    /// </remarks>
    public IReadOnlySet<string> Splits()
    {
        var result = new HashSet<string>(StringComparer.Ordinal);

        foreach ((TreeNode node, string? key) in SplitKeys())
        {
            if (key is not null && !ReferenceEquals(node, Root))
                result.Add(key);
        }

        return result;
    }

    /// <summary>
    /// Расстояние Робинсона — Фулдса: число разбиений, которые есть только в одном из двух деревьев
    /// </summary>
    /// <param name="first">Первое дерево</param>
    /// <param name="second">Второе дерево на тех же листьях</param>
    public static int RobinsonFoulds(PhylogeneticTree first, PhylogeneticTree second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var a = new HashSet<string>(first.LeafNames, StringComparer.Ordinal);

        if (!a.SetEquals(second.LeafNames))
            throw new ArgumentException("Деревья должны быть построены на одних и тех же листьях", nameof(second));

        IReadOnlySet<string> splitsA = first.Splits();
        IReadOnlySet<string> splitsB = second.Splits();

        return splitsA.Count(s => !splitsB.Contains(s)) + splitsB.Count(s => !splitsA.Contains(s));
    }

    /// <summary>
    /// Переносит корень во внутренний узел
    /// </summary>
    /// <param name="node">Внутренний узел этого дерева</param>
    /// <remarks>
    /// Ветви на пути от узла к прежнему корню меняют направление, длины сохраняются. Если прежний
    /// корень остался с одним потомком, он убирается, а две его ветви сливаются в одну. При
    /// обратимой модели замен правдоподобие от положения корня не зависит — это принцип «шкива»
    /// Фельзенштейна, и перенос корня этим проверяется.
    /// </remarks>
    public void RootAt(TreeNode node)
    {
        ArgumentNullException.ThrowIfNull(node);

        if (!Contains(node))
            throw new ArgumentException("Узел не принадлежит дереву", nameof(node));

        if (node.IsLeaf)
            throw new ArgumentException("Корень переносится во внутренний узел", nameof(node));

        if (ReferenceEquals(node, Root))
            return;

        var path = new List<TreeNode>();

        for (TreeNode? current = node; current is not null; current = current.Parent)
            path.Add(current);

        double[] lengths = path.Take(path.Count - 1).Select(p => p.BranchLength).ToArray();

        for (int m = 0; m + 1 < path.Count; m++)
            path[m + 1].Detach(path[m]);

        for (int m = 0; m + 1 < path.Count; m++)
            path[m].AddChild(path[m + 1], lengths[m]);

        node.BranchLength = 0;
        Root = node;

        TreeNode formerRoot = path[^1];

        if (formerRoot.Children.Count == 1 && formerRoot.Parent is TreeNode parent)
        {
            TreeNode only = formerRoot.Children[0];
            double merged = formerRoot.BranchLength + only.BranchLength;

            parent.Detach(formerRoot);
            formerRoot.Detach(only);
            parent.AddChild(only, merged);
        }
    }

    /// <summary>Запись дерева в формате Newick</summary>
    /// <param name="digits">Число значащих цифр длин ветвей</param>
    public string ToNewick(int digits = 10)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(digits);

        var text = new StringBuilder();
        Write(Root, text, "G" + digits.ToString(CultureInfo.InvariantCulture));
        text.Append(';');

        return text.ToString();
    }

    /// <summary>Читает дерево из записи Newick</summary>
    /// <param name="text">Запись вроде <c>((A:0.1,B:0.2):0.05,C:0.3);</c></param>
    /// <exception cref="FormatException">Запись не разобрана</exception>
    public static PhylogeneticTree ParseNewick(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        return new PhylogeneticTree(new NewickReader(text).Read());
    }

    /// <summary>Запись Newick</summary>
    public override string ToString() => ToNewick();

    /// <summary>Разбиение, задаваемое ветвью над каждым узлом; null для тривиального</summary>
    internal IEnumerable<(TreeNode Node, string? Key)> SplitKeys()
    {
        IReadOnlyList<TreeNode> leaves = Leaves;
        string[] names = RequireLeafNames(leaves);
        string[] sorted = names.OrderBy(n => n, StringComparer.Ordinal).ToArray();
        var position = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int i = 0; i < sorted.Length; i++)
            position[sorted[i]] = i;

        int n = sorted.Length;
        var sides = new Dictionary<TreeNode, bool[]>();

        foreach (TreeNode node in PostOrder())
        {
            var side = new bool[n];

            if (node.IsLeaf)
            {
                side[position[node.Name!]] = true;
            }
            else
            {
                foreach (TreeNode child in node.Children)
                {
                    bool[] below = sides[child];

                    for (int k = 0; k < n; k++)
                        side[k] |= below[k];
                }
            }

            sides[node] = side;
            yield return (node, Key(side));
        }
    }

    private static string? Key(bool[] side)
    {
        int count = side.Count(s => s);
        int n = side.Length;

        if (count < 2 || count > n - 2)
            return null;

        bool flip = side[0];
        var key = new char[n];

        for (int k = 0; k < n; k++)
            key[k] = side[k] ^ flip ? '1' : '0';

        return new string(key);
    }

    private static string[] RequireLeafNames(IReadOnlyList<TreeNode> leaves)
    {
        var names = new string[leaves.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (int i = 0; i < leaves.Count; i++)
        {
            string? name = leaves[i].Name;

            if (string.IsNullOrWhiteSpace(name) || !seen.Add(name))
                throw new InvalidOperationException($"Имя листа «{name}» пусто или повторяется");

            names[i] = name;
        }

        return names;
    }

    private bool Contains(TreeNode node)
    {
        for (TreeNode? current = node; current is not null; current = current.Parent)
        {
            if (ReferenceEquals(current, Root))
                return true;
        }

        return false;
    }

    private void Write(TreeNode node, StringBuilder text, string format)
    {
        if (!node.IsLeaf)
        {
            text.Append('(');

            for (int i = 0; i < node.Children.Count; i++)
            {
                if (i > 0)
                    text.Append(',');

                Write(node.Children[i], text, format);
            }

            text.Append(')');
        }

        if (!string.IsNullOrEmpty(node.Name))
            text.Append(Quote(node.Name));

        if (!ReferenceEquals(node, Root))
            text.Append(':').Append(node.BranchLength.ToString(format, CultureInfo.InvariantCulture));
    }

    private static string Quote(string name)
        => name.IndexOfAny(['(', ')', '[', ']', '\'', ':', ';', ',', ' ', '\t', '\n', '\r']) >= 0
            ? "'" + name.Replace("'", "''", StringComparison.Ordinal) + "'"
            : name;

    private sealed class NewickReader(string text)
    {
        private int _position;

        public TreeNode Read()
        {
            TreeNode root = Subtree();
            Skip();

            if (_position < text.Length && text[_position] == ';')
                _position++;

            Skip();

            if (_position != text.Length)
                throw Error("лишние символы после дерева");

            root.BranchLength = 0;

            return root;
        }

        private TreeNode Subtree()
        {
            Skip();
            var node = new TreeNode();

            if (Peek() == '(')
            {
                _position++;

                while (true)
                {
                    TreeNode child = Subtree();
                    node.AddChild(child, child.BranchLength);
                    Skip();

                    char next = Peek();
                    _position++;

                    if (next == ',')
                        continue;

                    if (next == ')')
                        break;

                    throw Error("ожидалась запятая или закрывающая скобка");
                }
            }

            Skip();
            string label = Label();
            node.Name = label.Length == 0 ? null : label;
            Skip();

            if (Peek() == ':')
            {
                _position++;
                Skip();
                node.BranchLength = Number();
            }

            return node;
        }

        private string Label()
        {
            if (Peek() == '\'')
            {
                _position++;
                var quoted = new StringBuilder();

                while (_position < text.Length)
                {
                    char c = text[_position++];

                    if (c != '\'')
                    {
                        quoted.Append(c);
                        continue;
                    }

                    if (Peek() == '\'')
                    {
                        quoted.Append('\'');
                        _position++;
                        continue;
                    }

                    return quoted.ToString();
                }

                throw Error("не закрыта кавычка в имени");
            }

            int start = _position;

            while (_position < text.Length && "(),:;[".IndexOf(text[_position]) < 0 && !char.IsWhiteSpace(text[_position]))
                _position++;

            return text[start.._position];
        }

        private double Number()
        {
            int start = _position;

            while (_position < text.Length && "0123456789+-.eE".IndexOf(text[_position]) >= 0)
                _position++;

            return double.TryParse(text.AsSpan(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out double value)
                ? value
                : throw Error("не разобрана длина ветви");
        }

        private void Skip()
        {
            while (_position < text.Length)
            {
                if (char.IsWhiteSpace(text[_position]))
                {
                    _position++;
                }
                else if (text[_position] == '[')
                {
                    int end = text.IndexOf(']', _position);

                    if (end < 0)
                        throw Error("не закрыт комментарий");

                    _position = end + 1;
                }
                else
                {
                    break;
                }
            }
        }

        private char Peek() => _position < text.Length ? text[_position] : '\0';

        private FormatException Error(string reason) => new($"Запись Newick не разобрана в позиции {_position + 1}: {reason}");
    }
}
