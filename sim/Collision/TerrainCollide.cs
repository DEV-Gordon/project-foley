using Foley.Sim.Math;

namespace Foley.Sim.Collision;

/// <summary>
/// Colisión de una pieza de terreno (malla de triángulos) de CoD1. Ver docs/re/colision.md.
/// Construcción: CM_GenerateTerrainCollide (CoDSP.exe 1.5 @0x418860).
/// Trazado: barrido de una esfera (la inferior de la cápsula que sustituye a la caja) contra
/// caras, aristas y vértices (@0x4196f0, llamado desde @0x41a2d0 / @0x41a350).
/// </summary>
public sealed class TerrainCollide
{
    private sealed class Vertex
    {
        public int CheckCount;
        public Vec3 Pos;
    }

    private sealed class Edge
    {
        public int CheckCount;
        public Vec3 Start;
        public Vec3 Perp;     // perpendicular unitaria a la arista
        public Vec3 Binormal; // dir × perp
        public Vec3 Dir;      // unitaria
        public float Length;
    }

    private sealed class Triangle
    {
        public Vec3 Normal;
        public float Dist;
        public Vec3 U; public float UDist;  // coordenada baricéntrica s = U·p - UDist
        public Vec3 V; public float VDist;  // coordenada baricéntrica t = V·p - VDist
        public readonly Vertex?[] Verts = new Vertex?[3];
        public readonly Edge?[] Edges = new Edge?[3];
    }

    private readonly Triangle[] _tris;

    /// <summary>true si todas las normales miran hacia abajo (el barrido usa entonces la esfera superior).</summary>
    public bool FacesDown { get; }
    public Vec3 Mins { get; }
    public Vec3 Maxs { get; }
    public int Contents { get; }
    public int SurfaceFlags { get; }

    /// <param name="verts">vértices de la pieza</param>
    /// <param name="indices">índices relativos a <paramref name="verts"/>, de 3 en 3</param>
    public TerrainCollide(IReadOnlyList<Vec3> verts, IReadOnlyList<int> indices, int contents, int surfaceFlags)
    {
        if (indices.Count % 3 != 0) throw new ArgumentException("numIndexes % 3 != 0");
        Contents = contents;
        SurfaceFlags = surfaceFlags;

        // --- Aristas: todas las de los triángulos, sin repetir; una compartida solo se descarta si es degenerada.
        var ea = new List<int>();
        var eb = new List<int>();
        var eo = new List<int>();
        for (var b = 0; b < indices.Count; b += 3)
        {
            for (var j = 0; j < 3; j++)
            {
                var a = indices[b + j];
                var c = indices[b + (j + 1) % 3];
                var opp = indices[b + (j + 2) % 3];
                if (a == c) throw new InvalidDataException("CM_GenerateTerrainCollide: degenerate triangle");

                var found = -1;
                for (var i = 0; i < ea.Count; i++)
                {
                    if ((ea[i] == c && eb[i] == a) || (ea[i] == a && eb[i] == c)) { found = i; break; }
                }
                if (found < 0)
                {
                    ea.Add(a); eb.Add(c); eo.Add(opp);
                    continue;
                }

                var p1 = verts[opp];
                var q = verts[eo[found]];
                var va = verts[a];
                var vb = verts[c];
                var A = Sub(va, p1);
                var B = Sub(vb, p1);
                var C = Sub(q, p1);
                var n = new Vec3(
                    (float)((double)B.Y * A.Z - (double)B.Z * A.Y),
                    (float)((double)B.Z * A.X - (double)B.X * A.Z),
                    (float)((double)B.X * A.Y - (double)B.Y * A.X));
                var dot = (float)((double)n.Z * C.Z + (double)n.Y * C.Y + (double)n.X * C.X);
                if (dot > 0)
                {
                    var la = LenSqZyx(Sub(q, va));
                    var lb = LenSqZyx(Sub(q, vb));
                    var m = la <= lb ? lb : la;
                    if (LenSqZyx(n) < (float)(m * (double)6.4e-9f))
                    {
                        var last = ea.Count - 1;
                        ea[found] = ea[last]; eb[found] = eb[last]; eo[found] = eo[last];
                        ea.RemoveAt(last); eb.RemoveAt(last); eo.RemoveAt(last);
                    }
                }
            }
        }

        // --- Vértices usados por alguna arista, numerados en el orden en que aparecen.
        var vmap = new int[verts.Count];
        Array.Fill(vmap, -1);
        var vertRecs = new List<Vertex>();
        for (var i = 0; i < ea.Count; i++)
        {
            if (vmap[ea[i]] < 0) { vmap[ea[i]] = vertRecs.Count; vertRecs.Add(new Vertex()); }
            if (vmap[eb[i]] < 0) { vmap[eb[i]] = vertRecs.Count; vertRecs.Add(new Vertex()); }
        }

        var edgeRecs = new Edge[ea.Count];

        // --- Aristas: inicio, dirección unitaria, longitud y base perpendicular (antes que los triángulos,
        // que guardan referencias a ellas).
        for (var i = 0; i < ea.Count; i++)
        {
            var start = verts[ea[i]];
            var dir = Sub(verts[eb[i]], start);
            var len = Normalize(ref dir);
            var perp = PerpendicularVector(dir);
            edgeRecs[i] = new Edge
            {
                Start = start,
                Dir = dir,
                Length = len,
                Perp = perp,
                Binormal = new Vec3(
                    (float)((double)dir.Y * perp.Z - (double)dir.Z * perp.Y),
                    (float)((double)dir.Z * perp.X - (double)dir.X * perp.Z),
                    (float)((double)dir.X * perp.Y - (double)dir.Y * perp.X)),
            };
        }

        // --- Triángulos.
        var anyDown = false;
        var anyUp = false;
        _tris = new Triangle[indices.Count / 3];
        for (var t = 0; t < _tris.Length; t++)
        {
            var i0 = indices[3 * t];
            var i1 = indices[3 * t + 1];
            var i2 = indices[3 * t + 2];
            var v0 = verts[i0];
            var v1 = verts[i1];
            var v2 = verts[i2];
            var tri = new Triangle();

            // PlaneFromPoints: (v2 - v0) × (v1 - v0), normalizado; dist = n·v0
            var c2 = Sub(v2, v0);
            var c1 = Sub(v1, v0);
            var normal = new Vec3(
                (float)((double)c2.Y * c1.Z - (double)c2.Z * c1.Y),
                (float)((double)c2.Z * c1.X - (double)c2.X * c1.Z),
                (float)((double)c2.X * c1.Y - (double)c2.Y * c1.X));
            Normalize(ref normal);
            tri.Normal = normal;
            tri.Dist = (float)((double)normal.Z * v0.Z + (double)v0.Y * normal.Y + (double)normal.X * v0.X);
            if (normal.Z < -0.001f) anyDown = true;
            else if (normal.Z > 0.001f) anyUp = true;

            // Base dual para las coordenadas baricéntricas.
            var e1 = Sub(v1, v0);
            var l1 = Normalize(ref e1);
            var e2 = Sub(v2, v0);
            var l2 = Normalize(ref e2);
            var d = (float)((double)e2.Z * e1.Z + (double)e2.Y * e1.Y + (double)e2.X * e1.X);
            var k = (float)(1.0 / (1.0 - (double)d * d));
            e1 = Scale(e1, k);
            e2 = Scale(e2, k);
            var nd = -d;
            var inv1 = (float)(1.0 / l1);
            tri.U = new Vec3(
                (float)(((double)nd * e2.X + e1.X) * inv1),
                (float)(((double)nd * e2.Y + e1.Y) * inv1),
                (float)(((double)nd * e2.Z + e1.Z) * inv1));
            tri.UDist = (float)((double)tri.U.X * v0.X + (double)tri.U.Z * v0.Z + (double)tri.U.Y * v0.Y);
            var inv2 = (float)(1.0 / l2);
            tri.V = new Vec3(
                (float)(((double)nd * e1.X + e2.X) * inv2),
                (float)(((double)nd * e1.Y + e2.Y) * inv2),
                (float)(((double)nd * e1.Z + e2.Z) * inv2));
            tri.VDist = (float)((double)tri.V.X * v0.X + (double)tri.V.Z * v0.Z + (double)tri.V.Y * v0.Y);

            tri.Verts[0] = vmap[i0] >= 0 ? vertRecs[vmap[i0]] : null;
            tri.Verts[1] = vmap[i1] >= 0 ? vertRecs[vmap[i1]] : null;
            tri.Verts[2] = vmap[i2] >= 0 ? vertRecs[vmap[i2]] : null;
            tri.Edges[0] = FindEdge(i1, i2);
            tri.Edges[1] = FindEdge(i0, i2);
            tri.Edges[2] = FindEdge(i1, i0);
            _tris[t] = tri;
        }
        FacesDown = _tris.Length > 0 && anyDown && !anyUp;

        // --- Límites y posiciones de los vértices.
        var mins = new Vec3(262144f, 262144f, 262144f);
        var maxs = new Vec3(-262144f, -262144f, -262144f);
        for (var i = 0; i < verts.Count; i++)
        {
            var v = verts[i];
            mins = new Vec3(MathF.Min(mins.X, v.X), MathF.Min(mins.Y, v.Y), MathF.Min(mins.Z, v.Z));
            maxs = new Vec3(MathF.Max(maxs.X, v.X), MathF.Max(maxs.Y, v.Y), MathF.Max(maxs.Z, v.Z));
            if (vmap[i] >= 0) vertRecs[vmap[i]].Pos = v;
        }
        Mins = mins;
        Maxs = maxs;

        Edge? FindEdge(int a, int b)
        {
            for (var i = 0; i < ea.Count; i++)
                if ((ea[i] == a && eb[i] == b) || (ea[i] == b && eb[i] == a)) return edgeRecs[i];
            return null;
        }
    }

    /// <summary>Estado del barrido compartido con el trazado de brushes (traceWork_t).</summary>
    public struct SweepState
    {
        public Vec3 Start, End, Delta;   // centro de la caja (ya con el offset simétrico) y delta
        public float DeltaLenSq;
        public float Radius, HalfHeight; // cápsula: size[1].x y size[1].z
        public float Fraction;
        public Vec3 Normal;
        public bool StartSolid;
        public int CheckCount;
    }

    /// <summary>Barrido de la esfera de la cápsula contra la malla (@0x4196f0). Modifica Fraction/Normal/StartSolid.</summary>
    public void Trace(ref SweepState tw)
    {
        var h = (float)((double)tw.HalfHeight - tw.Radius);
        float cap;
        var s = tw.Start;
        var e = tw.End;
        if (!FacesDown)
        {
            cap = (float)((double)h + h);
            s = s with { Z = (float)(s.Z - (double)h) };
            e = e with { Z = (float)(e.Z - (double)h) };
        }
        else
        {
            cap = (float)(h * -2.0);
            s = s with { Z = (float)((double)h + s.Z) };
            e = e with { Z = (float)((double)h + e.Z) };
        }
        var r = (float)(tw.Radius + 0.125);
        var nr = -r;
        var D = tw.Delta;

        foreach (var tri in _tris)
        {
            var n = tri.Normal;
            var dEnd = (float)(((double)n.X * e.X + (double)n.Y * e.Y + (double)n.Z * e.Z) - tri.Dist);
            if (!(dEnd < r)) continue;
            var dStart = (float)(((double)n.X * s.X + (double)n.Y * s.Y + (double)n.Z * s.Z) - tri.Dist);
            var denom = (float)((double)dStart - dEnd);
            if (!(denom > 0)) continue;

            if (dStart >= nr)
            {
                float t;
                Vec3 p;
                if (r <= dStart)
                {
                    t = (float)(((double)dStart - r) / denom);
                    if (tw.Fraction < t) continue;
                    p = new Vec3((float)((double)t * D.X + s.X), (float)((double)t * D.Y + s.Y), (float)((double)t * D.Z + s.Z));
                }
                else
                {
                    p = s;
                    t = 0;
                }

                var sc = (float)(((double)p.Z * tri.U.Z + (double)p.X * tri.U.X + (double)p.Y * tri.U.Y) - tri.UDist);
                var tc = (float)(((double)p.X * tri.V.X + (double)p.Y * tri.V.Y + (double)p.Z * tri.V.Z) - tri.VDist);
                var code = ((float)((double)sc + tc) > 1f ? 1 : 0) | (sc < 0 ? 2 : 0) | (tc < 0 ? 4 : 0);
                if (code == 0)
                {
                    tw.Normal = n;
                    if (t <= 1e-5f) { SetStartSolid(ref tw); return; }
                    tw.Fraction = (float)(t - (double)1e-5f);
                    continue;
                }

                for (var i = 0; i < 3; i++)
                {
                    if ((code & (1 << i)) == 0)
                    {
                        var vtx = tri.Verts[i];
                        if (vtx is null || vtx.CheckCount == tw.CheckCount) continue;
                        vtx.CheckCount = tw.CheckCount;
                        var w = Sub(s, vtx.Pos);
                        var c = (float)(((double)w.X * w.X + (double)w.Y * w.Y + (double)w.Z * w.Z) - (double)r * r);
                        if (c <= 0)
                        {
                            tw.Normal = n;
                            SetStartSolid(ref tw);
                            return;
                        }
                        var b = (float)((double)w.Z * D.Z + (double)w.X * D.X + (double)w.Y * D.Y);
                        if (!(b < 0)) continue;
                        var disc = (float)((double)b * b - (double)tw.DeltaLenSq * c);
                        if (!(disc >= 0)) continue;
                        var tt = (float)((-System.Math.Sqrt(disc) - b) / tw.DeltaLenSq);
                        if (!(tt < tw.Fraction)) continue;
                        var inv = (float)(1.0 / r);
                        tw.Normal = new Vec3(
                            (float)(((double)tt * D.X + w.X) * inv),
                            (float)(((double)tt * D.Y + w.Y) * inv),
                            (float)(((double)tt * D.Z + w.Z) * inv));
                        if (tw.Fraction <= 1e-5f) { SetStartSolid(ref tw); return; }
                        tw.Fraction = (float)(tt - (double)1e-5f);
                    }
                    else
                    {
                        var edge = tri.Edges[i];
                        if (edge is null || edge.CheckCount == tw.CheckCount) continue;
                        edge.CheckCount = tw.CheckCount;
                        var w = Sub(s, edge.Start);
                        var a1 = (float)((double)w.Y * edge.Perp.Y + (double)w.Z * edge.Perp.Z + (double)w.X * edge.Perp.X);
                        var a2 = (float)((double)w.Y * edge.Binormal.Y + (double)w.Z * edge.Binormal.Z + (double)w.X * edge.Binormal.X);
                        var along = (float)((double)w.X * edge.Dir.X + (double)w.Y * edge.Dir.Y + (double)w.Z * edge.Dir.Z);
                        var c = (float)(((double)a1 * a1 + (double)a2 * a2) - (double)r * r);
                        if (c > 0)
                        {
                            var d1 = (float)((double)edge.Perp.Z * D.Z + (double)D.Y * edge.Perp.Y + (double)edge.Perp.X * D.X);
                            var d2 = (float)((double)edge.Binormal.Z * D.Z + (double)D.X * edge.Binormal.X + (double)D.Y * edge.Binormal.Y);
                            var bb = (float)((double)a1 * d1 + (double)a2 * d2);
                            if (!(bb < 0)) continue;
                            var aa = (float)((double)d1 * d1 + (double)d2 * d2);
                            var disc = (float)((double)bb * bb - (double)aa * c);
                            if (!(disc > 0)) continue;
                            var tt = (float)((-System.Math.Sqrt(disc) - bb) / aa);
                            if (!(tt < tw.Fraction)) continue;
                            var al = (float)(((double)edge.Dir.Y * D.Y + (double)D.X * edge.Dir.X + (double)edge.Dir.Z * D.Z) * tt + along);
                            if (!(al >= 0 && al <= edge.Length)) continue;
                            var inv = (float)(1.0 / r);
                            var q1 = (float)(((double)d1 * tt + a1) * inv);
                            var q2 = (float)(((double)d2 * tt + a2) * inv);
                            tw.Normal = new Vec3(
                                (float)((double)q2 * edge.Binormal.X + (float)((double)q1 * edge.Perp.X)),
                                (float)((double)q2 * edge.Binormal.Y + (float)((double)q1 * edge.Perp.Y)),
                                (float)((double)q2 * edge.Binormal.Z + (float)((double)q1 * edge.Perp.Z)));
                            if (tw.Fraction <= 1e-5f) { SetStartSolid(ref tw); return; }
                            tw.Fraction = (float)(tt - (double)1e-5f);
                        }
                        else if (along >= 0 && along <= edge.Length)
                        {
                            tw.Normal = n;
                            SetStartSolid(ref tw);
                            return;
                        }
                    }
                }
            }
            else
            {
                // La esfera barrida empieza detrás del plano: ¿atraviesa la cápsula el triángulo?
                var other = (float)((double)cap * n.Z + dStart);
                if (!(other > nr)) continue;
                var invNz = (float)(1.0 / n.Z);
                var k = (float)(((double)nr - dStart) * invNz);
                var sS = (float)(((double)tri.U.Z * s.Z + (double)tri.U.X * s.X + (double)tri.U.Y * s.Y) - tri.UDist);
                var tS = (float)(((double)tri.V.Z * s.Z + (double)tri.V.X * s.X + (double)tri.V.Y * s.Y) - tri.VDist);
                var s1 = (float)((double)k * tri.U.Z + sS);
                if (s1 >= 0)
                {
                    var t1 = (float)((double)k * tri.V.Z + tS);
                    if (t1 >= 0 && (float)((double)s1 + t1) < 1f)
                    {
                        tw.Normal = n;
                        SetStartSolid(ref tw);
                        return;
                    }
                }
                var k2 = cap;
                if (r <= other) k2 = (float)(((double)r - dStart) * invNz);
                var s2 = (float)((double)k2 * tri.U.Z + sS);
                if (s2 >= 0)
                {
                    var t2 = (float)((double)k2 * tri.V.Z + tS);
                    if (t2 >= 0 && (float)((double)s2 + t2) <= 1f)
                    {
                        tw.Normal = n;
                        SetStartSolid(ref tw);
                        return;
                    }
                }
            }
        }
    }

    private static void SetStartSolid(ref SweepState tw)
    {
        tw.Fraction = 0;
        tw.StartSolid = true;
    }

    private static Vec3 Sub(Vec3 a, Vec3 b) => new((float)((double)a.X - b.X), (float)((double)a.Y - b.Y), (float)((double)a.Z - b.Z));
    private static Vec3 Scale(Vec3 a, float k) => new((float)((double)k * a.X), (float)((double)k * a.Y), (float)((double)k * a.Z));
    private static float LenSqZyx(Vec3 v) => (float)((double)v.Z * v.Z + (double)v.Y * v.Y + (double)v.X * v.X);

    /// <summary>VectorNormalize (@0x426710): longitud con z²+y²+x², multiplica por 1/longitud. Devuelve la longitud.</summary>
    private static float Normalize(ref Vec3 v)
    {
        var len = (float)System.Math.Sqrt(LenSqZyx(v));
        if (len != 0)
        {
            var inv = (float)(1.0 / len);
            v = Scale(v, inv);
        }
        return len;
    }

    /// <summary>PerpendicularVector (@0x4273c0), igual que ioquake3 q_math.c (ProjectPointOnPlane + normalizar).</summary>
    private static Vec3 PerpendicularVector(Vec3 src)
    {
        var pos = 0;
        var minelem = 1f;
        for (var i = 0; i < 3; i++)
        {
            var a = MathF.Abs(src[i]);
            if (a < minelem) { pos = i; minelem = a; }
        }
        var temp = Vec3.Zero.With(pos, 1f);
        // ProjectPointOnPlane(dst, temp, src)
        var invDenom = (float)(1.0 / ((double)src.X * src.X + (double)src.Y * src.Y + (double)src.Z * src.Z));
        var d = (float)(((double)src.X * temp.X + (double)src.Y * temp.Y + (double)src.Z * temp.Z) * invDenom);
        var n = Scale(src, invDenom);
        var dst = new Vec3((float)(temp.X - (double)d * n.X), (float)(temp.Y - (double)d * n.Y), (float)(temp.Z - (double)d * n.Z));
        Normalize(ref dst);
        return dst;
    }
}
