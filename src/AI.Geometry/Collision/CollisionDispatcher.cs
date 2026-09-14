#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using AI.Geometry.Primitives;

namespace AI.Geometry.Collision;

/// <summary>
/// Выбор способа поиска касания по паре форм: для шаров, коробок и капсул есть точные частные решения, для остальных
/// пар работают GJK и EPA по опорным функциям
/// </summary>
public static class CollisionDispatcher
{
    /// <summary>Наклон направлений поиска при сборе пятна касания формы с плоскостью</summary>
    private const double Tilt = 1e-3;

    /// <summary>Пятно касания двух выпуклых форм. Нормаль от A к B</summary>
    /// <param name="a">Первая форма</param>
    /// <param name="b">Вторая форма</param>
    public static ContactManifold Collide(IConvexShape a, IConvexShape b) => (a, b) switch
    {
        (SphereShape x, SphereShape y) => SphereContacts.SphereSphere(x, y),
        (BoxShape x, SphereShape y) => BoxContacts.BoxSphere(x, y),
        (SphereShape x, BoxShape y) => BoxContacts.BoxSphere(y, x).Flipped(),
        (BoxShape x, BoxShape y) => BoxContacts.BoxBox(x, y),
        (CapsuleShape x, CapsuleShape y) => CapsuleContacts.CapsuleCapsule(x, y),
        (CapsuleShape x, SphereShape y) => CapsuleContacts.CapsuleSphere(x, y),
        (SphereShape x, CapsuleShape y) => CapsuleContacts.CapsuleSphere(y, x).Flipped(),
        (CapsuleShape x, BoxShape y) => CapsuleContacts.CapsuleBox(x, y),
        (BoxShape x, CapsuleShape y) => CapsuleContacts.CapsuleBox(y, x).Flipped(),
        _ => Epa.Penetration(a, b),
    };

    /// <summary>
    /// Пятно касания формы с полупространством под плоскостью. Для шара и коробки частные решения; для остальных форм
    /// опорные точки в четырех направлениях, слегка наклоненных от обратной нормали плоскости: на плоском основании
    /// наклон выбирает его крайние точки, на ребре две точки, на вершине и на гладкой поверхности точки сходятся
    /// к самой глубокой.
    /// Нормаль от формы к плоскости
    /// </summary>
    /// <param name="shape">Форма</param>
    /// <param name="plane">Плоскость; нормаль не обязательно единичная</param>
    public static ContactManifold Collide(IConvexShape shape, Plane plane)
    {
        ArgumentNullException.ThrowIfNull(shape);
        if (shape is SphereShape sphere)
            return SphereContacts.SpherePlane(sphere, plane);
        if (shape is BoxShape box)
            return BoxContacts.BoxPlane(box, plane);

        var (normal, offset) = UnitPlane(plane);
        var across = normal.Cross(Math.Abs(normal.X) < 0.57 ? new Vector3(1, 0, 0) : new Vector3(0, 1, 0)).Normalized;
        var third = normal.Cross(across);
        Vector3[] directions = [(across * Tilt) - normal, (-across * Tilt) - normal, (third * Tilt) - normal, (-third * Tilt) - normal];
        var points = new List<ContactPoint>();
        for (int i = 0; i < directions.Length; i++)
        {
            var deepest = shape.Support(directions[i]);
            double height = normal.Dot(deepest) + offset;
            if (height < 0 && points.All(point => point.Position != deepest - (normal * (height / 2))))
                points.Add(new ContactPoint(deepest - (normal * (height / 2)), -height, i));
        }

        return points.Count == 0 ? ContactManifold.Empty : new ContactManifold(-normal, points.Max(point => point.Depth), points);
    }

    /// <summary>Единичная нормаль плоскости и свободный член, деленный на длину нормали</summary>
    internal static (Vector3 Normal, double Offset) UnitPlane(Plane plane)
    {
        ArgumentNullException.ThrowIfNull(plane);
        var normal = Vector3.FromVector(plane.Normal);
        double length = normal.Length;
        if (length <= 0)
            throw new ArgumentException("Нормаль плоскости нулевая", nameof(plane));

        return (normal / length, plane.D / length);
    }
}
