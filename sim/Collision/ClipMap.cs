using Foley.Formats.Bsp;
using Foley.Sim.Math;

namespace Foley.Sim.Collision;

/// <summary>
/// Plano de colisión de un lado de brush. <see cref="SignBits"/>: bit i activo si normal[i] &lt; 0
/// (ioquake3 <c>SetPlaneSignbits</c>). <see cref="SurfaceFlags"/>: banderas de superficie del
/// material del lado (lump 0, offset 64), las que el trazado devuelve como <c>surfaceFlags</c>.
/// </summary>
public readonly record struct CollisionPlane(Vec3 Normal, float Dist, int SignBits, int SurfaceFlags = 0)
{
    public static CollisionPlane Create(Vec3 normal, float dist, int surfaceFlags = 0)
    {
        var bits = 0;
        for (var j = 0; j < 3; j++)
            if (normal[j] < 0f) bits |= 1 << j;
        return new CollisionPlane(normal, dist, bits, surfaceFlags);
    }
}

/// <summary>Brush convexo: sus planos van en <c>ClipMap.Planes[FirstPlane..FirstPlane+PlaneCount)</c>; los 6 primeros son axiales.</summary>
public readonly record struct CollisionBrush(int FirstPlane, int PlaneCount, int Contents, Vec3 Mins, Vec3 Maxs);

/// <summary>Resultado de un trazado (equivalente a <c>trace_t</c> de ioquake3).</summary>
public readonly record struct TraceResult(
    float Fraction,
    Vec3 EndPos,
    CollisionPlane Plane,
    bool StartSolid,
    bool AllSolid,
    int Contents)
{
    /// <summary>Banderas de superficie del lado tocado (<c>trace.surfaceFlags</c>, del <c>leadside</c>).</summary>
    public int SurfaceFlags => Plane.SurfaceFlags;

    public bool Hit => Fraction < 1f;
}

/// <summary>
/// Mapa de colisión: brushes del mundo y trazado de cajas. Ver docs/re/colision.md.
/// Portado de ioquake3 code/qcommon/cm_trace.c (CM_Trace, CM_TraceThroughBrush, CM_TestBoxInBrush)
/// y code/qcommon/cm_test.c (CM_BoundsIntersect). Sin árbol BSP: rejilla propia en XY.
/// </summary>
public sealed class ClipMap
{
    /// <summary>ioquake3 cm_local.h <c>SURFACE_CLIP_EPSILON</c>. Es <c>double</c> en C y así promueve las expresiones.</summary>
    public const double SurfaceClipEpsilon = 0.125;

    /// <summary>Bits de contenido (nombres probables, ver docs/formats/bsp.md).</summary>
    public const int ContentsSolid = 0x1;
    public const int ContentsPlayerClip = 0x10000;

    /// <summary>
    /// Máscara de lo que bloquea al jugador.
    /// TODO(fidelidad): supuesto (sólido | clip de jugador, como MASK_PLAYERSOLID de Q3 sin cuerpos); confirmar con REA.
    /// </summary>
    public const int MaskPlayerSolid = ContentsSolid | ContentsPlayerClip;

    private const float CellSize = 512f;

    public CollisionPlane[] Planes { get; }
    public CollisionBrush[] Brushes { get; }
    /// <summary>Piezas de terreno (malla de triángulos). Las curvas aún no están portadas.</summary>
    public TerrainCollide[] Terrains { get; }

    private readonly float _gridMinX, _gridMinY;
    private readonly int _gridW, _gridH;
    private readonly int[][] _cells;
    private readonly int[][] _terrainCells;
    private readonly int[] _stamp;
    private readonly int[] _terrainStamp;
    private int _checkCount;
    private int _traceCount; // DAT_014bd720: marca de vértices/aristas de terreno ya probados en este trazado
    private readonly List<int> _candidates = new();
    private readonly List<int> _terrainCandidates = new();

    public ClipMap(CollisionPlane[] planes, CollisionBrush[] brushes, TerrainCollide[]? terrains = null)
    {
        Planes = planes;
        Brushes = brushes;
        Terrains = terrains ?? Array.Empty<TerrainCollide>();
        _stamp = new int[brushes.Length];
        _terrainStamp = new int[Terrains.Length];

        if (brushes.Length == 0 && Terrains.Length == 0)
        {
            _gridW = _gridH = 1;
            _cells = new[] { Array.Empty<int>() };
            _terrainCells = new[] { Array.Empty<int>() };
            return;
        }

        float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
        foreach (var b in brushes)
        {
            minX = MathF.Min(minX, b.Mins.X); minY = MathF.Min(minY, b.Mins.Y);
            maxX = MathF.Max(maxX, b.Maxs.X); maxY = MathF.Max(maxY, b.Maxs.Y);
        }
        foreach (var t in Terrains)
        {
            minX = MathF.Min(minX, t.Mins.X); minY = MathF.Min(minY, t.Mins.Y);
            maxX = MathF.Max(maxX, t.Maxs.X); maxY = MathF.Max(maxY, t.Maxs.Y);
        }
        _gridMinX = minX;
        _gridMinY = minY;
        _gridW = System.Math.Max(1, (int)MathF.Ceiling((maxX - minX) / CellSize) + 1);
        _gridH = System.Math.Max(1, (int)MathF.Ceiling((maxY - minY) / CellSize) + 1);

        _cells = BuildCells(brushes.Length, i => (brushes[i].Mins, brushes[i].Maxs));
        _terrainCells = BuildCells(Terrains.Length, i => (Terrains[i].Mins, Terrains[i].Maxs));
    }

    private int[][] BuildCells(int count, Func<int, (Vec3 min, Vec3 max)> bounds)
    {
        var lists = new List<int>[_gridW * _gridH];
        for (var i = 0; i < count; i++)
        {
            var (min, max) = bounds(i);
            CellRange(min.X, min.Y, max.X, max.Y, out var x0, out var y0, out var x1, out var y1);
            for (var y = y0; y <= y1; y++)
                for (var x = x0; x <= x1; x++)
                    (lists[y * _gridW + x] ??= new List<int>()).Add(i);
        }
        return lists.Select(l => l?.ToArray() ?? Array.Empty<int>()).ToArray();
    }

    /// <summary>Construye la colisión del modelo 0 (mundo) de un BSP. Ver docs/formats/bsp.md.</summary>
    public static ClipMap FromBsp(BspFile bsp)
    {
        if (bsp.Models.Length == 0) return new ClipMap(Array.Empty<CollisionPlane>(), Array.Empty<CollisionBrush>());

        var world = bsp.Models[0];
        var planes = new List<CollisionPlane>();
        var brushes = new CollisionBrush[world.BrushCount];
        for (var i = 0; i < brushes.Length; i++)
        {
            var b = bsp.Brushes[world.FirstBrush + i];
            var side = b.FirstSide;
            var min = new Vec3(bsp.BrushSides[side].AxialDistance, bsp.BrushSides[side + 2].AxialDistance, bsp.BrushSides[side + 4].AxialDistance);
            var max = new Vec3(bsp.BrushSides[side + 1].AxialDistance, bsp.BrushSides[side + 3].AxialDistance, bsp.BrushSides[side + 5].AxialDistance);

            var first = planes.Count;
            // Lados axiales en el orden de Quake 3: -X, +X, -Y, +Y, -Z, +Z.
            for (var axis = 0; axis < 3; axis++)
            {
                planes.Add(CollisionPlane.Create(Vec3.Zero.With(axis, -1f), -min[axis], SideFlags(bsp, side + axis * 2)));
                planes.Add(CollisionPlane.Create(Vec3.Zero.With(axis, 1f), max[axis], SideFlags(bsp, side + axis * 2 + 1)));
            }
            for (var k = BspBrushSide.AxialCount; k < b.SideCount; k++)
            {
                var p = bsp.Planes[bsp.BrushSides[side + k].PlaneIndex];
                planes.Add(CollisionPlane.Create(new Vec3(p.Nx, p.Ny, p.Nz), p.Dist, SideFlags(bsp, side + k)));
            }
            var contents = (int)bsp.Materials[b.MaterialIndex].ContentFlags;
            brushes[i] = new CollisionBrush(first, b.SideCount, contents, min, max);
        }

        // Piezas de terreno (CMod_LoadLeafpatchesAndTerrain @0x411fd0). Curvas: TODO(fidelidad), sin portar.
        var terrains = new List<TerrainCollide>();
        foreach (var p in bsp.CollisionPatches)
        {
            if (p.Type != BspCollisionPatch.TypeTerrain) continue;
            var verts = new Vec3[p.Dim0];
            for (var k = 0; k < verts.Length; k++)
            {
                var o = (int)(p.Field8 + k) * 3;
                verts[k] = new Vec3(bsp.CollisionVerts[o], bsp.CollisionVerts[o + 1], bsp.CollisionVerts[o + 2]);
            }
            var idx = new int[p.Dim1];
            for (var k = 0; k < idx.Length; k++) idx[k] = bsp.CollisionIndices[p.Field12 + k];
            var mat = bsp.Materials[p.MaterialIndex];
            terrains.Add(new TerrainCollide(verts, idx, (int)mat.ContentFlags, (int)mat.SurfaceFlags));
        }
        return new ClipMap(planes.ToArray(), brushes, terrains.ToArray());
    }

    private static int SideFlags(BspFile bsp, int sideIndex)
    {
        var m = bsp.BrushSides[sideIndex].MaterialIndex;
        return m < bsp.Materials.Length ? (int)bsp.Materials[m].SurfaceFlags : 0;
    }

    /// <summary>
    /// Traza una caja de <paramref name="start"/> a <paramref name="end"/> contra los brushes cuyo
    /// contenido coincide con <paramref name="brushMask"/>. Port de ioquake3 cm_trace.c <c>CM_Trace</c> (modelo 0, sin cápsula).
    /// </summary>
    public TraceResult Trace(Vec3 start, Vec3 end, Vec3 mins, Vec3 maxs, int brushMask)
    {
        var tw = new TraceWork { Fraction = 1f, Mask = brushMask };

        // Ajuste para que mins y maxs sean simétricos.
        Span<float> offset = stackalloc float[3];
        for (var i = 0; i < 3; i++)
        {
            offset[i] = (mins[i] + maxs[i]) * 0.5f;
            tw.Size0 = tw.Size0.With(i, mins[i] - offset[i]);
            tw.Size1 = tw.Size1.With(i, maxs[i] - offset[i]);
            tw.Start = tw.Start.With(i, start[i] + offset[i]);
            tw.End = tw.End.With(i, end[i] + offset[i]);
        }

        // offsets[signbits] = vector a la esquina correspondiente.
        tw.Offsets = new Vec3[8];
        for (var bits = 0; bits < 8; bits++)
        {
            tw.Offsets[bits] = new Vec3(
                (bits & 1) != 0 ? tw.Size1.X : tw.Size0.X,
                (bits & 2) != 0 ? tw.Size1.Y : tw.Size0.Y,
                (bits & 4) != 0 ? tw.Size1.Z : tw.Size0.Z);
        }

        for (var i = 0; i < 3; i++)
        {
            if (tw.Start[i] < tw.End[i])
            {
                tw.BoundsMin = tw.BoundsMin.With(i, tw.Start[i] + tw.Size0[i]);
                tw.BoundsMax = tw.BoundsMax.With(i, tw.End[i] + tw.Size1[i]);
            }
            else
            {
                tw.BoundsMin = tw.BoundsMin.With(i, tw.End[i] + tw.Size0[i]);
                tw.BoundsMax = tw.BoundsMax.With(i, tw.Start[i] + tw.Size1[i]);
            }
        }

        var positionTest = start.X == end.X && start.Y == end.Y && start.Z == end.Z;
        GatherCandidates(tw.BoundsMin, tw.BoundsMax);
        foreach (var index in _candidates)
        {
            var b = Brushes[index];
            if ((b.Contents & tw.Mask) == 0) continue;
            if (positionTest)
            {
                // CM_PositionTest -> CM_TestInLeaf
                TestBoxInBrush(ref tw, b);
                if (tw.AllSolid) break;
            }
            else
            {
                // CM_TraceThroughLeaf
                if (!BoundsIntersect(tw.BoundsMin, tw.BoundsMax, b.Mins, b.Maxs)) continue;
                TraceThroughBrush(ref tw, b);
                if (tw.Fraction == 0f) break;
            }
        }

        if (Terrains.Length > 0 && tw.Fraction > 0 && !tw.AllSolid && !IsPointTrace(tw))
            TraceTerrains(ref tw);

        // endpos a partir del start/end originales, sin ajustar.
        Vec3 endPos;
        if (tw.Fraction == 1f) endPos = end;
        else
            endPos = new Vec3(
                start.X + tw.Fraction * (end.X - start.X),
                start.Y + tw.Fraction * (end.Y - start.Y),
                start.Z + tw.Fraction * (end.Z - start.Z));

        return new TraceResult(tw.Fraction, endPos, tw.Plane, tw.StartSolid, tw.AllSolid, tw.Contents);
    }

    // Los trazados de punto contra terreno usan otra rutina (@0x419580), sin portar: TODO(fidelidad).
    private static bool IsPointTrace(TraceWork tw) =>
        tw.Size0 == Vec3.Zero && tw.Size1 == Vec3.Zero;

    /// <summary>
    /// Terreno: cápsula de radio size[1].x y media altura size[1].z (CM_Trace @0x41e960, @0x41a2d0),
    /// solo piezas cuyos límites tocan los del barrido (@0x41b690) y cuyo contenido entra en la máscara;
    /// al bajar la fracción toma el contenido y las banderas de superficie de la pieza (@0x41b860).
    /// </summary>
    private void TraceTerrains(ref TraceWork tw)
    {
        GatherTerrainCandidates(tw.BoundsMin, tw.BoundsMax);
        if (_terrainCandidates.Count == 0) return;
        _traceCount++;
        var delta = new Vec3(
            (float)((double)tw.End.X - tw.Start.X), (float)((double)tw.End.Y - tw.Start.Y), (float)((double)tw.End.Z - tw.Start.Z));
        var sweep = new TerrainCollide.SweepState
        {
            Start = tw.Start,
            End = tw.End,
            Delta = delta,
            DeltaLenSq = (float)((double)delta.X * delta.X + (double)delta.Y * delta.Y + (double)delta.Z * delta.Z),
            Radius = tw.Size1.X,
            HalfHeight = tw.Size1.Z,
            Fraction = tw.Fraction,
            CheckCount = _traceCount,
        };
        foreach (var i in _terrainCandidates)
        {
            var t = Terrains[i];
            if ((t.Contents & tw.Mask) == 0) continue;
            if (tw.BoundsMin.X > t.Maxs.X || tw.BoundsMax.X < t.Mins.X ||
                tw.BoundsMin.Y > t.Maxs.Y || tw.BoundsMax.Y < t.Mins.Y ||
                tw.BoundsMin.Z > t.Maxs.Z || tw.BoundsMax.Z < t.Mins.Z) continue;
            var before = sweep.Fraction;
            sweep.StartSolid = false;
            t.Trace(ref sweep);
            if (sweep.StartSolid) tw.StartSolid = true;
            if (sweep.Fraction < before)
            {
                tw.Fraction = sweep.Fraction;
                tw.Plane = new CollisionPlane(sweep.Normal, 0, 0, t.SurfaceFlags);
                tw.Contents = t.Contents;
            }
            if (sweep.Fraction == 0) break;
        }
    }

    private void GatherTerrainCandidates(Vec3 min, Vec3 max)
    {
        _terrainCandidates.Clear();
        if (++_checkCount == int.MaxValue)
        {
            Array.Clear(_stamp);
            Array.Clear(_terrainStamp);
            _checkCount = 1;
        }
        CellRange(min.X, min.Y, max.X, max.Y, out var x0, out var y0, out var x1, out var y1);
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                foreach (var t in _terrainCells[y * _gridW + x])
                {
                    if (_terrainStamp[t] == _checkCount) continue;
                    _terrainStamp[t] = _checkCount;
                    _terrainCandidates.Add(t);
                }
        _terrainCandidates.Sort();
    }

    private struct TraceWork
    {
        public Vec3 Start, End, Size0, Size1, BoundsMin, BoundsMax;
        public Vec3[] Offsets;
        public int Mask;
        public float Fraction;
        public CollisionPlane Plane;
        public bool StartSolid, AllSolid;
        public int Contents;
    }

    /// <summary>Port de ioquake3 cm_trace.c <c>CM_TraceThroughBrush</c> (rama de caja, sin cápsula).</summary>
    private void TraceThroughBrush(ref TraceWork tw, CollisionBrush brush)
    {
        var enterFrac = -1f;
        var leaveFrac = 1f;
        var hasClipPlane = false;
        CollisionPlane clipPlane = default;
        var getOut = false;
        var startOut = false;

        if (brush.PlaneCount == 0) return;

        for (var i = 0; i < brush.PlaneCount; i++)
        {
            var plane = Planes[brush.FirstPlane + i];

            // Ajuste de la distancia del plano según mins/maxs.
            var dist = plane.Dist - Vec3.Dot(tw.Offsets[plane.SignBits], plane.Normal);
            var d1 = Vec3.Dot(tw.Start, plane.Normal) - dist;
            var d2 = Vec3.Dot(tw.End, plane.Normal) - dist;

            if (d2 > 0f) getOut = true; // el punto final no está en sólido
            if (d1 > 0f) startOut = true;

            // Completamente delante de la cara: no hay intersección con el brush.
            if (d1 > 0f && (d2 >= SurfaceClipEpsilon || d2 >= d1)) return;

            // No cruza el plano: el plano no importa.
            if (d1 <= 0f && d2 <= 0f) continue;

            // Cruza la cara.
            if (d1 > d2)
            {
                // entra
                var f = (float)((d1 - SurfaceClipEpsilon) / (d1 - d2));
                if (f < 0f) f = 0f;
                if (f > enterFrac)
                {
                    enterFrac = f;
                    clipPlane = plane;
                    hasClipPlane = true;
                }
            }
            else
            {
                // sale
                var f = (float)((d1 + SurfaceClipEpsilon) / (d1 - d2));
                if (f > 1f) f = 1f;
                if (f < leaveFrac) leaveFrac = f;
            }
        }

        // Todos los planos revisados y el trazado no quedó completamente fuera del brush.
        if (!startOut)
        {
            // El punto inicial estaba dentro del brush.
            tw.StartSolid = true;
            if (!getOut)
            {
                tw.AllSolid = true;
                tw.Fraction = 0f;
                tw.Contents = brush.Contents;
            }
            return;
        }

        if (enterFrac < leaveFrac && enterFrac > -1f && enterFrac < tw.Fraction)
        {
            if (enterFrac < 0f) enterFrac = 0f;
            tw.Fraction = enterFrac;
            if (hasClipPlane) tw.Plane = clipPlane;
            tw.Contents = brush.Contents;
        }
    }

    /// <summary>Port de ioquake3 cm_trace.c <c>CM_TestBoxInBrush</c> (rama de caja, sin cápsula).</summary>
    private void TestBoxInBrush(ref TraceWork tw, CollisionBrush brush)
    {
        if (brush.PlaneCount == 0) return;

        // Prueba especial para los planos axiales.
        if (tw.BoundsMin.X > brush.Maxs.X || tw.BoundsMin.Y > brush.Maxs.Y || tw.BoundsMin.Z > brush.Maxs.Z ||
            tw.BoundsMax.X < brush.Mins.X || tw.BoundsMax.Y < brush.Mins.Y || tw.BoundsMax.Z < brush.Mins.Z)
            return;

        // Los 6 primeros planos son los axiales: solo hace falta probar el resto.
        for (var i = 6; i < brush.PlaneCount; i++)
        {
            var plane = Planes[brush.FirstPlane + i];
            var dist = plane.Dist - Vec3.Dot(tw.Offsets[plane.SignBits], plane.Normal);
            var d1 = Vec3.Dot(tw.Start, plane.Normal) - dist;
            if (d1 > 0f) return; // completamente delante de la cara
        }

        // Dentro del brush.
        tw.StartSolid = tw.AllSolid = true;
        tw.Fraction = 0f;
        tw.Contents = brush.Contents;
    }

    /// <summary>Port de ioquake3 cm_test.c <c>CM_BoundsIntersect</c> (comparaciones promovidas a double como en C).</summary>
    private static bool BoundsIntersect(Vec3 mins, Vec3 maxs, Vec3 mins2, Vec3 maxs2) =>
        !(maxs.X < mins2.X - SurfaceClipEpsilon || maxs.Y < mins2.Y - SurfaceClipEpsilon || maxs.Z < mins2.Z - SurfaceClipEpsilon ||
          mins.X > maxs2.X + SurfaceClipEpsilon || mins.Y > maxs2.Y + SurfaceClipEpsilon || mins.Z > maxs2.Z + SurfaceClipEpsilon);

    /// <summary>Brushes de las celdas que tocan la caja, sin repetir y en orden de índice (determinista).</summary>
    private void GatherCandidates(Vec3 min, Vec3 max)
    {
        _candidates.Clear();
        if (++_checkCount == int.MaxValue)
        {
            Array.Clear(_stamp);
            _checkCount = 1;
        }
        var eps = (float)SurfaceClipEpsilon;
        CellRange(min.X - eps, min.Y - eps, max.X + eps, max.Y + eps, out var x0, out var y0, out var x1, out var y1);
        for (var y = y0; y <= y1; y++)
            for (var x = x0; x <= x1; x++)
                foreach (var b in _cells[y * _gridW + x])
                {
                    if (_stamp[b] == _checkCount) continue;
                    _stamp[b] = _checkCount;
                    _candidates.Add(b);
                }
        _candidates.Sort();
    }

    private void CellRange(float minX, float minY, float maxX, float maxY, out int x0, out int y0, out int x1, out int y1)
    {
        x0 = Clamp((int)MathF.Floor((minX - _gridMinX) / CellSize), _gridW);
        y0 = Clamp((int)MathF.Floor((minY - _gridMinY) / CellSize), _gridH);
        x1 = Clamp((int)MathF.Floor((maxX - _gridMinX) / CellSize), _gridW);
        y1 = Clamp((int)MathF.Floor((maxY - _gridMinY) / CellSize), _gridH);
    }

    private static int Clamp(int v, int size) => v < 0 ? 0 : v >= size ? size - 1 : v;
}
