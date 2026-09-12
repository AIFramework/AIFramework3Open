namespace AI.Physics.Nuclear;

/// <summary>Нуклид: ядро с заданным числом протонов и нуклонов</summary>
public readonly record struct Nuclide
{
    private static readonly int[] MagicNumbers = [2, 8, 20, 28, 50, 82, 126];

    /// <summary>Создаёт нуклид</summary>
    /// <param name="protonNumber">Число протонов Z</param>
    /// <param name="massNumber">Массовое число A — число нуклонов</param>
    /// <param name="name">Обозначение, например «Fe-56»</param>
    public Nuclide(int protonNumber, int massNumber, string? name = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(massNumber);

        if (protonNumber < 0 || protonNumber > massNumber)
            throw new ArgumentOutOfRangeException(nameof(protonNumber),
                $"Число протонов {protonNumber} вне диапазона 0…{massNumber}");

        ProtonNumber = protonNumber;
        MassNumber = massNumber;
        Name = name;
    }

    /// <summary>Число протонов Z</summary>
    public int ProtonNumber { get; }

    /// <summary>Массовое число A</summary>
    public int MassNumber { get; }

    /// <summary>Число нейтронов N = A − Z</summary>
    public int NeutronNumber => MassNumber - ProtonNumber;

    /// <summary>Обозначение</summary>
    public string? Name { get; }

    /// <summary>Чётно-чётное ядро: спаривание добавляет ему связи</summary>
    public bool IsEvenEven => ProtonNumber % 2 == 0 && NeutronNumber % 2 == 0;

    /// <summary>Нечётно-нечётное ядро: спаривание отнимает связь</summary>
    public bool IsOddOdd => ProtonNumber % 2 == 1 && NeutronNumber % 2 == 1;

    /// <summary>Магическое число протонов или нейтронов: заполненная оболочка</summary>
    public bool IsMagic => MagicNumbers.Contains(ProtonNumber) || MagicNumbers.Contains(NeutronNumber);

    /// <summary>Запись нуклида</summary>
    public override string ToString() => Name ?? $"Z = {ProtonNumber}, A = {MassNumber}";
}
