namespace AI.Simulation.Learning;

/// <summary>Исход одного шага в среде</summary>
/// <param name="NextState">Состояние после шага</param>
/// <param name="Reward">Полученная награда</param>
/// <param name="Terminal">Закончился ли эпизод</param>
public readonly record struct Transition(int NextState, double Reward, bool Terminal);
