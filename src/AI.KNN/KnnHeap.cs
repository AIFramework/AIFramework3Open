using System.Runtime.CompilerServices;

namespace AI.KNN;

/// <summary>
/// Утилита partial sort на основе max-heap.
/// TopK находит k ближайших (наименьших) элементов за O(n log k).
/// </summary>
internal static class KnnHeap
{
    /// <summary>
    /// Возвращает массив индексов из <paramref name="k"/> элементов с наименьшими значениями
    /// в <paramref name="dists"/> (в произвольном порядке).
    /// </summary>
    internal static int[] TopK(double[] dists, int k)
    {
        int n = dists.Length;
        if (k >= n)
        {
            // Возвращаем все индексы
            int[] all = new int[n];
            for (int i = 0; i < n; i++) all[i] = i;
            return all;
        }

        // Max-heap размером k
        int[] heap = new int[k];
        for (int i = 0; i < k; i++) heap[i] = i;
        BuildMaxHeap(heap, dists, k);

        for (int i = k; i < n; i++)
        {
            if (dists[i] < dists[heap[0]])
            {
                heap[0] = i;
                SiftDown(heap, dists, 0, k);
            }
        }

        return heap;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void BuildMaxHeap(int[] heap, double[] dist, int k)
    {
        for (int i = k / 2 - 1; i >= 0; i--)
            SiftDown(heap, dist, i, k);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void SiftDown(int[] heap, double[] dist, int root, int k)
    {
        while (true)
        {
            int largest = root;
            int left    = 2 * root + 1;
            int right   = 2 * root + 2;

            if (left  < k && dist[heap[left]]  > dist[heap[largest]]) largest = left;
            if (right < k && dist[heap[right]] > dist[heap[largest]]) largest = right;

            if (largest == root) break;

            (heap[root], heap[largest]) = (heap[largest], heap[root]);
            root = largest;
        }
    }
}
