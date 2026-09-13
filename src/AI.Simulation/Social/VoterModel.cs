using AI.Algorithms.GraphStructure;
using AI.Simulation.Space;

namespace AI.Simulation.Social;

/// <summary>Исход модели избирателя</summary>
/// <param name="Consensus">Мнение, к которому пришли все; <c>null</c> — предел шагов исчерпан раньше</param>
/// <param name="Updates">Выполнено обновлений</param>
public readonly record struct VoterOutcome(bool? Consensus, long Updates);

/// <summary>
/// Модель избирателя: случайный агент перенимает мнение случайного своего контакта.
/// </summary>
/// <remarks>
/// <para>
/// Самая простая модель подражания: мнений два, а агент на каждом шаге копирует соседа. На связной
/// сети все рано или поздно приходят к одному мнению, но к какому — случайно.
/// </para>
/// <para>
/// Проверяемый ответ: сумма Σ dᵢ·xᵢ по степеням узлов — мартингал. При обновлении агента i его мнение
/// заменяется мнением случайного соседа, и в среднем по соседям прирост Σ dᵢ·xᵢ по каждому ребру
/// взаимно гасится. Отсюда вероятность согласия на мнении «да» равна доле «да» с весами степеней, а не
/// простой доле: один центр звезды весит столько же, сколько все её лучи вместе.
/// </para>
/// </remarks>
public static class VoterModel
{
    /// <summary>Вероятность прийти к мнению «да»: Σ dᵢ·xᵢ/Σ dᵢ</summary>
    /// <param name="network">Связная сеть контактов</param>
    /// <param name="opinions">Начальные мнения: «да» или «нет»</param>
    public static double ConsensusProbability(ContactNetwork network, IReadOnlyList<bool> opinions)
    {
        Require(network, opinions);

        if (network.NodeCount == 1)
            return opinions[0] ? 1 : 0;

        double agree = 0, total = 0;

        for (int i = 0; i < network.NodeCount; i++)
        {
            int degree = network.Degree(i);
            total += degree;

            if (opinions[i])
                agree += degree;
        }

        return agree / total;
    }

    /// <summary>Моделирует динамику до согласия</summary>
    /// <param name="network">Связная сеть контактов</param>
    /// <param name="opinions">Начальные мнения</param>
    /// <param name="random">Генератор</param>
    /// <param name="maxUpdates">Предел обновлений</param>
    public static VoterOutcome Run(ContactNetwork network, IReadOnlyList<bool> opinions, Random random, long maxUpdates = 10_000_000)
    {
        Require(network, opinions);
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(maxUpdates);

        bool[] state = [.. opinions];
        int n = state.Length;
        int agree = state.Count(value => value);

        for (long update = 0; ; update++)
        {
            if (agree == 0 || agree == n)
                return new VoterOutcome(agree == n, update);

            if (update == maxUpdates)
                return new VoterOutcome(null, update);

            int i = random.Next(n);
            int[] contacts = network.Contacts(i);
            bool copied = state[contacts[random.Next(contacts.Length)]];

            if (copied != state[i])
            {
                state[i] = copied;
                agree += copied ? 1 : -1;
            }
        }
    }

    private static void Require(ContactNetwork network, IReadOnlyList<bool> opinions)
    {
        ArgumentNullException.ThrowIfNull(network);
        ArgumentNullException.ThrowIfNull(opinions);

        if (opinions.Count != network.NodeCount)
            throw new ArgumentException($"Мнений {opinions.Count}, а узлов {network.NodeCount}", nameof(opinions));

        // Неориентированная сеть: сильно связные компоненты Тарьяна совпадают со связными
        if (new TarjanSCC(network.Graph).Count != 1)
            throw new ArgumentException("Сеть должна быть связной: иначе каждая часть придёт к своему мнению", nameof(network));
    }
}
