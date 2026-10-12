namespace Foley.Formats.XModel;

/// <summary>Influencia de hueso sobre un vértice: posición en el espacio del hueso y peso.</summary>
public readonly record struct XModelWeight(int Bone, float X, float Y, float Z, float Weight);

/// <summary>
/// Superficie de <c>xmodelsurfs</c>. Los vértices quedan en el espacio de sus huesos; usar
/// <see cref="XModelMesh"/> para obtenerlos en la pose de reposo.
/// </summary>
public sealed class XModelSurface
{
    public int Bone { get; init; }                          // -1 = vértices con pesos
    public float[] Normals { get; init; } = Array.Empty<float>(); // 3 por vértice, espacio del hueso
    public float[] UVs { get; init; } = Array.Empty<float>();     // 2 por vértice
    /// <summary>Influencias de cada vértice: la primera es la principal (hueso del vértice).</summary>
    public XModelWeight[][] Weights { get; init; } = Array.Empty<XModelWeight[]>();
    /// <summary>Lista de triángulos (3 índices cada uno), ya convertida desde tiras.</summary>
    public int[] Triangles { get; init; } = Array.Empty<int>();
    public int VertexCount => Weights.Length;
}

/// <summary>Archivo <c>xmodelsurfs/&lt;lod&gt;</c> (versión 14). Ver docs/formats/xmodel.md.</summary>
public sealed class XModelSurfs
{
    public XModelSurface[] Surfaces { get; }

    private XModelSurfs(XModelSurface[] surfaces) => Surfaces = surfaces;

    public static XModelSurfs Read(ReadOnlySpan<byte> data)
    {
        var r = new XReader(data);
        var version = r.U16();
        if (version != XModelFile.Version)
            throw new InvalidDataException($"xmodelsurfs: versión {version}, se esperaba {XModelFile.Version}.");
        var surfaces = new XModelSurface[r.U16()];
        for (var s = 0; s < surfaces.Length; s++) surfaces[s] = ReadSurface(ref r);
        r.ExpectEnd("xmodelsurfs");
        return new XModelSurfs(surfaces);
    }

    // Lectura de superficie del cargador original (@0x47f190).
    private static XModelSurface ReadSurface(ref XReader r)
    {
        r.U8(); // modo, sin interpretar
        var vertexCount = r.U16();
        var triCount = r.U16();
        var stripCount = r.U16();
        var bone = r.I16();
        var extraCount = 0;
        if (bone == -1)
        {
            extraCount = r.U16();
            r.U16(); // sin interpretar
        }

        var tris = new List<int>(triCount * 3);
        for (var i = 0; i < stripCount; i++)
        {
            var n = r.U8();
            var strip = new int[n];
            for (var k = 0; k < n; k++) strip[k] = r.U16();
            StripToTriangles(strip, tris);
        }
        if (tris.Count != triCount * 3)
            throw new InvalidDataException($"xmodelsurfs: {tris.Count / 3} triángulos, la cabecera dice {triCount}.");
        foreach (var t in tris)
            if (t >= vertexCount) throw new InvalidDataException($"xmodelsurfs: índice {t} fuera de {vertexCount} vértices.");

        var normals = new float[vertexCount * 3];
        var uvs = new float[vertexCount * 2];
        var weights = new XModelWeight[vertexCount][];
        var extraPerVertex = new int[vertexCount];
        for (var v = 0; v < vertexCount; v++)
        {
            normals[v * 3] = r.F32(); normals[v * 3 + 1] = r.F32(); normals[v * 3 + 2] = r.F32();
            uvs[v * 2] = r.F32(); uvs[v * 2 + 1] = r.F32();
            if (bone == -1)
            {
                var extra = r.I16();
                var b = r.U16();
                float x = r.F32(), y = r.F32(), z = r.F32();
                var w = extra != 0 ? r.F32() : 1f;
                extraPerVertex[v] = extra;
                weights[v] = new XModelWeight[1 + extra];
                weights[v][0] = new XModelWeight(b, x, y, z, w);
            }
            else
            {
                weights[v] = new[] { new XModelWeight(bone, r.F32(), r.F32(), r.F32(), 1f) };
            }
        }

        // Influencias extra, guardadas tras todos los vértices y en su orden.
        var read = 0;
        for (var v = 0; v < vertexCount; v++)
            for (var k = 1; k <= extraPerVertex[v]; k++, read++)
                weights[v][k] = new XModelWeight(r.U16(), r.F32(), r.F32(), r.F32(), r.F32());
        if (read != extraCount)
            throw new InvalidDataException($"xmodelsurfs: {read} influencias extra, la cabecera dice {extraCount}.");

        return new XModelSurface
        {
            Bone = bone,
            Normals = normals,
            UVs = uvs,
            Weights = weights,
            Triangles = tris.ToArray(),
        };
    }

    /// <summary>Tira → triángulos como en el original (@0x47f090), sin los degenerados.</summary>
    public static void StripToTriangles(IReadOnlyList<int> s, List<int> output)
    {
        if (s.Count < 3) return;
        Add(output, s[0], s[1], s[2]);
        for (var k = 3; k < s.Count; k += 2)
        {
            Add(output, s[k - 1], s[k - 2], s[k]);
            if (k + 1 < s.Count) Add(output, s[k - 1], s[k], s[k + 1]);
        }
    }

    private static void Add(List<int> o, int a, int b, int c)
    {
        if (a == b || a == c || b == c) return;
        o.Add(a); o.Add(b); o.Add(c);
    }
}
