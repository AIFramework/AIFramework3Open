#nullable enable

using System;
using System.Collections.Generic;
using AI.Geometry.Primitives;

namespace AI.Geometry.Hull;

/// <summary>
/// Выпуклая оболочка множества точек в 3D, построенная алгоритмом Quickhull.
/// </summary>
/// <remarks>
/// <para>
/// Начальный тетраэдр строится на крайних точках; затем к оболочке по одной добавляются самые далекие
/// внешние точки: видимые из точки грани удаляются, а край видимой области (горизонт) соединяется с ней
/// новыми гранями. Точки, оказавшиеся внутри, отбрасываются.
/// </para>
/// <para>
/// Точка считается внешней, только если она выше плоскости грани больше чем на допуск
/// 3·ε·(max|x| + max|y| + max|z|) (как у Ллойда в QuickHull3D). Поэтому повторяющиеся точки и точки,
/// лежащие на гранях или ребрах, в оболочку не попадают. Грани не сливаются: плоская грань оболочки
/// представлена несколькими треугольниками в одной плоскости.
/// </para>
/// </remarks>
public sealed class ConvexHull3D
{
    private ConvexHull3D(Vector3[] vertices, HullFace[] faces)
    {
        Vertices = vertices;
        Faces = faces;
        var triangles = new (int A, int B, int C)[faces.Length];

        for (int i = 0; i < faces.Length; i++)
            triangles[i] = (faces[i].A, faces[i].B, faces[i].C);

        Triangles = triangles;
    }

    /// <summary>
    /// Вершины оболочки (только те исходные точки, что стали вершинами).
    /// </summary>
    public IReadOnlyList<Vector3> Vertices { get; }

    /// <summary>
    /// Треугольные грани с внешними нормалями.
    /// </summary>
    public IReadOnlyList<HullFace> Faces { get; }

    /// <summary>
    /// Грани как тройки индексов вершин, например для <see cref="MassProperties.MeshMass.Compute"/>.
    /// </summary>
    public IReadOnlyList<(int A, int B, int C)> Triangles { get; }

    /// <summary>
    /// Проверяет, лежит ли точка внутри оболочки или на ее границе.
    /// </summary>
    /// <param name="point">Точка.</param>
    /// <param name="tolerance">Допустимый выход за плоскость грани.</param>
    public bool Contains(Vector3 point, double tolerance = 1e-9)
    {
        foreach (HullFace face in Faces)
        {
            if (face.SignedDistance(point) > tolerance)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Строит выпуклую оболочку.
    /// </summary>
    /// <param name="points">Исходные точки; допускаются повторы и точки на гранях.</param>
    /// <exception cref="ArgumentException">Точек меньше четырех либо все они лежат в одной плоскости.</exception>
    public static ConvexHull3D Build(IReadOnlyList<Vector3> points)
    {
        ArgumentNullException.ThrowIfNull(points);

        if (points.Count < 4)
            throw new ArgumentException("Для объемной оболочки нужно не меньше четырех точек", nameof(points));

        return new Builder(points).Run();
    }

    /// <summary>
    /// Состояние одного построения.
    /// </summary>
    private sealed class Builder
    {
        private readonly IReadOnlyList<Vector3> _points;
        private readonly Dictionary<(int From, int To), Face> _edges = new();
        private readonly List<Face> _faces = new();
        private readonly double _tolerance;
        private int _stamp;

        public Builder(IReadOnlyList<Vector3> points)
        {
            _points = points;
            Vector3 extent = Vector3.Zero;

            foreach (Vector3 p in points)
                extent = Vector3.Max(extent, Vector3.Abs(p));

            _tolerance = 3 * 2.220446049250313e-16 * (extent.X + extent.Y + extent.Z);
        }

        public ConvexHull3D Run()
        {
            var (i0, i1, i2, i3) = InitialSimplex();
            Vector3 inside = (_points[i0] + _points[i1] + _points[i2] + _points[i3]) / 4;
            var pending = new Stack<Face>();

            foreach (var (a, b, c) in new[] { (i0, i1, i2), (i0, i1, i3), (i0, i2, i3), (i1, i2, i3) })
            {
                // Нормаль каждой грани направляется от внутренней точки тетраэдра
                Vector3 normal = (_points[b] - _points[a]).Cross(_points[c] - _points[a]);

                if (normal.Dot(inside - _points[a]) > 0)
                    AddFace(a, c, b);
                else
                    AddFace(a, b, c);
            }

            var initial = _faces.ToArray();
            var all = new List<int>(_points.Count);

            for (int i = 0; i < _points.Count; i++)
            {
                if (i != i0 && i != i1 && i != i2 && i != i3)
                    all.Add(i);
            }

            Assign(all, initial, pending);

            while (pending.Count > 0)
            {
                Face face = pending.Pop();

                if (face.Alive && face.Outside.Count > 0)
                    AddPoint(face, pending);
            }

            return Collect();
        }

        /// <summary>
        /// Четыре точки общего положения: самая протяженная пара по осям, самая далекая от их прямой
        /// и самая далекая от плоскости трех первых.
        /// </summary>
        private (int, int, int, int) InitialSimplex()
        {
            Vector3 lo = _points[0], hi = _points[0], mean = Vector3.Zero;

            foreach (Vector3 p in _points)
            {
                lo = Vector3.Min(lo, p);
                hi = Vector3.Max(hi, p);
                mean += p;
            }

            mean /= _points.Count;
            Vector3 size = hi - lo;
            int axis = size.X >= size.Y && size.X >= size.Z ? 0 : size.Y >= size.Z ? 1 : 2;

            if (size[axis] <= _tolerance)
                throw new ArgumentException("Все точки совпадают", "points");

            var all = new int[_points.Count];

            for (int i = 0; i < all.Length; i++)
                all[i] = i;

            int i0 = Extreme(all, p => -p[axis], mean, out _);
            int i1 = Extreme(all, p => p[axis], mean, out _);
            Vector3 p0 = _points[i0];
            Vector3 direction = (_points[i1] - p0).Normalized;
            int i2 = Extreme(all, p => (p - p0).Cross(direction).Length, mean, out double lineDistance);

            if (lineDistance <= _tolerance)
                throw new ArgumentException("Все точки лежат на одной прямой", "points");

            Vector3 normal = (_points[i1] - p0).Cross(_points[i2] - p0).Normalized;
            int i3 = Extreme(all, p => Math.Abs(normal.Dot(p - p0)), mean, out double planeDistance);

            if (planeDistance <= _tolerance)
                throw new ArgumentException("Все точки лежат в одной плоскости", "points");

            return (i0, i1, i2, i3);
        }

        /// <summary>
        /// Точка с наибольшим значением выпуклой функции key. Среди равных в пределах допуска берется
        /// самая далекая от reference: строго выпуклая добавка гарантирует, что выбрана вершина
        /// оболочки, а не середина ребра или грани, на которых key постоянна.
        /// </summary>
        private int Extreme(IReadOnlyList<int> indices, Func<Vector3, double> key, Vector3 reference, out double best)
        {
            int index = indices[0];
            best = double.NegativeInfinity;
            double spread = double.NegativeInfinity;

            foreach (int i in indices)
            {
                Vector3 p = _points[i];
                double d = key(p);
                double s = (p - reference).LengthSquared;

                if (d > best + _tolerance || (d >= best - _tolerance && s > spread))
                {
                    index = i;
                    spread = s;
                    best = Math.Max(best, d);
                }
            }

            return index;
        }

        /// <summary>
        /// Добавляет в оболочку самую далекую внешнюю точку грани.
        /// </summary>
        private void AddPoint(Face start, Stack<Face> pending)
        {
            int eye = Extreme(start.Outside, start.Distance, start.Centroid, out _);
            Vector3 eyePoint = _points[eye];
            int stamp = ++_stamp;
            var visible = new List<Face> { start };
            var horizon = new List<(int From, int To)>();
            start.Stamp = stamp;

            // Обход в глубину по соседям: видимая область связна, ее край и есть горизонт
            for (int v = 0; v < visible.Count; v++)
            {
                Face face = visible[v];

                foreach (var (from, to) in face.Edges())
                {
                    Face neighbor = _edges[(to, from)];

                    if (neighbor.Stamp == stamp)
                        continue;

                    if (neighbor.Distance(eyePoint) > _tolerance)
                    {
                        neighbor.Stamp = stamp;
                        visible.Add(neighbor);
                    }
                    else
                    {
                        horizon.Add((from, to));
                    }
                }
            }

            var orphans = new List<int>();

            foreach (Face face in visible)
            {
                foreach (int index in face.Outside)
                {
                    if (index != eye)
                        orphans.Add(index);
                }

                RemoveFace(face);
            }

            var created = new Face[horizon.Count];

            for (int i = 0; i < horizon.Count; i++)
                created[i] = AddFace(horizon[i].From, horizon[i].To, eye);

            Assign(orphans, created, pending);
        }

        /// <summary>
        /// Раздает точки граням, над которыми они лежат; прочие точки внутри и отбрасываются.
        /// </summary>
        private void Assign(List<int> indices, Face[] faces, Stack<Face> pending)
        {
            foreach (int index in indices)
            {
                Vector3 p = _points[index];

                foreach (Face face in faces)
                {
                    if (face.Distance(p) > _tolerance)
                    {
                        face.Outside.Add(index);
                        break;
                    }
                }
            }

            foreach (Face face in faces)
            {
                if (face.Outside.Count > 0)
                    pending.Push(face);
            }
        }

        private Face AddFace(int a, int b, int c)
        {
            var face = new Face(a, b, c, _points[a], _points[b], _points[c]);
            _faces.Add(face);

            foreach (var edge in face.Edges())
                _edges[edge] = face;

            return face;
        }

        private void RemoveFace(Face face)
        {
            face.Alive = false;

            foreach (var edge in face.Edges())
            {
                if (_edges.TryGetValue(edge, out Face? owner) && owner == face)
                    _edges.Remove(edge);
            }
        }

        /// <summary>
        /// Перенумеровывает вершины живых граней подряд.
        /// </summary>
        private ConvexHull3D Collect()
        {
            var map = new Dictionary<int, int>();
            var vertices = new List<Vector3>();
            var faces = new List<HullFace>();

            int Map(int index)
            {
                if (!map.TryGetValue(index, out int mapped))
                {
                    mapped = vertices.Count;
                    map[index] = mapped;
                    vertices.Add(_points[index]);
                }

                return mapped;
            }

            foreach (Face face in _faces)
            {
                if (face.Alive)
                    faces.Add(new HullFace(Map(face.A), Map(face.B), Map(face.C), face.Normal, face.Offset));
            }

            return new ConvexHull3D(vertices.ToArray(), faces.ToArray());
        }
    }

    /// <summary>
    /// Грань в процессе построения.
    /// </summary>
    private sealed class Face
    {
        public Face(int a, int b, int c, Vector3 pa, Vector3 pb, Vector3 pc)
        {
            A = a;
            B = b;
            C = c;
            Normal = (pb - pa).Cross(pc - pa).Normalized;
            Offset = Normal.Dot(pa);
            Centroid = (pa + pb + pc) / 3;
        }

        public Vector3 Centroid { get; }

        public int A { get; }

        public int B { get; }

        public int C { get; }

        public Vector3 Normal { get; }

        public double Offset { get; }

        public List<int> Outside { get; } = new();

        public bool Alive { get; set; } = true;

        public int Stamp { get; set; }

        public double Distance(Vector3 p) => Normal.Dot(p) - Offset;

        public (int From, int To)[] Edges() => [(A, B), (B, C), (C, A)];
    }
}
