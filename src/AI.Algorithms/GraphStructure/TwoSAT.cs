using System;
using System.Collections.Generic;

namespace AI.Algorithms.GraphStructure;

/// <summary>
/// Решатель задачи 2-SAT (выполнимость конъюнктивной нормальной формы
/// с двумя литералами в каждом дизъюнкте). Строит граф импликаций
/// и использует алгоритм Тарьяна для нахождения SCC.
/// </summary>
[Serializable]
public class TwoSAT
{
    private readonly int _numVars;
    private readonly int _numNodes;
    private readonly List<int>[] _adj;

    /// <summary>
    /// Результат: значение каждой переменной (индексация с 0).
    /// Доступно после успешного вызова Solve().
    /// </summary>
    public bool[] Assignment { get; private set; }

    /// <summary>
    /// Создаёт экземпляр решателя 2-SAT
    /// </summary>
    /// <param name="numVariables">Количество булевых переменных (нумерация с 1)</param>
    public TwoSAT(int numVariables)
    {
        _numVars = numVariables;
        _numNodes = 2 * numVariables;
        _adj = new List<int>[_numNodes];
        for (int i = 0; i < _numNodes; i++)
            _adj[i] = new List<int>();
    }

    private int LiteralToNode(int literal)
    {
        if (literal > 0) return 2 * (literal - 1);
        return 2 * (-literal - 1) + 1;
    }

    private int Negate(int node)
    {
        return node ^ 1;
    }

    /// <summary>
    /// Добавляет дизъюнкт (u OR v). Литералы задаются целыми числами:
    /// положительное значение — переменная, отрицательное — отрицание переменной.
    /// Например, AddClause(1, -2) означает (x1 OR NOT x2).
    /// </summary>
    /// <param name="u">Первый литерал</param>
    /// <param name="v">Второй литерал</param>
    public void AddClause(int u, int v)
    {
        int nu = LiteralToNode(u);
        int nv = LiteralToNode(v);
        _adj[Negate(nu)].Add(nv);
        _adj[Negate(nv)].Add(nu);
    }

    /// <summary>
    /// Решает задачу 2-SAT. Возвращает true, если формула выполнима.
    /// При успешном решении заполняет свойство Assignment.
    /// </summary>
    /// <returns>true, если формула выполнима; false иначе</returns>
    public bool Solve()
    {
        int[] comp = TarjanSccInternal();

        for (int i = 0; i < _numVars; i++)
        {
            if (comp[2 * i] == comp[2 * i + 1])
                return false;
        }

        Assignment = new bool[_numVars];
        for (int i = 0; i < _numVars; i++)
        {
            Assignment[i] = comp[2 * i] > comp[2 * i + 1];
        }

        return true;
    }

    private int[] TarjanSccInternal()
    {
        int[] compId = new int[_numNodes];
        int[] disc = new int[_numNodes];
        int[] low = new int[_numNodes];
        bool[] onStack = new bool[_numNodes];
        Stack<int> stack = new Stack<int>();
        int index = 0;
        int compCount = 0;

        for (int i = 0; i < _numNodes; i++)
        {
            disc[i] = -1;
            compId[i] = -1;
        }

        for (int i = 0; i < _numNodes; i++)
        {
            if (disc[i] == -1)
                DfsTarjan(i, disc, low, onStack, stack, compId, ref index, ref compCount);
        }

        return compId;
    }

    private void DfsTarjan(int u, int[] disc, int[] low, bool[] onStack,
        Stack<int> stack, int[] compId, ref int index, ref int compCount)
    {
        disc[u] = low[u] = index++;
        stack.Push(u);
        onStack[u] = true;

        foreach (int w in _adj[u])
        {
            if (disc[w] == -1)
            {
                DfsTarjan(w, disc, low, onStack, stack, compId, ref index, ref compCount);
                low[u] = Math.Min(low[u], low[w]);
            }
            else if (onStack[w])
            {
                low[u] = Math.Min(low[u], disc[w]);
            }
        }

        if (low[u] == disc[u])
        {
            int w;
            do
            {
                w = stack.Pop();
                onStack[w] = false;
                compId[w] = compCount;
            } while (w != u);
            compCount++;
        }
    }
}
