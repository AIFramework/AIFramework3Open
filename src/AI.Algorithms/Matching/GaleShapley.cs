using System;
using System.Collections.Generic;

namespace AI.Algorithms.Matching;

/// <summary>
/// Алгоритм Гейла-Шепли для задачи об устойчивых браках (stable marriage)
/// </summary>
[Serializable]
public class GaleShapley
{
    /// <summary>
    /// Партнёр каждого мужчины: ManPartner[i] — индекс женщины
    /// </summary>
    public int[] ManPartner { get; private set; }

    /// <summary>
    /// Партнёр каждой женщины: WomanPartner[j] — индекс мужчины
    /// </summary>
    public int[] WomanPartner { get; private set; }

    /// <summary>
    /// Решает задачу об устойчивых браках
    /// </summary>
    /// <param name="menPrefs">Предпочтения мужчин: menPrefs[i] — ранжирование женщин мужчиной i</param>
    /// <param name="womenPrefs">Предпочтения женщин: womenPrefs[j] — ранжирование мужчин женщиной j</param>
    public GaleShapley(int[][] menPrefs, int[][] womenPrefs)
    {
        int n = menPrefs.Length;

        ManPartner = new int[n];
        WomanPartner = new int[n];

        int[][] womenRank = new int[n][];
        for (int i = 0; i < n; i++)
        {
            womenRank[i] = new int[n];
            for (int j = 0; j < n; j++)
                womenRank[i][womenPrefs[i][j]] = j;
        }

        int[] nextProposal = new int[n];

        for (int i = 0; i < n; i++)
        {
            ManPartner[i] = -1;
            WomanPartner[i] = -1;
        }

        Queue<int> freeMan = new Queue<int>();
        for (int i = 0; i < n; i++)
            freeMan.Enqueue(i);

        while (freeMan.Count > 0)
        {
            int m = freeMan.Dequeue();
            int w = menPrefs[m][nextProposal[m]];
            nextProposal[m]++;

            if (WomanPartner[w] == -1)
            {
                ManPartner[m] = w;
                WomanPartner[w] = m;
            }
            else
            {
                int currentMan = WomanPartner[w];
                if (womenRank[w][m] < womenRank[w][currentMan])
                {
                    ManPartner[m] = w;
                    WomanPartner[w] = m;
                    ManPartner[currentMan] = -1;
                    freeMan.Enqueue(currentMan);
                }
                else
                {
                    freeMan.Enqueue(m);
                }
            }
        }
    }
}
