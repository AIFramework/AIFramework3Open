#nullable enable
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Выпуклая форма, заданная опорной функцией: GJK, EPA и консервативное продвижение работают с любой такой формой
/// </summary>
public interface IConvexShape
{
    /// <summary>Внутренняя точка формы в мировых координатах, обычно центр</summary>
    Vector3 Center { get; }

    /// <summary>Самая дальняя точка формы в направлении direction, в мировых координатах</summary>
    /// <param name="direction">Направление поиска, не обязательно единичное</param>
    Vector3 Support(Vector3 direction);
}
