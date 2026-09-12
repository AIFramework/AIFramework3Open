namespace AI.Microwave.Propagation;

/// <summary>Тип местности для модели Окумуры — Хаты</summary>
public enum HataEnvironment
{
    /// <summary>Небольшой или средний город</summary>
    MediumCity,

    /// <summary>Крупный город с плотной высокой застройкой</summary>
    LargeCity,

    /// <summary>Пригород; в COST-231 совпадает со средним городом</summary>
    Suburban,

    /// <summary>Открытая местность без высоких препятствий; лес на трассе учитывается отдельно, <see cref="VegetationLoss"/></summary>
    Open,
}
