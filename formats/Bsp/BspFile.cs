using System.Buffers.Binary;
using System.Text;

namespace Foley.Formats.Bsp;

/// <summary>Material del lump 0 (72 bytes).</summary>
public readonly record struct BspMaterial(string Name, uint SurfaceFlags, uint ContentFlags);

/// <summary>Triangle soup del lump 6 (16 bytes). Los índices son relativos a <see cref="FirstVertex"/>.</summary>
public readonly record struct BspSurface(
    ushort MaterialIndex,
    ushort LightmapIndex,
    uint FirstVertex,
    ushort VertexCount,
    ushort IndexCount,
    uint FirstIndex)
{
    public const ushort NoLightmap = 0xFFFF;
}

/// <summary>Vértice del lump 7 (44 bytes). Unidades y ejes originales (Z arriba).</summary>
public readonly record struct BspVertex(
    float X, float Y, float Z,
    float U, float V,
    float LmU, float LmV,
    float Nx, float Ny, float Nz,
    byte R, byte G, byte B, byte A);

/// <summary>Plano del lump 2 (16 bytes).</summary>
public readonly record struct BspPlane(float Nx, float Ny, float Nz, float Dist);

/// <summary>Brush del lump 4 (4 bytes). Sus lados empiezan en <see cref="FirstSide"/> (calculado al leer).</summary>
public readonly record struct BspBrush(ushort SideCount, ushort MaterialIndex, int FirstSide);

/// <summary>
/// Lado de brush del lump 3 (8 bytes). En los 6 primeros lados de cada brush el primer campo
/// es una distancia axial (minX, maxX, minY, maxY, minZ, maxZ); en el resto, un índice de plano.
/// </summary>
public readonly record struct BspBrushSide(uint Raw, uint MaterialIndex)
{
    public const int AxialCount = 6;
    public float AxialDistance => BitConverter.UInt32BitsToSingle(Raw);
    public uint PlaneIndex => Raw;
}

/// <summary>Modelo del lump 27 (48 bytes). El 0 es el mundo; los demás, los modelos inline <c>*N</c>.</summary>
public readonly record struct BspModel(
    float MinX, float MinY, float MinZ,
    float MaxX, float MaxY, float MaxZ,
    uint FirstSurface, uint SurfaceCount,
    uint FirstBrush, uint BrushCount);

/// <summary>
/// Pieza de colisión no-brush del lump 24 (16 bytes): terreno (malla de triángulos) o curva
/// (rejilla de puntos de control). Los índices de vértices y de índices son del lump 25 y 26.
/// Ver docs/formats/bsp.md y docs/re/colision.md.
/// </summary>
public readonly record struct BspCollisionPatch(
    ushort MaterialIndex,
    byte Type,          // 0 = curva, 1 = terreno
    ushort Dim0,        // terreno: nº de vértices; curva: ancho de la rejilla
    ushort Dim1,        // terreno: nº de índices; curva: alto de la rejilla
    uint Field8,        // terreno: primer vértice; curva: sin identificar
    uint Field12)       // terreno: primer índice; curva: primer vértice
{
    public const byte TypeCurve = 0;
    public const byte TypeTerrain = 1;
}

/// <summary>Entidad del lump 29: pares clave/valor.</summary>
public sealed class BspEntity
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyDictionary<string, string> Values => _values;
    public string ClassName => Get("classname") ?? "";

    public string? Get(string key) => _values.GetValueOrDefault(key);

    internal void Set(string key, string value) => _values[key] = value;
}

/// <summary>
/// Lector de IBSP v59 (ver docs/formats/bsp.md). Solo lo necesario para dibujar el mapa:
/// materiales, superficies, vértices, índices y entidades. La colisión es del hito 3.
/// </summary>
public sealed class BspFile
{
    public const int Version = 59;
    public const int LumpCount = 33;

    public const int LumpMaterials = 0;
    public const int LumpPlanes = 2;
    public const int LumpBrushSides = 3;
    public const int LumpBrushes = 4;
    public const int LumpSurfaces = 6;
    public const int LumpVertices = 7;
    public const int LumpIndices = 8;
    public const int LumpCollisionPatches = 24;
    public const int LumpCollisionVerts = 25;
    public const int LumpCollisionIndices = 26;
    public const int LumpModels = 27;
    public const int LumpEntities = 29;

    private const int MaterialSize = 72;
    private const int SurfaceSize = 16;
    private const int VertexSize = 44;
    private const int PlaneSize = 16;
    private const int BrushSideSize = 8;
    private const int BrushSize = 4;
    private const int ModelSize = 48;

    public BspMaterial[] Materials { get; }
    public BspSurface[] Surfaces { get; }
    public BspVertex[] Vertices { get; }
    public ushort[] Indices { get; }
    public IReadOnlyList<BspEntity> Entities { get; }
    public BspPlane[] Planes { get; }
    public BspBrush[] Brushes { get; }
    public BspBrushSide[] BrushSides { get; }
    public BspModel[] Models { get; }
    public BspCollisionPatch[] CollisionPatches { get; }
    public float[] CollisionVerts { get; }          // x, y, z consecutivos (lump 25)
    public ushort[] CollisionIndices { get; }       // lump 26

    private BspFile(BspMaterial[] materials, BspSurface[] surfaces, BspVertex[] vertices,
        ushort[] indices, IReadOnlyList<BspEntity> entities,
        BspPlane[] planes, BspBrush[] brushes, BspBrushSide[] brushSides, BspModel[] models,
        BspCollisionPatch[] patches, float[] collisionVerts, ushort[] collisionIndices)
    {
        CollisionPatches = patches;
        CollisionVerts = collisionVerts;
        CollisionIndices = collisionIndices;
        Materials = materials;
        Surfaces = surfaces;
        Vertices = vertices;
        Indices = indices;
        Entities = entities;
        Planes = planes;
        Brushes = brushes;
        BrushSides = brushSides;
        Models = models;
    }

    public static BspFile Read(byte[] data) => Read(data.AsSpan());

    public static BspFile Read(ReadOnlySpan<byte> data)
    {
        if (data.Length < 8 + LumpCount * 8)
            throw new InvalidDataException("BSP demasiado corto para la cabecera.");
        if (Encoding.ASCII.GetString(data[..4]) != "IBSP")
            throw new InvalidDataException("Firma BSP inválida (se esperaba IBSP).");
        var version = BinaryPrimitives.ReadInt32LittleEndian(data[4..]);
        if (version != Version)
            throw new InvalidDataException($"Versión de BSP no soportada: {version} (se esperaba {Version}).");

        var materials = ReadMaterials(Lump(data, LumpMaterials, MaterialSize));
        var vertices = ReadVertices(Lump(data, LumpVertices, VertexSize));
        var indices = ReadIndices(Lump(data, LumpIndices, 2));
        var surfaces = ReadSurfaces(Lump(data, LumpSurfaces, SurfaceSize),
            materials.Length, vertices.Length, indices.Length);
        var entities = ParseEntities(Lump(data, LumpEntities, 1));
        var planes = ReadPlanes(Lump(data, LumpPlanes, PlaneSize));
        var sides = ReadBrushSides(Lump(data, LumpBrushSides, BrushSideSize));
        var brushes = ReadBrushes(Lump(data, LumpBrushes, BrushSize), materials.Length, sides, planes.Length);
        var models = ReadModels(Lump(data, LumpModels, ModelSize), surfaces.Length, brushes.Length);
        var cverts = ReadFloats(Lump(data, LumpCollisionVerts, 12));
        var cindices = ReadIndices(Lump(data, LumpCollisionIndices, 2));
        var patches = ReadCollisionPatches(Lump(data, LumpCollisionPatches, 16), materials.Length, cverts.Length / 3, cindices.Length);
        return new BspFile(materials, surfaces, vertices, indices, entities, planes, brushes, sides, models,
            patches, cverts, cindices);
    }

    private static ReadOnlySpan<byte> Lump(ReadOnlySpan<byte> data, int index, int recordSize)
    {
        var entry = data[(8 + index * 8)..];
        var length = BinaryPrimitives.ReadUInt32LittleEndian(entry);
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(entry[4..]);
        if ((long)offset + length > data.Length)
            throw new InvalidDataException($"Lump {index} fuera del archivo (offset {offset}, longitud {length}).");
        if (recordSize > 1 && length % recordSize != 0)
            throw new InvalidDataException($"Lump {index}: longitud {length} no es múltiplo de {recordSize}.");
        return data.Slice((int)offset, (int)length);
    }

    private static BspMaterial[] ReadMaterials(ReadOnlySpan<byte> lump)
    {
        var result = new BspMaterial[lump.Length / MaterialSize];
        for (var i = 0; i < result.Length; i++)
        {
            var rec = lump.Slice(i * MaterialSize, MaterialSize);
            var nameBytes = rec[..64];
            var end = nameBytes.IndexOf((byte)0);
            var name = Encoding.Latin1.GetString(end < 0 ? nameBytes : nameBytes[..end]);
            result[i] = new BspMaterial(name,
                BinaryPrimitives.ReadUInt32LittleEndian(rec[64..]),
                BinaryPrimitives.ReadUInt32LittleEndian(rec[68..]));
        }
        return result;
    }

    private static BspVertex[] ReadVertices(ReadOnlySpan<byte> lump)
    {
        var result = new BspVertex[lump.Length / VertexSize];
        for (var i = 0; i < result.Length; i++)
        {
            var r = lump.Slice(i * VertexSize, VertexSize);
            result[i] = new BspVertex(
                F(r, 0), F(r, 4), F(r, 8), F(r, 12), F(r, 16), F(r, 20), F(r, 24),
                F(r, 28), F(r, 32), F(r, 36), r[40], r[41], r[42], r[43]);
        }
        return result;
    }

    private static float F(ReadOnlySpan<byte> r, int offset) =>
        BinaryPrimitives.ReadSingleLittleEndian(r[offset..]);

    private static uint U(ReadOnlySpan<byte> r, int offset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(r[offset..]);

    private static BspPlane[] ReadPlanes(ReadOnlySpan<byte> lump)
    {
        var result = new BspPlane[lump.Length / PlaneSize];
        for (var i = 0; i < result.Length; i++)
        {
            var r = lump.Slice(i * PlaneSize, PlaneSize);
            result[i] = new BspPlane(F(r, 0), F(r, 4), F(r, 8), F(r, 12));
        }
        return result;
    }

    private static BspBrushSide[] ReadBrushSides(ReadOnlySpan<byte> lump)
    {
        var result = new BspBrushSide[lump.Length / BrushSideSize];
        for (var i = 0; i < result.Length; i++)
        {
            var r = lump.Slice(i * BrushSideSize, BrushSideSize);
            result[i] = new BspBrushSide(
                BinaryPrimitives.ReadUInt32LittleEndian(r),
                BinaryPrimitives.ReadUInt32LittleEndian(r[4..]));
        }
        return result;
    }

    private static BspBrush[] ReadBrushes(ReadOnlySpan<byte> lump, int materialCount, BspBrushSide[] sides, int planeCount)
    {
        var result = new BspBrush[lump.Length / BrushSize];
        var next = 0;
        for (var i = 0; i < result.Length; i++)
        {
            var count = BinaryPrimitives.ReadUInt16LittleEndian(lump[(i * BrushSize)..]);
            var material = BinaryPrimitives.ReadUInt16LittleEndian(lump[(i * BrushSize + 2)..]);
            if (count < BspBrushSide.AxialCount)
                throw new InvalidDataException($"Brush {i}: {count} lados (mínimo {BspBrushSide.AxialCount}).");
            if (material >= materialCount)
                throw new InvalidDataException($"Brush {i}: material {material} fuera de rango ({materialCount}).");
            if (next + count > sides.Length)
                throw new InvalidDataException($"Brush {i}: lados fuera de rango.");
            for (var k = BspBrushSide.AxialCount; k < count; k++)
                if (sides[next + k].PlaneIndex >= planeCount)
                    throw new InvalidDataException($"Brush {i}, lado {k}: plano {sides[next + k].PlaneIndex} fuera de rango.");
            result[i] = new BspBrush(count, material, next);
            next += count;
        }
        if (next != sides.Length)
            throw new InvalidDataException($"Los brushes usan {next} lados pero el lump tiene {sides.Length}.");
        return result;
    }

    private static BspModel[] ReadModels(ReadOnlySpan<byte> lump, int surfaceCount, int brushCount)
    {
        var result = new BspModel[lump.Length / ModelSize];
        for (var i = 0; i < result.Length; i++)
        {
            var r = lump.Slice(i * ModelSize, ModelSize);
            var m = new BspModel(F(r, 0), F(r, 4), F(r, 8), F(r, 12), F(r, 16), F(r, 20),
                U(r, 24), U(r, 28), U(r, 40), U(r, 44));
            if ((long)m.FirstBrush + m.BrushCount > brushCount)
                throw new InvalidDataException($"Modelo {i}: brushes fuera de rango.");
            if ((long)m.FirstSurface + m.SurfaceCount > surfaceCount)
                throw new InvalidDataException($"Modelo {i}: superficies fuera de rango.");
            result[i] = m;
        }
        return result;
    }

    private static float[] ReadFloats(ReadOnlySpan<byte> lump)
    {
        var result = new float[lump.Length / 4];
        for (var i = 0; i < result.Length; i++) result[i] = F(lump, i * 4);
        return result;
    }

    private static BspCollisionPatch[] ReadCollisionPatches(ReadOnlySpan<byte> lump, int materialCount, int vertCount, int indexCount)
    {
        var result = new BspCollisionPatch[lump.Length / 16];
        for (var i = 0; i < result.Length; i++)
        {
            var r = lump.Slice(i * 16, 16);
            var p = new BspCollisionPatch(
                BinaryPrimitives.ReadUInt16LittleEndian(r), r[2],
                BinaryPrimitives.ReadUInt16LittleEndian(r[4..]), BinaryPrimitives.ReadUInt16LittleEndian(r[6..]),
                U(r, 8), U(r, 12));
            if (p.MaterialIndex >= materialCount)
                throw new InvalidDataException($"Pieza de colisión {i}: material {p.MaterialIndex} fuera de rango.");
            if (p.Type == BspCollisionPatch.TypeTerrain)
            {
                if ((long)p.Field8 + p.Dim0 > vertCount || (long)p.Field12 + p.Dim1 > indexCount || p.Dim1 % 3 != 0)
                    throw new InvalidDataException($"Pieza de terreno {i}: rangos inválidos.");
            }
            else if ((long)p.Field12 + p.Dim0 * p.Dim1 > vertCount)
            {
                throw new InvalidDataException($"Curva {i}: vértices fuera de rango.");
            }
            result[i] = p;
        }
        return result;
    }

    private static ushort[] ReadIndices(ReadOnlySpan<byte> lump)
    {
        var result = new ushort[lump.Length / 2];
        for (var i = 0; i < result.Length; i++)
            result[i] = BinaryPrimitives.ReadUInt16LittleEndian(lump[(i * 2)..]);
        return result;
    }

    private static BspSurface[] ReadSurfaces(ReadOnlySpan<byte> lump, int materialCount, int vertexCount, int indexCount)
    {
        var result = new BspSurface[lump.Length / SurfaceSize];
        for (var i = 0; i < result.Length; i++)
        {
            var r = lump.Slice(i * SurfaceSize, SurfaceSize);
            var s = new BspSurface(
                BinaryPrimitives.ReadUInt16LittleEndian(r),
                BinaryPrimitives.ReadUInt16LittleEndian(r[2..]),
                BinaryPrimitives.ReadUInt32LittleEndian(r[4..]),
                BinaryPrimitives.ReadUInt16LittleEndian(r[8..]),
                BinaryPrimitives.ReadUInt16LittleEndian(r[10..]),
                BinaryPrimitives.ReadUInt32LittleEndian(r[12..]));
            if (s.MaterialIndex >= materialCount)
                throw new InvalidDataException($"Superficie {i}: material {s.MaterialIndex} fuera de rango ({materialCount}).");
            if ((long)s.FirstVertex + s.VertexCount > vertexCount)
                throw new InvalidDataException($"Superficie {i}: vértices fuera de rango.");
            if ((long)s.FirstIndex + s.IndexCount > indexCount)
                throw new InvalidDataException($"Superficie {i}: índices fuera de rango.");
            if (s.IndexCount % 3 != 0)
                throw new InvalidDataException($"Superficie {i}: cantidad de índices {s.IndexCount} no es múltiplo de 3.");
            result[i] = s;
        }
        return result;
    }

    /// <summary>Parser de entidades estilo Quake 3: <c>{ "clave" "valor" ... }</c>.</summary>
    internal static List<BspEntity> ParseEntities(ReadOnlySpan<byte> lump)
    {
        var end = lump.IndexOf((byte)0);
        var text = Encoding.Latin1.GetString(end < 0 ? lump : lump[..end]);
        var entities = new List<BspEntity>();
        BspEntity? current = null;
        var pos = 0;
        while (pos < text.Length)
        {
            var c = text[pos];
            if (c == '{')
            {
                if (current is not null) throw new InvalidDataException("Entidades: '{' dentro de una entidad.");
                current = new BspEntity();
                pos++;
            }
            else if (c == '}')
            {
                if (current is null) throw new InvalidDataException("Entidades: '}' sin entidad abierta.");
                entities.Add(current);
                current = null;
                pos++;
            }
            else if (c == '"')
            {
                if (current is null) throw new InvalidDataException("Entidades: texto fuera de una entidad.");
                var key = ReadQuoted(text, ref pos);
                while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
                if (pos >= text.Length || text[pos] != '"')
                    throw new InvalidDataException($"Entidades: falta valor para '{key}'.");
                current.Set(key, ReadQuoted(text, ref pos));
            }
            else if (char.IsWhiteSpace(c)) pos++;
            else throw new InvalidDataException($"Entidades: carácter inesperado '{c}'.");
        }
        if (current is not null) throw new InvalidDataException("Entidades: entidad sin cerrar.");
        return entities;
    }

    private static string ReadQuoted(string text, ref int pos)
    {
        var close = text.IndexOf('"', pos + 1);
        if (close < 0) throw new InvalidDataException("Entidades: comilla sin cerrar.");
        var s = text.Substring(pos + 1, close - pos - 1);
        pos = close + 1;
        return s;
    }
}
