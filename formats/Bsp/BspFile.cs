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
    public const int LumpSurfaces = 6;
    public const int LumpVertices = 7;
    public const int LumpIndices = 8;
    public const int LumpEntities = 29;

    private const int MaterialSize = 72;
    private const int SurfaceSize = 16;
    private const int VertexSize = 44;

    public BspMaterial[] Materials { get; }
    public BspSurface[] Surfaces { get; }
    public BspVertex[] Vertices { get; }
    public ushort[] Indices { get; }
    public IReadOnlyList<BspEntity> Entities { get; }

    private BspFile(BspMaterial[] materials, BspSurface[] surfaces, BspVertex[] vertices,
        ushort[] indices, IReadOnlyList<BspEntity> entities)
    {
        Materials = materials;
        Surfaces = surfaces;
        Vertices = vertices;
        Indices = indices;
        Entities = entities;
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
        return new BspFile(materials, surfaces, vertices, indices, entities);
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
