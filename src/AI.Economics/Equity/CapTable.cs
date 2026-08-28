using System;
using System.Collections.Generic;
using System.Linq;

namespace AI.Economics.Equity;

/// <summary>Пакет акций одного держателя в одном классе.</summary>
public sealed record Holding
{
    /// <summary>Имя держателя.</summary>
    public string Holder { get; init; } = string.Empty;

    /// <summary>Название класса акций.</summary>
    public string ShareClass { get; init; } = string.Empty;

    /// <summary>Число акций.</summary>
    public double Shares { get; init; }

    /// <summary>Вложенная сумма — база для расчёта ликвидационной преференции.</summary>
    public double Invested { get; init; }
}

/// <summary>Строка сводки по владению.</summary>
/// <param name="Holder">Держатель.</param>
/// <param name="Shares">Число акций.</param>
/// <param name="Ownership">Доля от полностью разводнённого капитала.</param>
/// <param name="Invested">Вложенная сумма.</param>
public sealed record OwnershipRow(string Holder, double Shares, double Ownership, double Invested);

/// <summary>
/// Таблица капитализации: классы акций, пакеты держателей и нераспределённый
/// опционный пул.
/// </summary>
/// <remarks>
/// Все доли считаются от полностью разводнённого капитала (fully diluted),
/// включая нераспределённые опционы. Именно эта база используется при
/// определении цены раунда, поэтому иные варианты подсчёта («по выпущенным
/// акциям») здесь сознательно не поддерживаются: они дают другую цену
/// и другую долю инвестора при тех же деньгах.
/// </remarks>
public sealed class CapTable
{
    private readonly List<ShareClass> _classes = [];
    private readonly List<Holding> _holdings = [];

    /// <summary>Создаёт пустую таблицу с классом обыкновенных акций.</summary>
    public CapTable()
    {
        _classes.Add(new ShareClass { Name = CommonClassName, IsCommon = true, Seniority = 0 });
    }

    /// <summary>Название класса обыкновенных акций по умолчанию.</summary>
    public const string CommonClassName = "Common";

    /// <summary>Классы акций.</summary>
    public IReadOnlyList<ShareClass> Classes => _classes;

    /// <summary>Пакеты акций.</summary>
    public IReadOnlyList<Holding> Holdings => _holdings;

    /// <summary>Нераспределённые опционы, в акциях.</summary>
    public double UnallocatedPool { get; set; }

    /// <summary>Сумма выпущенных акций без нераспределённого пула.</summary>
    public double IssuedShares => _holdings.Sum(h => h.Shares);

    /// <summary>Полностью разводнённое число акций, включая нераспределённый пул.</summary>
    public double FullyDilutedShares => IssuedShares + UnallocatedPool;

    /// <summary>Добавляет класс акций.</summary>
    /// <param name="shareClass">Описание класса.</param>
    /// <returns>Эта же таблица — для цепочки вызовов.</returns>
    /// <exception cref="ArgumentNullException">Класс не задан.</exception>
    public CapTable AddClass(ShareClass shareClass)
    {
        ArgumentNullException.ThrowIfNull(shareClass);
        _classes.RemoveAll(c => c.Name == shareClass.Name);
        _classes.Add(shareClass);
        return this;
    }

    /// <summary>Добавляет пакет акций.</summary>
    /// <param name="holder">Имя держателя.</param>
    /// <param name="shares">Число акций.</param>
    /// <param name="shareClass">Класс акций; по умолчанию обыкновенные.</param>
    /// <param name="invested">Вложенная сумма.</param>
    /// <returns>Эта же таблица — для цепочки вызовов.</returns>
    public CapTable AddHolding(string holder, double shares, string? shareClass = null, double invested = 0)
    {
        _holdings.Add(new Holding
        {
            Holder = holder,
            ShareClass = shareClass ?? CommonClassName,
            Shares = shares,
            Invested = invested,
        });
        return this;
    }

    /// <summary>Ищет класс по имени.</summary>
    /// <param name="name">Название класса.</param>
    /// <returns>Класс либо <c>null</c>.</returns>
    public ShareClass? FindClass(string name) => _classes.FirstOrDefault(c => c.Name == name);

    /// <summary>Число акций держателя во всех классах.</summary>
    /// <param name="holder">Имя держателя.</param>
    public double SharesOf(string holder) => _holdings.Where(h => h.Holder == holder).Sum(h => h.Shares);

    /// <summary>Доля держателя от полностью разводнённого капитала.</summary>
    /// <param name="holder">Имя держателя.</param>
    public double OwnershipOf(string holder)
    {
        double total = FullyDilutedShares;
        return total > 0 ? SharesOf(holder) / total : 0;
    }

    /// <summary>
    /// Сводка владения по держателям, отсортированная по убыванию доли.
    /// Нераспределённый пул показан отдельной строкой.
    /// </summary>
    public IReadOnlyList<OwnershipRow> Ownership()
    {
        double total = FullyDilutedShares;
        var rows = _holdings
            .GroupBy(h => h.Holder)
            .Select(g => new OwnershipRow(
                g.Key,
                g.Sum(h => h.Shares),
                total > 0 ? g.Sum(h => h.Shares) / total : 0,
                g.Sum(h => h.Invested)))
            .OrderByDescending(r => r.Shares)
            .ToList();

        if (UnallocatedPool > 0)
            rows.Add(new OwnershipRow("Опционный пул (свободный)", UnallocatedPool,
                total > 0 ? UnallocatedPool / total : 0, 0));

        return rows;
    }

    /// <summary>Создаёт независимую копию таблицы.</summary>
    /// <returns>Глубокая копия.</returns>
    public CapTable Clone()
    {
        var copy = new CapTable { UnallocatedPool = UnallocatedPool };
        copy._classes.Clear();
        copy._classes.AddRange(_classes);
        copy._holdings.AddRange(_holdings);
        return copy;
    }
}
