using Foley.Sim.Math;

namespace Foley.Sim.Collision;

/// <summary>
/// Colisión de una curva (patch de rejilla de puntos de control) de CoD1. Ver docs/re/colision.md.
/// Portado de ioquake3 code/qcommon/cm_patch.c (CM_GeneratePatchCollide, CM_SetGridWrapWidth,
/// CM_SubdivideGridColumns, CM_RemoveDegenerateColumns, CM_TransposeGrid, CM_PatchCollideFromGrid,
/// CM_FindPlane, CM_FindPlane2, CM_PlaneEqual, CM_SnapVector, CM_EdgePlaneNum, CM_SetBorderInward,
/// CM_ValidateFacet, CM_AddFacetBevels, CM_CheckFacetPlane, CM_TraceThroughPatchCollide) y de
/// cm_polylib.c (BaseWindingForPlane, ChopWindingInPlace, WindingBounds), con las diferencias de
/// CoD1 comprobadas en CoDSP.exe 1.5:
/// - la subdivisión usa el umbral de cada pieza (campo 8 del lump 24) y no colapsa columnas (@0x4131f0);
/// - límites del mundo de 131072 en vez de 65535 (@0x416b00, @0x414050);
/// - un bisel de arista solo se añade si algún punto de la faceta queda detrás a más de 0.1 (@0x414240);
/// - el trazado parte de leaveFrac = fracción actual y solo copia la normal del plano (@0x415a40).
/// </summary>
public sealed class PatchCollide
{
    private const int MaxGridSize = 129;
    private const int MaxFacets = 1024;
    private const int MaxPatchPlanes = 4096;
    private const int MaxBorders = 4 + 6 + 16;
    private const float PlaneTriEpsilon = 0.1f;
    private const float WrapPointEpsilon = 0.1f;
    private const float PointEpsilon = 0.1f;
    private const double NormalEpsilon = 0.0001;
    private const double DistEpsilon = 0.02;
    private const float MaxMapBounds = 131072f;
    private const float SurfaceClipEpsilon = 0.125f;

    private const int SideFront = 0, SideBack = 1, SideOn = 2;

    private readonly struct PatchPlane
    {
        public readonly float X, Y, Z, D;
        public readonly int SignBits;

        public PatchPlane(float x, float y, float z, float d)
        {
            X = x; Y = y; Z = z; D = d;
            SignBits = (x < 0 ? 1 : 0) | (y < 0 ? 2 : 0) | (z < 0 ? 4 : 0);
        }
    }

    private sealed class Facet
    {
        public int SurfacePlane;
        public int NumBorders;
        public readonly int[] BorderPlanes = new int[MaxBorders];
        public readonly bool[] BorderInward = new bool[MaxBorders];
        public readonly bool[] BorderNoAdjust = new bool[MaxBorders];
    }

    private readonly PatchPlane[] _planes;
    private readonly Facet[] _facets;

    public Vec3 Mins { get; }
    public Vec3 Maxs { get; }
    public int Contents { get; }
    public int SurfaceFlags { get; }
    public int FacetCount => _facets.Length;
    public int PlaneCount => _planes.Length;
    public int MaxBorderCount { get; }

    /// <param name="width">ancho de la rejilla de puntos de control (impar, 3..129)</param>
    /// <param name="height">alto de la rejilla (impar, 3..129)</param>
    /// <param name="points">puntos de control por filas: <c>points[j * width + i]</c></param>
    /// <param name="subdivideDistance">umbral de subdivisión de la pieza (campo 8 del lump 24)</param>
    public PatchCollide(int width, int height, IReadOnlyList<Vec3> points, int subdivideDistance, int contents, int surfaceFlags)
    {
        // Comprobaciones de CM_GeneratePatchCollide (@0x415170).
        if (width < 3 || height < 3 || points.Count < width * height)
            throw new ArgumentException($"CM_GeneratePatchFacets: bad parameters: ({width}, {height})");
        if ((width & 1) == 0 || (height & 1) == 0)
            throw new ArgumentException("CM_GeneratePatchFacets: even sizes are invalid for quadratic meshes");
        if (width > MaxGridSize || height > MaxGridSize)
            throw new ArgumentException("CM_GeneratePatchFacets: source is > MAX_GRID_SIZE");
        Contents = contents;
        SurfaceFlags = surfaceFlags;

        var g = new Grid { Width = width, Height = height };
        for (var i = 0; i < width; i++)
            for (var j = 0; j < height; j++)
                g.Points[i, j] = points[j * width + i];

        g.SetWrapWidth();
        g.SubdivideColumns(subdivideDistance);
        g.RemoveDegenerateColumns();
        g.Transpose();
        g.SetWrapWidth();
        g.SubdivideColumns(subdivideDistance);
        g.RemoveDegenerateColumns();

        float minX = 262144f, minY = 262144f, minZ = 262144f, maxX = -262144f, maxY = -262144f, maxZ = -262144f;
        for (var i = 0; i < g.Width; i++)
            for (var j = 0; j < g.Height; j++)
            {
                var p = g.Points[i, j];
                if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
                if (p.Z < minZ) minZ = p.Z; if (p.Z > maxZ) maxZ = p.Z;
            }

        var b = new Builder();
        b.FromGrid(g);
        _planes = b.Planes.ToArray();
        _facets = b.Facets.ToArray();
        MaxBorderCount = _facets.Length == 0 ? 0 : _facets.Max(f => f.NumBorders);

        // Se amplían en una unidad por los épsilon.
        Mins = new Vec3(minX - 1, minY - 1, minZ - 1);
        Maxs = new Vec3(maxX + 1, maxY + 1, maxZ + 1);
    }

    // ------------------------------------------------------------------ rejilla

    private sealed class Grid
    {
        public int Width, Height;
        public bool WrapWidth, WrapHeight;
        // [ancho][alto]; con espacio para crecer al subdividir
        public readonly Vec3[,] Points = new Vec3[MaxGridSize + 2, MaxGridSize + 2];

        /// <summary>CM_SetGridWrapWidth (@0x413070).</summary>
        public void SetWrapWidth()
        {
            int i;
            for (i = 0; i < Height; i++)
            {
                var a = Points[0, i];
                var c = Points[Width - 1, i];
                int j;
                for (j = 0; j < 3; j++)
                {
                    var d = a[j] - c[j];
                    if (d < -WrapPointEpsilon || d > WrapPointEpsilon) break;
                }
                if (j != 3) break;
            }
            WrapWidth = i == Height;
        }

        /// <summary>
        /// CM_SubdivideGridColumns de CoD1 (@0x4131f0): el criterio es el de Quake 3 con el umbral de la
        /// pieza, pero una columna que no necesita subdivisión se conserva (Quake 3 la colapsa).
        /// </summary>
        public void SubdivideColumns(int threshold)
        {
            var i = 0;
            while (i < Width - 2)
            {
                int j;
                for (j = 0; j < Height; j++)
                {
                    var a = Points[i, j];
                    var m = Points[i + 1, j];
                    var c = Points[i + 2, j];
                    double dx = (double)c.X + a.X - ((double)m.X + m.X);
                    double dy = (double)c.Y + a.Y - ((double)m.Y + m.Y);
                    double dz = (double)c.Z + a.Z - ((double)m.Z + m.Z);
                    var dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz) * 0.25;
                    if (threshold < dist) break;
                }
                if (j == Height)
                {
                    i += 2;
                    continue;
                }

                for (j = 0; j < Height; j++)
                {
                    var prev = Points[i, j];
                    var mid = Points[i + 1, j];
                    var next = Points[i + 2, j];
                    for (var k = Width - 1; k > i + 1; k--)
                        Points[k + 2, j] = Points[k, j];
                    var o1 = Half(prev, mid);
                    var o3 = Half(mid, next);
                    Points[i + 1, j] = o1;
                    Points[i + 2, j] = Half(o1, o3);
                    Points[i + 3, j] = o3;
                }
                Width += 2;
            }
        }

        private static Vec3 Half(Vec3 a, Vec3 b) => new(
            (float)(((double)a.X + b.X) * 0.5), (float)(((double)a.Y + b.Y) * 0.5), (float)(((double)a.Z + b.Z) * 0.5));

        private static bool ComparePoints(Vec3 a, Vec3 b)
        {
            for (var k = 0; k < 3; k++)
            {
                var d = a[k] - b[k];
                if (d < -PointEpsilon || d > PointEpsilon) return false;
            }
            return true;
        }

        /// <summary>CM_RemoveDegenerateColumns (@0x4134d0).</summary>
        public void RemoveDegenerateColumns()
        {
            for (var i = 0; i < Width - 1; i++)
            {
                int j;
                for (j = 0; j < Height; j++)
                    if (!ComparePoints(Points[i, j], Points[i + 1, j])) break;
                if (j != Height) continue;

                for (j = 0; j < Height; j++)
                    for (var k = i + 2; k < Width; k++)
                        Points[k - 1, j] = Points[k, j];
                Width--;
                i--;
            }
        }

        /// <summary>CM_TransposeGrid (@0x412f10).</summary>
        public void Transpose()
        {
            if (Width > Height)
            {
                for (var i = 0; i < Height; i++)
                    for (var j = i + 1; j < Width; j++)
                    {
                        if (j < Height)
                            (Points[i, j], Points[j, i]) = (Points[j, i], Points[i, j]);
                        else
                            Points[i, j] = Points[j, i];
                    }
            }
            else
            {
                for (var i = 0; i < Width; i++)
                    for (var j = i + 1; j < Height; j++)
                    {
                        if (j < Width)
                            (Points[j, i], Points[i, j]) = (Points[i, j], Points[j, i]);
                        else
                            Points[j, i] = Points[i, j];
                    }
            }
            (Width, Height) = (Height, Width);
            (WrapWidth, WrapHeight) = (WrapHeight, WrapWidth);
        }
    }

    // ------------------------------------------------------------------ facetas

    private sealed class Builder
    {
        public readonly List<PatchPlane> Planes = new();
        public readonly List<Facet> Facets = new();

        private static bool PlaneFromPoints(Vec3 a, Vec3 b, Vec3 c, out PatchPlane plane)
        {
            var d1 = b - a;
            var d2 = c - a;
            var n = QMath.Cross(d2, d1);
            if (QMath.Normalize(ref n) == 0) { plane = default; return false; }
            plane = new PatchPlane(n.X, n.Y, n.Z, Vec3.Dot(a, n));
            return true;
        }

        private static float Dist(Vec3 p, PatchPlane pl) => p.X * pl.X + p.Y * pl.Y + p.Z * pl.Z - pl.D;

        /// <summary>CM_FindPlane (@0x413830).</summary>
        public int FindPlane(Vec3 p1, Vec3 p2, Vec3 p3)
        {
            if (!PlaneFromPoints(p1, p2, p3, out var plane)) return -1;
            for (var i = 0; i < Planes.Count; i++)
            {
                var q = Planes[i];
                if (plane.X * q.X + plane.Y * q.Y + plane.Z * q.Z < 0) continue;
                var d = Dist(p1, q);
                if (d < -PlaneTriEpsilon || d > PlaneTriEpsilon) continue;
                d = Dist(p2, q);
                if (d < -PlaneTriEpsilon || d > PlaneTriEpsilon) continue;
                d = Dist(p3, q);
                if (d < -PlaneTriEpsilon || d > PlaneTriEpsilon) continue;
                return i;
            }
            if (Planes.Count == MaxPatchPlanes) throw new InvalidOperationException("MAX_PATCH_PLANES");
            Planes.Add(plane);
            return Planes.Count - 1;
        }

        /// <summary>CM_PlaneEqual (@0x413600).</summary>
        private static bool PlaneEqual(PatchPlane p, float x, float y, float z, float d, out bool flipped)
        {
            if (System.Math.Abs(p.X - x) < NormalEpsilon && System.Math.Abs(p.Y - y) < NormalEpsilon &&
                System.Math.Abs(p.Z - z) < NormalEpsilon && System.Math.Abs(p.D - d) < DistEpsilon)
            {
                flipped = false;
                return true;
            }
            if (System.Math.Abs(p.X + x) < NormalEpsilon && System.Math.Abs(p.Y + y) < NormalEpsilon &&
                System.Math.Abs(p.Z + z) < NormalEpsilon && System.Math.Abs(p.D + d) < DistEpsilon)
            {
                flipped = true;
                return true;
            }
            flipped = false;
            return false;
        }

        /// <summary>CM_FindPlane2 (@0x413750).</summary>
        private int FindPlane2(float x, float y, float z, float d, out bool flipped)
        {
            for (var i = 0; i < Planes.Count; i++)
                if (PlaneEqual(Planes[i], x, y, z, d, out flipped)) return i;
            if (Planes.Count == MaxPatchPlanes) throw new InvalidOperationException("MAX_PATCH_PLANES");
            Planes.Add(new PatchPlane(x, y, z, d));
            flipped = false;
            return Planes.Count - 1;
        }

        private int PointOnPlaneSide(Vec3 p, int planeNum)
        {
            if (planeNum == -1) return SideOn;
            var d = Dist(p, Planes[planeNum]);
            if (d > PlaneTriEpsilon) return SideFront;
            if (d < -PlaneTriEpsilon) return SideBack;
            return SideOn;
        }

        private static int GridPlane(int[,,] gp, int i, int j, int tri)
        {
            var p = gp[i, j, tri];
            if (p != -1) return p;
            p = gp[i, j, 1 - tri];
            return p; // -1 si no se puede resolver
        }

        /// <summary>CM_EdgePlaneNum (en CoD1, en línea dentro de @0x414950).</summary>
        private int EdgePlaneNum(Grid g, int[,,] gp, int i, int j, int k)
        {
            Vec3 p1, p2;
            int p;
            switch (k)
            {
                case 0: // borde superior
                    p1 = g.Points[i, j]; p2 = g.Points[i + 1, j];
                    p = GridPlane(gp, i, j, 0);
                    if (p == -1) return -1;
                    return FindPlane(p1, p2, Up(p1, p));
                case 2: // borde inferior
                    p1 = g.Points[i, j + 1]; p2 = g.Points[i + 1, j + 1];
                    p = GridPlane(gp, i, j, 1);
                    if (p == -1) return -1;
                    return FindPlane(p2, p1, Up(p1, p));
                case 3: // borde izquierdo
                    p1 = g.Points[i, j]; p2 = g.Points[i, j + 1];
                    p = GridPlane(gp, i, j, 1);
                    if (p == -1) return -1;
                    return FindPlane(p2, p1, Up(p1, p));
                case 1: // borde derecho
                    p1 = g.Points[i + 1, j]; p2 = g.Points[i + 1, j + 1];
                    p = GridPlane(gp, i, j, 0);
                    if (p == -1) return -1;
                    return FindPlane(p1, p2, Up(p1, p));
                case 4: // diagonal del triángulo 0
                    p1 = g.Points[i + 1, j + 1]; p2 = g.Points[i, j];
                    p = GridPlane(gp, i, j, 0);
                    if (p == -1) return -1;
                    return FindPlane(p1, p2, Up(p1, p));
                case 5: // diagonal del triángulo 1
                    p1 = g.Points[i, j]; p2 = g.Points[i + 1, j + 1];
                    p = GridPlane(gp, i, j, 1);
                    if (p == -1) return -1;
                    return FindPlane(p1, p2, Up(p1, p));
            }
            throw new ArgumentOutOfRangeException(nameof(k));
        }

        private Vec3 Up(Vec3 p1, int plane)
        {
            var pl = Planes[plane];
            return new Vec3(p1.X + 4 * pl.X, p1.Y + 4 * pl.Y, p1.Z + 4 * pl.Z);
        }

        /// <summary>CM_SetBorderInward (@0x413d00).</summary>
        private void SetBorderInward(Facet f, Grid g, int i, int j, int which)
        {
            Vec3[] pts = which switch
            {
                -1 => new[] { g.Points[i, j], g.Points[i + 1, j], g.Points[i + 1, j + 1], g.Points[i, j + 1] },
                0 => new[] { g.Points[i, j], g.Points[i + 1, j], g.Points[i + 1, j + 1] },
                1 => new[] { g.Points[i + 1, j + 1], g.Points[i, j + 1], g.Points[i, j] },
                _ => throw new ArgumentOutOfRangeException(nameof(which)),
            };
            for (var k = 0; k < f.NumBorders; k++)
            {
                int front = 0, back = 0;
                foreach (var p in pts)
                {
                    var side = PointOnPlaneSide(p, f.BorderPlanes[k]);
                    if (side == SideFront) front++;
                    if (side == SideBack) back++;
                }
                if (front > 0 && back == 0) f.BorderInward[k] = true;
                else if (back > 0 && front == 0) f.BorderInward[k] = false;
                else if (front == 0 && back == 0) f.BorderPlanes[k] = -1; // borde plano
                else f.BorderInward[k] = false;                          // borde que corta la faceta
            }
        }

        private PatchPlane Oriented(int planeNum, bool inward)
        {
            var p = Planes[planeNum];
            return inward ? p : new PatchPlane(-p.X, -p.Y, -p.Z, -p.D);
        }

        /// <summary>CM_ValidateFacet (@0x414050).</summary>
        private bool ValidateFacet(Facet f)
        {
            if (f.SurfacePlane == -1) return false;
            var w = Winding.BaseForPlane(Planes[f.SurfacePlane]);
            for (var j = 0; j < f.NumBorders && w is not null; j++)
            {
                if (f.BorderPlanes[j] == -1) return false;
                var pl = Oriented(f.BorderPlanes[j], f.BorderInward[j]);
                w = Winding.Chop(w, pl.X, pl.Y, pl.Z, pl.D, 0.1f);
            }
            if (w is null) return false;
            w.Bounds(out var mins, out var maxs);
            for (var j = 0; j < 3; j++)
            {
                if (maxs[j] - mins[j] > MaxMapBounds) return false;
                if (mins[j] >= MaxMapBounds) return false;
                if (maxs[j] <= -MaxMapBounds) return false;
            }
            return true;
        }

        private bool AlreadyBorder(Facet f, float x, float y, float z, float d)
        {
            for (var i = 0; i < f.NumBorders; i++)
                if (PlaneEqual(Planes[f.BorderPlanes[i]], x, y, z, d, out _)) return true;
            return false;
        }

        /// <summary>CM_AddFacetBevels de CoD1 (@0x414240).</summary>
        private void AddFacetBevels(Facet f)
        {
            var w = Winding.BaseForPlane(Planes[f.SurfacePlane]);
            for (var j = 0; j < f.NumBorders && w is not null; j++)
            {
                if (f.BorderPlanes[j] == f.SurfacePlane) continue;
                var pl = Oriented(f.BorderPlanes[j], f.BorderInward[j]);
                w = Winding.Chop(w, pl.X, pl.Y, pl.Z, pl.D, 0.1f);
            }
            if (w is null) return;

            w.Bounds(out var mins, out var maxs);

            // Planos axiales.
            for (var axis = 0; axis < 3; axis++)
            {
                for (var dir = -1; dir <= 1; dir += 2)
                {
                    float x = axis == 0 ? dir : 0, y = axis == 1 ? dir : 0, z = axis == 2 ? dir : 0;
                    var d = dir == 1 ? maxs[axis] : -mins[axis];
                    if (PlaneEqual(Planes[f.SurfacePlane], x, y, z, d, out _)) continue;
                    if (AlreadyBorder(f, x, y, z, d)) continue;
                    if (f.NumBorders >= MaxBorders) continue; // "ERROR: too many bevels"
                    f.BorderPlanes[f.NumBorders] = FindPlane2(x, y, z, d, out var flipped);
                    f.BorderNoAdjust[f.NumBorders] = false;
                    f.BorderInward[f.NumBorders] = flipped;
                    f.NumBorders++;
                }
            }

            // Biseles de las aristas no axiales.
            for (var j = 0; j < w.Count; j++)
            {
                var k = (j + 1) % w.Count;
                var vec = w.Points[j] - w.Points[k];
                if (QMath.Normalize(ref vec) < 0.5f) continue;
                vec = SnapVector(vec);
                if (vec.X == -1 || vec.X == 1 || vec.Y == -1 || vec.Y == 1 || vec.Z == -1 || vec.Z == 1) continue;

                for (var axis = 0; axis < 3; axis++)
                {
                    for (var dir = -1; dir <= 1; dir += 2)
                    {
                        var vec2 = new Vec3(axis == 0 ? dir : 0, axis == 1 ? dir : 0, axis == 2 ? dir : 0);
                        var n = QMath.Cross(vec, vec2);
                        if (QMath.Normalize(ref n) < 0.5f) continue;
                        var pd = Vec3.Dot(w.Points[j], n);

                        // Todos los puntos detrás (d <= 0.1) y, solo en CoD1, alguno claramente detrás (d < -0.1).
                        var behind = false;
                        int l;
                        for (l = 0; l < w.Count; l++)
                        {
                            double d = Vec3.Dot(w.Points[l], n) - pd;
                            if (d > 0.1) break;
                            if (d < -0.1) behind = true;
                        }
                        if (l < w.Count || !behind) continue;
                        if (AlreadyBorder(f, n.X, n.Y, n.Z, pd)) continue;
                        if (f.NumBorders >= MaxBorders) continue; // "ERROR: too many bevels"

                        var newPlane = FindPlane2(n.X, n.Y, n.Z, pd, out var flipped);
                        var w2 = w.Copy();
                        var np = Oriented(newPlane, flipped);
                        w2 = Winding.Chop(w2, np.X, np.Y, np.Z, np.D, 0.1f);
                        if (w2 is null) continue; // "invalid bevel"
                        f.BorderPlanes[f.NumBorders] = newPlane;
                        f.BorderNoAdjust[f.NumBorders] = false;
                        f.BorderInward[f.NumBorders] = flipped;
                        f.NumBorders++;
                    }
                }
            }

            // Plano opuesto: el trazado nunca recorta contra la cara de atrás.
            if (f.NumBorders >= MaxBorders) return;
            f.BorderPlanes[f.NumBorders] = f.SurfacePlane;
            f.BorderNoAdjust[f.NumBorders] = false;
            f.BorderInward[f.NumBorders] = true;
            f.NumBorders++;
        }

        private static Vec3 SnapVector(Vec3 v)
        {
            for (var i = 0; i < 3; i++)
            {
                if (System.Math.Abs(v[i] - 1) < NormalEpsilon) return new Vec3(i == 0 ? 1 : 0, i == 1 ? 1 : 0, i == 2 ? 1 : 0);
                if (System.Math.Abs(v[i] + 1) < NormalEpsilon) return new Vec3(i == 0 ? -1 : 0, i == 1 ? -1 : 0, i == 2 ? -1 : 0);
            }
            return v;
        }

        private void AddFacet(Facet f, Grid g, int i, int j, int which)
        {
            SetBorderInward(f, g, i, j, which);
            if (!ValidateFacet(f)) return;
            AddFacetBevels(f);
            Facets.Add(f);
        }

        private Facet NewFacet()
        {
            if (Facets.Count == MaxFacets) throw new InvalidOperationException("MAX_FACETS");
            return new Facet();
        }

        /// <summary>CM_PatchCollideFromGrid (@0x414950).</summary>
        public void FromGrid(Grid g)
        {
            var gp = new int[MaxGridSize, MaxGridSize, 2];
            for (var i = 0; i < g.Width - 1; i++)
                for (var j = 0; j < g.Height - 1; j++)
                {
                    gp[i, j, 0] = FindPlane(g.Points[i, j], g.Points[i + 1, j], g.Points[i + 1, j + 1]);
                    gp[i, j, 1] = FindPlane(g.Points[i + 1, j + 1], g.Points[i, j + 1], g.Points[i, j]);
                }

            const int top = 0, right = 1, bottom = 2, left = 3;
            var borders = new int[4];
            var noAdjust = new bool[4];
            for (var i = 0; i < g.Width - 1; i++)
            {
                for (var j = 0; j < g.Height - 1; j++)
                {
                    borders[top] = -1;
                    if (j > 0) borders[top] = gp[i, j - 1, 1];
                    else if (g.WrapHeight) borders[top] = gp[i, g.Height - 2, 1];
                    noAdjust[top] = borders[top] == gp[i, j, 0];
                    if (borders[top] == -1 || noAdjust[top]) borders[top] = EdgePlaneNum(g, gp, i, j, 0);

                    borders[bottom] = -1;
                    if (j < g.Height - 2) borders[bottom] = gp[i, j + 1, 0];
                    else if (g.WrapHeight) borders[bottom] = gp[i, 0, 0];
                    noAdjust[bottom] = borders[bottom] == gp[i, j, 1];
                    if (borders[bottom] == -1 || noAdjust[bottom]) borders[bottom] = EdgePlaneNum(g, gp, i, j, 2);

                    borders[left] = -1;
                    if (i > 0) borders[left] = gp[i - 1, j, 0];
                    else if (g.WrapWidth) borders[left] = gp[g.Width - 2, j, 0];
                    noAdjust[left] = borders[left] == gp[i, j, 1];
                    if (borders[left] == -1 || noAdjust[left]) borders[left] = EdgePlaneNum(g, gp, i, j, 3);

                    borders[right] = -1;
                    if (i < g.Width - 2) borders[right] = gp[i + 1, j, 1];
                    else if (g.WrapWidth) borders[right] = gp[0, j, 1];
                    noAdjust[right] = borders[right] == gp[i, j, 0];
                    if (borders[right] == -1 || noAdjust[right]) borders[right] = EdgePlaneNum(g, gp, i, j, 1);

                    var f = NewFacet();
                    if (gp[i, j, 0] == gp[i, j, 1])
                    {
                        if (gp[i, j, 0] == -1) continue; // degenerada
                        f.SurfacePlane = gp[i, j, 0];
                        f.NumBorders = 4;
                        f.BorderPlanes[0] = borders[top]; f.BorderNoAdjust[0] = noAdjust[top];
                        f.BorderPlanes[1] = borders[right]; f.BorderNoAdjust[1] = noAdjust[right];
                        f.BorderPlanes[2] = borders[bottom]; f.BorderNoAdjust[2] = noAdjust[bottom];
                        f.BorderPlanes[3] = borders[left]; f.BorderNoAdjust[3] = noAdjust[left];
                        AddFacet(f, g, i, j, -1);
                    }
                    else
                    {
                        // dos triángulos
                        f.SurfacePlane = gp[i, j, 0];
                        f.NumBorders = 3;
                        f.BorderPlanes[0] = borders[top]; f.BorderNoAdjust[0] = noAdjust[top];
                        f.BorderPlanes[1] = borders[right]; f.BorderNoAdjust[1] = noAdjust[right];
                        f.BorderPlanes[2] = gp[i, j, 1];
                        if (f.BorderPlanes[2] == -1)
                        {
                            f.BorderPlanes[2] = borders[bottom];
                            if (f.BorderPlanes[2] == -1) f.BorderPlanes[2] = EdgePlaneNum(g, gp, i, j, 4);
                        }
                        AddFacet(f, g, i, j, 0);

                        f = NewFacet();
                        f.SurfacePlane = gp[i, j, 1];
                        f.NumBorders = 3;
                        f.BorderPlanes[0] = borders[bottom]; f.BorderNoAdjust[0] = noAdjust[bottom];
                        f.BorderPlanes[1] = borders[left]; f.BorderNoAdjust[1] = noAdjust[left];
                        f.BorderPlanes[2] = gp[i, j, 0];
                        if (f.BorderPlanes[2] == -1)
                        {
                            f.BorderPlanes[2] = borders[top];
                            if (f.BorderPlanes[2] == -1) f.BorderPlanes[2] = EdgePlaneNum(g, gp, i, j, 5);
                        }
                        AddFacet(f, g, i, j, 1);
                    }
                }
            }
        }
    }

    // ------------------------------------------------------------------ polígonos (cm_polylib.c)

    private sealed class Winding
    {
        private const int MaxPointsOnWinding = 64;
        public readonly List<Vec3> Points;
        public int Count => Points.Count;

        private Winding(List<Vec3> points) => Points = points;

        public Winding Copy() => new(new List<Vec3>(Points));

        /// <summary>BaseWindingForPlane con MAX_MAP_BOUNDS = 131072 (@0x416b00).</summary>
        public static Winding BaseForPlane(PatchPlane plane)
        {
            var normal = new Vec3(plane.X, plane.Y, plane.Z);
            var max = -MaxMapBounds;
            var x = -1;
            for (var i = 0; i < 3; i++)
            {
                var v = System.Math.Abs(normal[i]);
                if (v > max) { x = i; max = v; }
            }
            if (x == -1) throw new InvalidOperationException("BaseWindingForPlane: no axis found");

            var vup = x == 2 ? new Vec3(1, 0, 0) : new Vec3(0, 0, 1);
            var d = Vec3.Dot(vup, normal);
            vup = Vec3.MA(vup, -d, normal);
            vup = QMath.Normalized(vup);
            var org = normal * plane.D;
            var vright = QMath.Cross(vup, normal);
            vup = vup * MaxMapBounds;
            vright = vright * MaxMapBounds;
            return new Winding(new List<Vec3>(4)
            {
                org - vright + vup,
                org + vright + vup,
                org + vright - vup,
                org - vright - vup,
            });
        }

        /// <summary>ChopWindingInPlace: devuelve null si el polígono queda entero detrás.</summary>
        public static Winding? Chop(Winding w, float nx, float ny, float nz, float dist, float epsilon)
        {
            var n = w.Count;
            var dists = new float[n + 1];
            var sides = new int[n + 1];
            var counts = new int[3];
            for (var i = 0; i < n; i++)
            {
                var p = w.Points[i];
                var dot = p.X * nx + p.Y * ny + p.Z * nz - dist;
                dists[i] = dot;
                sides[i] = dot > epsilon ? SideFront : dot < -epsilon ? SideBack : SideOn;
                counts[sides[i]]++;
            }
            sides[n] = sides[0];
            dists[n] = dists[0];

            if (counts[SideFront] == 0) return null;
            if (counts[SideBack] == 0) return w;

            var f = new List<Vec3>(n + 4);
            for (var i = 0; i < n; i++)
            {
                var p1 = w.Points[i];
                if (sides[i] == SideOn)
                {
                    f.Add(p1);
                    continue;
                }
                if (sides[i] == SideFront) f.Add(p1);
                if (sides[i + 1] == SideOn || sides[i + 1] == sides[i]) continue;

                var p2 = w.Points[(i + 1) % n];
                var dot = dists[i] / (dists[i] - dists[i + 1]);
                float Mid(int j, float normal, float a, float b) =>
                    normal == 1 ? dist : normal == -1 ? -dist : a + dot * (b - a);
                f.Add(new Vec3(Mid(0, nx, p1.X, p2.X), Mid(1, ny, p1.Y, p2.Y), Mid(2, nz, p1.Z, p2.Z)));
            }
            if (f.Count > MaxPointsOnWinding) throw new InvalidOperationException("ClipWinding: MAX_POINTS_ON_WINDING");
            return new Winding(f);
        }

        public void Bounds(out Vec3 mins, out Vec3 maxs)
        {
            float minX = MaxMapBounds, minY = MaxMapBounds, minZ = MaxMapBounds;
            float maxX = -MaxMapBounds, maxY = -MaxMapBounds, maxZ = -MaxMapBounds;
            foreach (var p in Points)
            {
                if (p.X < minX) minX = p.X; if (p.X > maxX) maxX = p.X;
                if (p.Y < minY) minY = p.Y; if (p.Y > maxY) maxY = p.Y;
                if (p.Z < minZ) minZ = p.Z; if (p.Z > maxZ) maxZ = p.Z;
            }
            mins = new Vec3(minX, minY, minZ);
            maxs = new Vec3(maxX, maxY, maxZ);
        }
    }

    // ------------------------------------------------------------------ trazado

    /// <summary>
    /// CM_CheckFacetPlane (@0x415910). d1 y d2 se guardan en float; la fracción se calcula y compara
    /// en precisión extendida (aquí double) y se guarda en float.
    /// </summary>
    private static bool CheckFacetPlane(float px, float py, float pz, float pd, Vec3 start, Vec3 end,
        ref float enterFrac, ref float leaveFrac, out bool hit)
    {
        hit = false;
        var d1 = (float)((double)start.X * px + (double)start.Y * py + (double)start.Z * pz - pd);
        var d2 = (float)((double)end.X * px + (double)end.Y * py + (double)end.Z * pz - pd);

        // completamente delante de la cara: no hay intersección con la faceta
        if (d1 > 0 && (d2 >= SurfaceClipEpsilon || d2 >= d1)) return false;
        // no cruza el plano: no influye
        if (d1 <= 0 && d2 <= 0) return true;

        if (d1 > d2)
        {
            // entra
            var f = ((double)d1 - SurfaceClipEpsilon) / ((double)d1 - d2);
            if (f < 0) f = 0;
            if (f > enterFrac)
            {
                enterFrac = (float)f;
                hit = true;
            }
        }
        else
        {
            // sale
            var f = ((double)d1 + SurfaceClipEpsilon) / ((double)d1 - d2);
            if (f > 1) f = 1;
            if (f < leaveFrac) leaveFrac = (float)f;
        }
        return true;
    }

    /// <summary>
    /// CM_TraceThroughPatchCollide de CoD1 con caja (@0x415a40, rama sin cápsula). <paramref name="start"/>
    /// y <paramref name="end"/> son los centros de la caja simétrica; <paramref name="offsets"/> la
    /// esquina por <c>signbits</c>. Baja <paramref name="fraction"/> si la caja toca la curva antes.
    /// </summary>
    public bool Trace(Vec3 start, Vec3 end, Vec3[] offsets, ref float fraction, out Vec3 normal)
    {
        normal = default;
        var hitAny = false;
        foreach (var facet in _facets)
        {
            var enterFrac = -1f;
            var leaveFrac = fraction;
            var hitnum = -1;
            var best = Vec3.Zero;

            var sp = _planes[facet.SurfacePlane];
            var o = offsets[sp.SignBits];
            var sd = (float)(sp.D - ((double)sp.X * o.X + (double)sp.Y * o.Y + (double)sp.Z * o.Z));
            if (!CheckFacetPlane(sp.X, sp.Y, sp.Z, sd, start, end, ref enterFrac, ref leaveFrac, out var hit)) continue;
            if (hit) best = new Vec3(sp.X, sp.Y, sp.Z);

            int j;
            for (j = 0; j < facet.NumBorders; j++)
            {
                var bp = _planes[facet.BorderPlanes[j]];
                float x, y, z, d;
                if (facet.BorderInward[j]) { x = -bp.X; y = -bp.Y; z = -bp.Z; d = -bp.D; }
                else { x = bp.X; y = bp.Y; z = bp.Z; d = bp.D; }
                // funciona aunque el plano esté invertido porque la caja está centrada
                var bo = offsets[bp.SignBits];
                d = (float)(System.Math.Abs((double)x * bo.X + (double)y * bo.Y + (double)z * bo.Z) + d);
                if (!CheckFacetPlane(x, y, z, d, start, end, ref enterFrac, ref leaveFrac, out hit)) break;
                if (hit)
                {
                    hitnum = j;
                    best = new Vec3(x, y, z);
                }
            }
            if (j < facet.NumBorders) continue;
            // nunca recorta contra la cara de atrás
            if (hitnum == facet.NumBorders - 1) continue;

            if (enterFrac < leaveFrac && enterFrac >= 0 && enterFrac < fraction)
            {
                fraction = enterFrac;
                normal = best;
                hitAny = true;
            }
        }
        return hitAny;
    }
}
