#nullable enable

using System;
using System.Collections.Generic;
using AI.Geometry.Primitives;

namespace AI.Geometry.Spatial;

/// <summary>
/// Динамическое дерево осеориентированных параллелепипедов (BVH) для широкой фазы поиска столкновений.
/// </summary>
/// <remarks>
/// <para>
/// Листья хранят объекты с их точными границами и расширенными на запас (<see cref="Margin"/>) границами.
/// Узлы строятся по расширенным, поэтому объект, сдвинувшийся в пределах запаса, не требует перестройки
/// дерева: <see cref="Update"/> лишь запоминает новые точные границы.
/// </para>
/// <para>
/// Место вставки выбирается по приросту площади поверхности, а после вставки и удаления дерево
/// балансируется поворотами, как АВЛ-дерево (схема Box2D). Запросы сравнивают с запросом точные
/// границы листьев, поэтому их результат совпадает с полным перебором, а запас влияет только на скорость.
/// </para>
/// </remarks>
/// <typeparam name="T">Данные, связанные с объектом.</typeparam>
public sealed class DynamicAabbTree<T>
{
    private const int Null = -1;

    private Node[] _nodes = new Node[16];
    private int _root = Null;
    private int _free = Null;
    private int _allocated;

    /// <summary>
    /// Создает пустое дерево.
    /// </summary>
    /// <param name="margin">Запас, на который границы листа расширяются по каждой оси.</param>
    public DynamicAabbTree(double margin = 0.1)
    {
        if (margin < 0 || double.IsNaN(margin))
            throw new ArgumentOutOfRangeException(nameof(margin), "Запас не может быть отрицательным");

        Margin = margin;
    }

    /// <summary>
    /// Запас расширения границ листьев.
    /// </summary>
    public double Margin { get; }

    /// <summary>
    /// Число объектов в дереве.
    /// </summary>
    public int Count { get; private set; }

    /// <summary>
    /// Высота дерева: 0 у пустого, 1 у дерева из одного листа.
    /// </summary>
    public int Height => _root == Null ? 0 : _nodes[_root].Height + 1;

    /// <summary>
    /// Данные объекта по его идентификатору.
    /// </summary>
    /// <param name="proxy">Идентификатор, выданный <see cref="Insert"/>.</param>
    public T this[int proxy] => _nodes[CheckLeaf(proxy)].Item;

    /// <summary>
    /// Добавляет объект.
    /// </summary>
    /// <param name="min">Минимальный угол границ.</param>
    /// <param name="max">Максимальный угол границ.</param>
    /// <param name="item">Данные объекта.</param>
    /// <returns>Идентификатор объекта; остается прежним до удаления.</returns>
    public int Insert(Vector3 min, Vector3 max, T item)
    {
        int leaf = Allocate();
        ref Node node = ref _nodes[leaf];
        node.Item = item;
        node.Height = 0;
        SetBounds(ref node, min, max);
        InsertLeaf(leaf);
        Count++;
        return leaf;
    }

    /// <summary>
    /// Удаляет объект.
    /// </summary>
    /// <param name="proxy">Идентификатор объекта.</param>
    public void Remove(int proxy)
    {
        CheckLeaf(proxy);
        RemoveLeaf(proxy);
        Release(proxy);
        Count--;
    }

    /// <summary>
    /// Сообщает новые границы объекта.
    /// </summary>
    /// <param name="proxy">Идентификатор объекта.</param>
    /// <param name="min">Минимальный угол новых границ.</param>
    /// <param name="max">Максимальный угол новых границ.</param>
    /// <returns>true, если объект вышел за расширенные границы и лист перевставлен.</returns>
    public bool Update(int proxy, Vector3 min, Vector3 max)
    {
        ref Node node = ref _nodes[CheckLeaf(proxy)];

        if (Contains(node.Min, node.Max, min, max))
        {
            node.TightMin = min;
            node.TightMax = max;
            return false;
        }

        RemoveLeaf(proxy);
        SetBounds(ref _nodes[proxy], min, max);
        InsertLeaf(proxy);
        return true;
    }

    /// <summary>
    /// Точные границы объекта.
    /// </summary>
    /// <param name="proxy">Идентификатор объекта.</param>
    public (Vector3 Min, Vector3 Max) GetBounds(int proxy)
    {
        ref Node node = ref _nodes[CheckLeaf(proxy)];
        return (node.TightMin, node.TightMax);
    }

    /// <summary>
    /// Расширенные границы объекта, по которым построено дерево.
    /// </summary>
    /// <param name="proxy">Идентификатор объекта.</param>
    public (Vector3 Min, Vector3 Max) GetFatBounds(int proxy)
    {
        ref Node node = ref _nodes[CheckLeaf(proxy)];
        return (node.Min, node.Max);
    }

    /// <summary>
    /// Объекты, границы которых пересекаются с параллелепипедом (касание считается пересечением).
    /// </summary>
    /// <param name="min">Минимальный угол запроса.</param>
    /// <param name="max">Максимальный угол запроса.</param>
    /// <param name="results">Куда добавить идентификаторы.</param>
    public void Query(Vector3 min, Vector3 max, ICollection<int> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        var stack = new Stack<int>();
        Query(min, max, stack, leaf => results.Add(leaf));
    }

    /// <summary>
    /// Объекты, границы которых пересекает луч origin + t·direction при t ∈ [0, maxT].
    /// </summary>
    /// <param name="origin">Начало луча.</param>
    /// <param name="direction">Направление луча (не обязательно единичное).</param>
    /// <param name="maxT">Наибольший параметр t; для бесконечного луча <see cref="double.PositiveInfinity"/>.</param>
    /// <param name="results">Куда добавить идентификаторы.</param>
    public void QueryRay(Vector3 origin, Vector3 direction, double maxT, ICollection<int> results)
    {
        ArgumentNullException.ThrowIfNull(results);

        if (_root == Null)
            return;

        var inverse = new Vector3(1 / direction.X, 1 / direction.Y, 1 / direction.Z);
        var stack = new Stack<int>();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            ref Node node = ref _nodes[index];

            if (node.IsLeaf)
            {
                if (RayHits(origin, direction, inverse, maxT, node.TightMin, node.TightMax))
                    results.Add(index);
            }
            else if (RayHits(origin, direction, inverse, maxT, node.Min, node.Max))
            {
                stack.Push(node.Left);
                stack.Push(node.Right);
            }
        }
    }

    /// <summary>
    /// Все пары объектов с пересекающимися границами, каждая пара один раз (A &lt; B).
    /// </summary>
    /// <param name="pairs">Куда добавить пары идентификаторов.</param>
    public void QueryPairs(ICollection<(int A, int B)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        var stack = new Stack<int>();

        for (int i = 0; i < _allocated; i++)
        {
            if (_nodes[i].Height != 0)
                continue;

            int self = i;
            Query(_nodes[i].TightMin, _nodes[i].TightMax, stack, other =>
            {
                if (other > self)
                    pairs.Add((self, other));
            });
        }
    }

    private void Query(Vector3 min, Vector3 max, Stack<int> stack, Action<int> found)
    {
        if (_root == Null)
            return;

        stack.Clear();
        stack.Push(_root);

        while (stack.Count > 0)
        {
            int index = stack.Pop();
            ref Node node = ref _nodes[index];

            if (node.IsLeaf)
            {
                if (Overlaps(node.TightMin, node.TightMax, min, max))
                    found(index);
            }
            else if (Overlaps(node.Min, node.Max, min, max))
            {
                stack.Push(node.Left);
                stack.Push(node.Right);
            }
        }
    }

    private void SetBounds(ref Node node, Vector3 min, Vector3 max)
    {
        var margin = new Vector3(Margin, Margin, Margin);
        node.TightMin = min;
        node.TightMax = max;
        node.Min = min - margin;
        node.Max = max + margin;
    }

    private void InsertLeaf(int leaf)
    {
        if (_root == Null)
        {
            _root = leaf;
            _nodes[leaf].Parent = Null;
            return;
        }

        Vector3 leafMin = _nodes[leaf].Min, leafMax = _nodes[leaf].Max;
        int index = _root;

        // Спуск к соседу, при котором суммарный прирост площади поверхности наименьший
        while (!_nodes[index].IsLeaf)
        {
            ref Node node = ref _nodes[index];
            double area = Area(node.Min, node.Max);
            double combined = Area(Vector3.Min(node.Min, leafMin), Vector3.Max(node.Max, leafMax));
            double cost = 2 * combined;
            double inheritance = 2 * (combined - area);
            double costLeft = ChildCost(node.Left, leafMin, leafMax) + inheritance;
            double costRight = ChildCost(node.Right, leafMin, leafMax) + inheritance;

            if (cost < costLeft && cost < costRight)
                break;

            index = costLeft < costRight ? node.Left : node.Right;
        }

        int sibling = index;
        int oldParent = _nodes[sibling].Parent;
        int newParent = Allocate();
        ref Node parent = ref _nodes[newParent];
        parent.Parent = oldParent;
        parent.Min = Vector3.Min(leafMin, _nodes[sibling].Min);
        parent.Max = Vector3.Max(leafMax, _nodes[sibling].Max);
        parent.Height = _nodes[sibling].Height + 1;
        parent.Left = sibling;
        parent.Right = leaf;

        if (oldParent != Null)
        {
            if (_nodes[oldParent].Left == sibling)
                _nodes[oldParent].Left = newParent;
            else
                _nodes[oldParent].Right = newParent;
        }
        else
        {
            _root = newParent;
        }

        _nodes[sibling].Parent = newParent;
        _nodes[leaf].Parent = newParent;
        Refit(_nodes[leaf].Parent);
    }

    private void RemoveLeaf(int leaf)
    {
        if (leaf == _root)
        {
            _root = Null;
            return;
        }

        int parent = _nodes[leaf].Parent;
        int grandParent = _nodes[parent].Parent;
        int sibling = _nodes[parent].Left == leaf ? _nodes[parent].Right : _nodes[parent].Left;

        if (grandParent != Null)
        {
            if (_nodes[grandParent].Left == parent)
                _nodes[grandParent].Left = sibling;
            else
                _nodes[grandParent].Right = sibling;

            _nodes[sibling].Parent = grandParent;
            Release(parent);
            Refit(grandParent);
        }
        else
        {
            _root = sibling;
            _nodes[sibling].Parent = Null;
            Release(parent);
        }
    }

    /// <summary>
    /// Балансирует и пересчитывает границы и высоты от узла до корня.
    /// </summary>
    private void Refit(int index)
    {
        while (index != Null)
        {
            index = Balance(index);
            ref Node node = ref _nodes[index];
            ref Node left = ref _nodes[node.Left];
            ref Node right = ref _nodes[node.Right];
            node.Height = 1 + Math.Max(left.Height, right.Height);
            node.Min = Vector3.Min(left.Min, right.Min);
            node.Max = Vector3.Max(left.Max, right.Max);
            index = node.Parent;
        }
    }

    /// <summary>
    /// Если высоты поддеревьев узла a различаются больше чем на 1, поднимает более высокого ребенка на место a.
    /// </summary>
    /// <returns>Индекс узла, оказавшегося на месте a.</returns>
    private int Balance(int a)
    {
        ref Node nodeA = ref _nodes[a];

        if (nodeA.IsLeaf || nodeA.Height < 2)
            return a;

        int b = nodeA.Left;
        int c = nodeA.Right;
        int balance = _nodes[c].Height - _nodes[b].Height;

        if (balance > 1)
            return Rotate(a, c, b, rightChild: true);

        if (balance < -1)
            return Rotate(a, b, c, rightChild: false);

        return a;
    }

    /// <summary>
    /// Поворот: высокий ребенок up встает на место a, a становится его ребенком, а более низкий
    /// внук up переходит к a на место up.
    /// </summary>
    private int Rotate(int a, int up, int other, bool rightChild)
    {
        ref Node nodeA = ref _nodes[a];
        ref Node nodeUp = ref _nodes[up];
        int f = nodeUp.Left;
        int g = nodeUp.Right;

        nodeUp.Left = a;
        nodeUp.Parent = nodeA.Parent;
        nodeA.Parent = up;

        if (nodeUp.Parent != Null)
        {
            if (_nodes[nodeUp.Parent].Left == a)
                _nodes[nodeUp.Parent].Left = up;
            else
                _nodes[nodeUp.Parent].Right = up;
        }
        else
        {
            _root = up;
        }

        // Выше остается внук с большей высотой, ниже переходит к a
        int keep = _nodes[f].Height > _nodes[g].Height ? f : g;
        int move = keep == f ? g : f;
        nodeUp.Right = keep;

        if (rightChild)
            nodeA.Right = move;
        else
            nodeA.Left = move;

        _nodes[move].Parent = a;
        nodeA.Min = Vector3.Min(_nodes[other].Min, _nodes[move].Min);
        nodeA.Max = Vector3.Max(_nodes[other].Max, _nodes[move].Max);
        nodeA.Height = 1 + Math.Max(_nodes[other].Height, _nodes[move].Height);
        nodeUp.Min = Vector3.Min(nodeA.Min, _nodes[keep].Min);
        nodeUp.Max = Vector3.Max(nodeA.Max, _nodes[keep].Max);
        nodeUp.Height = 1 + Math.Max(nodeA.Height, _nodes[keep].Height);
        return up;
    }

    private double ChildCost(int child, Vector3 leafMin, Vector3 leafMax)
    {
        ref Node node = ref _nodes[child];
        double combined = Area(Vector3.Min(node.Min, leafMin), Vector3.Max(node.Max, leafMax));
        return node.IsLeaf ? combined : combined - Area(node.Min, node.Max);
    }

    private int Allocate()
    {
        if (_free == Null)
        {
            if (_allocated == _nodes.Length)
                Array.Resize(ref _nodes, _nodes.Length * 2);

            _free = _allocated++;
            _nodes[_free] = new Node { Parent = Null, Height = -1 };
        }

        int index = _free;
        _free = _nodes[index].Parent;
        _nodes[index] = new Node { Parent = Null, Left = Null, Right = Null, Height = 0 };
        return index;
    }

    private void Release(int index)
    {
        _nodes[index] = new Node { Parent = _free, Left = Null, Right = Null, Height = -1 };
        _free = index;
    }

    private int CheckLeaf(int proxy)
    {
        if (proxy < 0 || proxy >= _allocated || _nodes[proxy].Height != 0)
            throw new ArgumentOutOfRangeException(nameof(proxy), "Нет объекта с таким идентификатором");

        return proxy;
    }

    private static double Area(Vector3 min, Vector3 max)
    {
        Vector3 d = max - min;
        return 2 * ((d.X * d.Y) + (d.Y * d.Z) + (d.Z * d.X));
    }

    private static bool Overlaps(Vector3 aMin, Vector3 aMax, Vector3 bMin, Vector3 bMax)
        => aMin.X <= bMax.X && aMax.X >= bMin.X
        && aMin.Y <= bMax.Y && aMax.Y >= bMin.Y
        && aMin.Z <= bMax.Z && aMax.Z >= bMin.Z;

    private static bool Contains(Vector3 outerMin, Vector3 outerMax, Vector3 min, Vector3 max)
        => outerMin.X <= min.X && outerMin.Y <= min.Y && outerMin.Z <= min.Z
        && max.X <= outerMax.X && max.Y <= outerMax.Y && max.Z <= outerMax.Z;

    /// <summary>
    /// Метод слоев: пересекает ли отрезок луча [0, maxT] параллелепипед.
    /// </summary>
    private static bool RayHits(Vector3 origin, Vector3 direction, Vector3 inverse, double maxT, Vector3 min, Vector3 max)
    {
        double tMin = 0, tMax = maxT;

        for (int k = 0; k < 3; k++)
        {
            if (direction[k] == 0)
            {
                if (origin[k] < min[k] || origin[k] > max[k])
                    return false;

                continue;
            }

            double t0 = (min[k] - origin[k]) * inverse[k];
            double t1 = (max[k] - origin[k]) * inverse[k];

            if (t0 > t1)
                (t0, t1) = (t1, t0);

            tMin = Math.Max(tMin, t0);
            tMax = Math.Min(tMax, t1);

            if (tMax < tMin)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Узел дерева. У листа Left = Right = −1 и Height = 0, у свободного узла Height = −1,
    /// а Parent хранит следующий свободный узел.
    /// </summary>
    private struct Node
    {
        public Vector3 Min;
        public Vector3 Max;
        public Vector3 TightMin;
        public Vector3 TightMax;
        public int Parent;
        public int Left;
        public int Right;
        public int Height;
        public T Item;

        public readonly bool IsLeaf => Left == Null;
    }
}
