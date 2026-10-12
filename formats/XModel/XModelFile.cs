using System.Buffers.Binary;
using System.Text;

namespace Foley.Formats.XModel;

/// <summary>LOD de un xmodel: nombre de sus archivos parts/surfs y un material por superficie.</summary>
public sealed record XModelLod(float Distance, string Name, string[] Materials);

/// <summary>Superficie de colisión del xmodel (los triángulos se guardan en crudo, 12 floats cada uno).</summary>
public sealed record XModelCollisionSurface(float[] Triangles, int Bone, uint Contents, uint SurfaceFlags)
{
    public int TriangleCount => Triangles.Length / 12;
}

/// <summary>Archivo <c>xmodel/&lt;nombre&gt;</c> (versión 14). Ver docs/formats/xmodel.md.</summary>
public sealed class XModelFile
{
    public const int Version = 14;

    public float[] Mins { get; }
    public float[] Maxs { get; }
    /// <summary>Solo los LODs con nombre, en orden.</summary>
    public XModelLod[] Lods { get; }
    public int CollisionLod { get; }
    public XModelCollisionSurface[] CollisionSurfaces { get; }

    private XModelFile(float[] mins, float[] maxs, XModelLod[] lods, int collisionLod, XModelCollisionSurface[] coll)
    {
        Mins = mins;
        Maxs = maxs;
        Lods = lods;
        CollisionLod = collisionLod;
        CollisionSurfaces = coll;
    }

    public static XModelFile Read(ReadOnlySpan<byte> data)
    {
        var r = new XReader(data);
        var version = r.U16();
        if (version != Version)
            throw new InvalidDataException($"xmodel: versión {version}, se esperaba {Version}.");
        var mins = new[] { r.F32(), r.F32(), r.F32() };
        var maxs = new[] { r.F32(), r.F32(), r.F32() };

        var lodHeaders = new List<(float dist, string name)>();
        for (var i = 0; i < 3; i++)
        {
            var dist = r.F32();
            var name = r.CString();
            if (name.Length > 0) lodHeaders.Add((dist, name));
        }

        var collisionLod = r.I32();
        var collCount = (int)r.U32();
        var coll = new XModelCollisionSurface[collCount];
        for (var i = 0; i < collCount; i++)
        {
            var tris = new float[checked((int)r.U32() * 12)];
            for (var k = 0; k < tris.Length; k++) tris[k] = r.F32();
            r.Skip(24); // límites
            var bone = r.I32();
            var contents = r.U32();
            var flags = r.U32();
            coll[i] = new XModelCollisionSurface(tris, bone, contents, flags);
        }

        var lods = new XModelLod[lodHeaders.Count];
        for (var i = 0; i < lods.Length; i++)
        {
            var mats = new string[r.U16()];
            for (var k = 0; k < mats.Length; k++) mats[k] = r.CString();
            lods[i] = new XModelLod(lodHeaders[i].dist, lodHeaders[i].name, mats);
        }
        r.ExpectEnd("xmodel");
        return new XModelFile(mins, maxs, lods, collisionLod, coll);
    }
}

/// <summary>Lector secuencial little-endian para los archivos de modelo.</summary>
internal ref struct XReader
{
    private readonly ReadOnlySpan<byte> _data;
    private int _pos;

    public XReader(ReadOnlySpan<byte> data)
    {
        _data = data;
        _pos = 0;
    }

    public int Position => _pos;

    private ReadOnlySpan<byte> Take(int n)
    {
        if (_pos + n > _data.Length)
            throw new InvalidDataException($"Fin de archivo inesperado (offset {_pos}, se piden {n} bytes).");
        var s = _data.Slice(_pos, n);
        _pos += n;
        return s;
    }

    public byte U8() => Take(1)[0];
    public sbyte I8() => (sbyte)Take(1)[0];
    public ushort U16() => BinaryPrimitives.ReadUInt16LittleEndian(Take(2));
    public short I16() => BinaryPrimitives.ReadInt16LittleEndian(Take(2));
    public uint U32() => BinaryPrimitives.ReadUInt32LittleEndian(Take(4));
    public int I32() => BinaryPrimitives.ReadInt32LittleEndian(Take(4));
    public float F32() => BinaryPrimitives.ReadSingleLittleEndian(Take(4));
    public void Skip(int n) => Take(n);

    public string CString()
    {
        var rest = _data[_pos..];
        var end = rest.IndexOf((byte)0);
        if (end < 0) throw new InvalidDataException($"Cadena sin terminar en offset {_pos}.");
        var s = Encoding.Latin1.GetString(rest[..end]);
        _pos += end + 1;
        return s;
    }

    public void ExpectEnd(string what)
    {
        if (_pos != _data.Length)
            throw new InvalidDataException($"{what}: sobran {_data.Length - _pos} bytes al final.");
    }
}
