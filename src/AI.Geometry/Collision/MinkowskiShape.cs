#nullable enable
using System;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Сумма Минковского двух выпуклых форм: опорная точка суммы равна сумме опорных точек. Коробка плюс шар с центром в
/// нуле дает скругленную коробку, отрезок плюс шар дает капсулу, форма плюс точка дает перенесенную форму
/// </summary>
/// <param name="First">Первое слагаемое</param>
/// <param name="Second">Второе слагаемое</param>
public sealed record MinkowskiShape(IConvexShape First, IConvexShape Second) : IConvexShape
{
    /// <summary>Первое слагаемое</summary>
    public IConvexShape First { get; } = First ?? throw new ArgumentNullException(nameof(First));

    /// <summary>Второе слагаемое</summary>
    public IConvexShape Second { get; } = Second ?? throw new ArgumentNullException(nameof(Second));

    /// <inheritdoc/>
    public Vector3 Center => First.Center + Second.Center;

    /// <inheritdoc/>
    public Vector3 Support(Vector3 direction) => First.Support(direction) + Second.Support(direction);
}
