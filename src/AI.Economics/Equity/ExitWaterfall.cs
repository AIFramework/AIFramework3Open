using System;
using System.Collections.Generic;
using System.Linq;
using AI.DataStructs.Algebraic;

using AI.Economics.Insights;

namespace AI.Economics.Equity;

/// <summary>Выплата одному держателю при выходе.</summary>
public sealed record HolderPayout
{
    /// <summary>Держатель.</summary>
    public string Holder { get; init; } = string.Empty;

    /// <summary>Класс акций.</summary>
    public string ShareClass { get; init; } = string.Empty;

    /// <summary>Число акций.</summary>
    public double Shares { get; init; }

    /// <summary>Вложенная сумма.</summary>
    public double Invested { get; init; }

    /// <summary>Полученная сумма.</summary>
    public double Payout { get; init; }

    /// <summary>Доля от суммы сделки.</summary>
    public double ShareOfExit { get; init; }

    /// <summary>Доля в капитале — для сравнения с фактической выплатой.</summary>
    public double Ownership { get; init; }

    /// <summary>Возврат на вложенное; <c>NaN</c> для тех, кто не вкладывал деньги.</summary>
    public double MultipleOnInvested { get; init; }
}

/// <summary>Итог по классу акций при выходе.</summary>
public sealed record ClassOutcome
{
    /// <summary>Название класса.</summary>
    public string ClassName { get; init; } = string.Empty;

    /// <summary>Сконвертирован ли класс в обыкновенные акции.</summary>
    public bool Converted { get; init; }

    /// <summary>Выплаченная ликвидационная преференция.</summary>
    public double PreferencePaid { get; init; }

    /// <summary>Доля в остатке.</summary>
    public double ParticipationPaid { get; init; }

    /// <summary>Всего выплачено классу.</summary>
    public double TotalPaid { get; init; }

    /// <summary>Возврат на вложенное по классу.</summary>
    public double MultipleOnInvested { get; init; }

    /// <summary>Как класс получил деньги — словами.</summary>
    public string Decision { get; init; } = string.Empty;
}

/// <summary>Результат распределения выручки от продажи компании.</summary>
public sealed partial record ExitWaterfallResult
{
    /// <summary>Сумма сделки.</summary>
    public double ExitValue { get; init; }

    /// <summary>Выплаты держателям, по убыванию суммы.</summary>
    public IReadOnlyList<HolderPayout> Payouts { get; init; } = [];

    /// <summary>Итоги по классам акций.</summary>
    public IReadOnlyList<ClassOutcome> Classes { get; init; } = [];

    /// <summary>Всего выплачено ликвидационных преференций.</summary>
    public double TotalPreferences { get; init; }

    /// <summary>Остаток, распределённый между участвующими акциями.</summary>
    public double Residual { get; init; }

    /// <summary>Цена одной обыкновенной акции в этой сделке.</summary>
    public double CommonPerShare { get; init; }
}

/// <summary>
/// Каскад выплат при выходе: кто и сколько получит при продаже компании
/// за заданную сумму.
/// </summary>
/// <remarks>
/// <para>
/// Главный вопрос, на который таблица долей не отвечает: 20 % — это сколько
/// денег? При наличии ликвидационных преференций ответ нелинеен и на
/// умеренных суммах сделки может быть равен нулю.
/// </para>
/// <para>
/// Порядок распределения: сначала преференции по старшинству (классы одного
/// старшинства делят пропорционально), затем остаток — между обыкновенными
/// акциями, конвертированными классами и участвующими привилегированными.
/// </para>
/// <para>
/// Решение о конвертации ищется итеративно. Неучаствующий класс сравнивает
/// преференцию с тем, что дала бы конвертация в обыкновенные акции, и
/// выбирает большее; выбор одного класса меняет расклад для остальных,
/// поэтому процесс повторяется до стабилизации. Точка, где конвертация
/// становится выгоднее преференции, и есть та самая «ступенька» на графике
/// выплат основателям.
/// </para>
/// </remarks>
public static class ExitWaterfall
{
    private const int MaxConversionRounds = 64;
    private const double Epsilon = 1e-7;

    /// <summary>Распределяет сумму сделки между держателями.</summary>
    /// <param name="table">Таблица капитализации.</param>
    /// <param name="exitValue">Сумма сделки.</param>
    /// <returns>Кто и сколько получит.</returns>
    /// <exception cref="ArgumentNullException">Таблица не задана.</exception>
    public static ExitWaterfallResult Distribute(CapTable table, double exitValue)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (exitValue < 0) exitValue = 0;

        var state = new WaterfallState(table);
        var converted = new HashSet<string>(StringComparer.Ordinal);

        Allocation allocation = state.Allocate(exitValue, converted);

        for (int round = 0; round < MaxConversionRounds; round++)
        {
            string? bestClass = null;
            double bestGain = Epsilon;

            foreach (ShareClass c in state.PreferredClasses)
            {
                if (converted.Contains(c.Name)) continue;

                var trial = new HashSet<string>(converted, StringComparer.Ordinal) { c.Name };
                Allocation candidate = state.Allocate(exitValue, trial);

                double gain = candidate.Total(c.Name) - allocation.Total(c.Name);
                if (gain > bestGain) { bestGain = gain; bestClass = c.Name; }
            }

            if (bestClass is null) break;

            converted.Add(bestClass);
            allocation = state.Allocate(exitValue, converted);
        }

        return state.BuildResult(exitValue, converted, allocation);
    }

    /// <summary>
    /// Кривая выплат по сумме сделки: для каждого держателя — сколько он
    /// получит при разных ценах продажи.
    /// </summary>
    /// <param name="table">Таблица капитализации.</param>
    /// <param name="maxExit">Максимальная сумма сделки.</param>
    /// <param name="points">Число точек кривой.</param>
    /// <returns>Суммы сделки и выплаты по держателям.</returns>
    /// <exception cref="ArgumentNullException">Таблица не задана.</exception>
    public static (Vector Exits, IReadOnlyDictionary<string, Vector> Payouts) PayoutCurve(
        CapTable table, double maxExit, int points = 60)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (points < 2) points = 2;

        var exits = new Vector(points);
        var curves = new Dictionary<string, Vector>(StringComparer.Ordinal);

        foreach (OwnershipRow row in table.Ownership())
            curves[row.Holder] = new Vector(points);

        for (int i = 0; i < points; i++)
        {
            double exit = maxExit * i / (points - 1);
            exits[i] = exit;

            ExitWaterfallResult result = Distribute(table, exit);
            var byHolder = result.Payouts
                .GroupBy(p => p.Holder)
                .ToDictionary(g => g.Key, g => g.Sum(p => p.Payout), StringComparer.Ordinal);

            foreach ((string holder, Vector curve) in curves)
                curve[i] = byHolder.TryGetValue(holder, out double v) ? v : 0;
        }

        return (exits, curves);
    }

    /// <summary>Промежуточное распределение по классам.</summary>
    private sealed class Allocation
    {
        public Dictionary<string, double> Preference { get; } = new(StringComparer.Ordinal);

        public Dictionary<string, double> Participation { get; } = new(StringComparer.Ordinal);

        public double Residual { get; set; }

        public double Total(string className)
        {
            double sum = 0;
            if (Preference.TryGetValue(className, out double p)) sum += p;
            if (Participation.TryGetValue(className, out double q)) sum += q;
            return sum;
        }
    }

    /// <summary>Предрасчитанные агрегаты таблицы: акции и вложения по классам.</summary>
    private sealed class WaterfallState
    {
        private readonly CapTable _table;
        private readonly Dictionary<string, double> _shares = new(StringComparer.Ordinal);
        private readonly Dictionary<string, double> _invested = new(StringComparer.Ordinal);

        public WaterfallState(CapTable table)
        {
            _table = table;

            foreach (ShareClass c in table.Classes)
            {
                _shares[c.Name] = 0;
                _invested[c.Name] = 0;
            }

            foreach (Holding h in table.Holdings)
            {
                _shares.TryGetValue(h.ShareClass, out double s);
                _invested.TryGetValue(h.ShareClass, out double v);
                _shares[h.ShareClass] = s + h.Shares;
                _invested[h.ShareClass] = v + h.Invested;
            }

            // Нераспределённые опционы участвуют как обыкновенные акции.
            // Цена исполнения не учитывается: на суммах сделки, где она значима,
            // опционы всё равно не исполняются
            ShareClass? common = table.Classes.FirstOrDefault(c => c.IsCommon);
            if (common is not null && table.UnallocatedPool > 0)
                _shares[common.Name] += table.UnallocatedPool;

            PreferredClasses = [.. table.Classes.Where(c => !c.IsCommon && _shares[c.Name] > 0 && _invested[c.Name] > 0)];
        }

        public IReadOnlyList<ShareClass> PreferredClasses { get; }

        public double Shares(string className) => _shares.TryGetValue(className, out double s) ? s : 0;

        public double Invested(string className) => _invested.TryGetValue(className, out double v) ? v : 0;

        /// <summary>Распределяет сумму при заданном множестве конвертированных классов.</summary>
        public Allocation Allocate(double exitValue, HashSet<string> converted)
        {
            var allocation = new Allocation();
            double remaining = exitValue;

            // ── Шаг 1. Преференции по старшинству ─────────────────────────
            var bySeniority = PreferredClasses
                .Where(c => !converted.Contains(c.Name))
                .GroupBy(c => c.Seniority)
                .OrderByDescending(g => g.Key);

            foreach (var group in bySeniority)
            {
                var needs = group.ToDictionary(
                    c => c.Name,
                    c => c.LiquidationMultiple * Invested(c.Name),
                    StringComparer.Ordinal);

                double need = needs.Values.Sum();
                if (need <= 0) continue;

                double pay = Math.Min(remaining, need);
                foreach ((string name, double amount) in needs)
                    allocation.Preference[name] = pay * amount / need;

                remaining -= pay;
            }

            allocation.Residual = remaining;

            // ── Шаг 2. Остаток между участвующими акциями ─────────────────
            var active = new List<ShareClass>();
            foreach (ShareClass c in _table.Classes)
            {
                if (Shares(c.Name) <= 0) continue;

                bool participates = c.IsCommon
                    || converted.Contains(c.Name)
                    || c.Preference == PreferenceType.Participating;

                if (participates) active.Add(c);
            }

            double pool = remaining;

            for (int guard = 0; guard < 32 && active.Count > 0; guard++)
            {
                double totalShares = active.Sum(c => Shares(c.Name));
                if (totalShares <= 0) break;

                ShareClass? capped = null;
                double cappedAmount = 0;

                foreach (ShareClass c in active)
                {
                    double share = pool * Shares(c.Name) / totalShares;

                    if (c.IsCommon || converted.Contains(c.Name) || double.IsNaN(c.ParticipationCap)) continue;

                    double cap = c.ParticipationCap * Invested(c.Name);
                    allocation.Preference.TryGetValue(c.Name, out double pref);

                    if (pref + share > cap + Epsilon)
                    {
                        capped = c;
                        cappedAmount = Math.Max(0, cap - pref);
                        break;
                    }
                }

                if (capped is null)
                {
                    foreach (ShareClass c in active)
                        allocation.Participation[c.Name] =
                            (allocation.Participation.TryGetValue(c.Name, out double prev) ? prev : 0)
                            + (pool * Shares(c.Name) / totalShares);
                    break;
                }

                allocation.Participation[capped.Name] = cappedAmount;
                pool -= cappedAmount;
                active.Remove(capped);
            }

            return allocation;
        }

        /// <summary>Собирает итоговый отчёт по держателям и классам.</summary>
        public ExitWaterfallResult BuildResult(double exitValue, HashSet<string> converted, Allocation allocation)
        {
            var payouts = new List<HolderPayout>();
            double fullyDiluted = _table.FullyDilutedShares;

            foreach (Holding h in _table.Holdings)
            {
                double classShares = Shares(h.ShareClass);
                double classPayout = allocation.Total(h.ShareClass);
                double payout = classShares > 0 ? classPayout * h.Shares / classShares : 0;

                payouts.Add(new HolderPayout
                {
                    Holder = h.Holder,
                    ShareClass = h.ShareClass,
                    Shares = h.Shares,
                    Invested = h.Invested,
                    Payout = payout,
                    ShareOfExit = exitValue > 0 ? payout / exitValue : 0,
                    Ownership = fullyDiluted > 0 ? h.Shares / fullyDiluted : 0,
                    MultipleOnInvested = h.Invested > 0 ? payout / h.Invested : double.NaN,
                });
            }

            var classes = new List<ClassOutcome>();
            foreach (ShareClass c in _table.Classes)
            {
                double shares = Shares(c.Name);
                if (shares <= 0) continue;

                allocation.Preference.TryGetValue(c.Name, out double pref);
                allocation.Participation.TryGetValue(c.Name, out double part);
                double invested = Invested(c.Name);
                bool isConverted = converted.Contains(c.Name);

                classes.Add(new ClassOutcome
                {
                    ClassName = c.Name,
                    Converted = isConverted,
                    PreferencePaid = pref,
                    ParticipationPaid = part,
                    TotalPaid = pref + part,
                    MultipleOnInvested = invested > 0 ? (pref + part) / invested : double.NaN,
                    Decision = Describe(c, isConverted, pref, part, invested),
                });
            }

            ShareClass? common = _table.Classes.FirstOrDefault(x => x.IsCommon);
            double commonShares = common is not null ? Shares(common.Name) : 0;
            double commonPaid = common is not null ? allocation.Total(common.Name) : 0;

            return new ExitWaterfallResult
            {
                ExitValue = exitValue,
                Payouts = [.. payouts.OrderByDescending(p => p.Payout)],
                Classes = classes,
                TotalPreferences = allocation.Preference.Values.Sum(),
                Residual = allocation.Residual,
                CommonPerShare = commonShares > 0 ? commonPaid / commonShares : 0,
            };
        }

        private static string Describe(ShareClass c, bool converted, double pref, double part, double invested)
        {
            if (c.IsCommon) return "Обыкновенные акции";
            if (converted) return "Конвертация в обыкновенные";
            if (c.Preference == PreferenceType.NonParticipating) return "Ликвидационная преференция";

            if (!double.IsNaN(c.ParticipationCap))
            {
                double cap = c.ParticipationCap * invested;
                if (pref + part >= cap - Epsilon) return "Преференция + участие (потолок)";
            }

            return "Преференция + участие";
        }
    }
}
