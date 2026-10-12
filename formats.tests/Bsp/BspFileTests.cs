using System.Buffers.Binary;
using System.Text;
using Foley.Formats.Bsp;

namespace Foley.Formats.Tests.Bsp;

/// <summary>Tests con un BSP sintético construido aquí; no usan datos del juego.</summary>
public class BspFileTests
{
    private static byte[] Material(string name, uint surf = 0, uint content = 0)
    {
        var b = new byte[72];
        Encoding.Latin1.GetBytes(name, b);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(64), surf);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(68), content);
        return b;
    }

    private static byte[] Surface(ushort mat, ushort lm, uint fv, ushort vc, ushort ic, uint fi)
    {
        var b = new byte[16];
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(0), mat);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(2), lm);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(4), fv);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(8), vc);
        BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(10), ic);
        BinaryPrimitives.WriteUInt32LittleEndian(b.AsSpan(12), fi);
        return b;
    }

    private static byte[] Vertex(float x, float y, float z, float u, float v, float lu, float lv,
        float nx, float ny, float nz, byte r, byte g, byte bl, byte a)
    {
        var b = new byte[44];
        float[] f = { x, y, z, u, v, lu, lv, nx, ny, nz };
        for (var i = 0; i < f.Length; i++)
            BinaryPrimitives.WriteSingleLittleEndian(b.AsSpan(i * 4), f[i]);
        b[40] = r; b[41] = g; b[42] = bl; b[43] = a;
        return b;
    }

    private static byte[] Indices(params ushort[] idx)
    {
        var b = new byte[idx.Length * 2];
        for (var i = 0; i < idx.Length; i++)
            BinaryPrimitives.WriteUInt16LittleEndian(b.AsSpan(i * 2), idx[i]);
        return b;
    }

    private static byte[] Build(Dictionary<int, byte[]> lumps, int version = 59, string magic = "IBSP")
    {
        var headerSize = 8 + BspFile.LumpCount * 8;
        using var ms = new MemoryStream();
        var header = new byte[headerSize];
        Encoding.ASCII.GetBytes(magic, header);
        BinaryPrimitives.WriteInt32LittleEndian(header.AsSpan(4), version);
        var offset = headerSize;
        for (var i = 0; i < BspFile.LumpCount; i++)
        {
            var data = lumps.GetValueOrDefault(i) ?? Array.Empty<byte>();
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(8 + i * 8), (uint)data.Length);
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(12 + i * 8), (uint)offset);
            offset += data.Length;
        }
        ms.Write(header);
        for (var i = 0; i < BspFile.LumpCount; i++)
            if (lumps.TryGetValue(i, out var d)) ms.Write(d);
        return ms.ToArray();
    }

    private static Dictionary<int, byte[]> ValidLumps() => new()
    {
        [BspFile.LumpMaterials] = Material("textures/a/b", 5, 7),
        [BspFile.LumpVertices] = Vertex(1, 2, 3, 0.5f, 1.5f, 0.25f, 0.75f, 0, 0, 1, 10, 20, 30, 40)
            .Concat(Vertex(4, 5, 6, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0))
            .Concat(Vertex(7, 8, 9, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0)).ToArray(),
        [BspFile.LumpIndices] = Indices(0, 1, 2),
        [BspFile.LumpSurfaces] = Surface(0, 65535, 0, 3, 3, 0),
        [BspFile.LumpEntities] = Encoding.Latin1.GetBytes("{\n\"classname\" \"worldspawn\"\n\"n\" \"a b\"\n}\0"),
    };

    [Fact]
    public void LeeTodosLosLumpsUsados()
    {
        var bsp = BspFile.Read(Build(ValidLumps()));

        var m = Assert.Single(bsp.Materials);
        Assert.Equal(new BspMaterial("textures/a/b", 5, 7), m);

        Assert.Equal(3, bsp.Vertices.Length);
        Assert.Equal(new BspVertex(1, 2, 3, 0.5f, 1.5f, 0.25f, 0.75f, 0, 0, 1, 10, 20, 30, 40), bsp.Vertices[0]);

        Assert.Equal(new ushort[] { 0, 1, 2 }, bsp.Indices);

        var s = Assert.Single(bsp.Surfaces);
        Assert.Equal(new BspSurface(0, BspSurface.NoLightmap, 0, 3, 3, 0), s);

        var e = Assert.Single(bsp.Entities);
        Assert.Equal("worldspawn", e.ClassName);
        Assert.Equal("a b", e.Get("N")); // claves sin distinguir mayúsculas
    }

    [Fact]
    public void RechazaFirmaInvalida() =>
        Assert.Throws<InvalidDataException>(() => BspFile.Read(Build(ValidLumps(), magic: "XXXX")));

    [Fact]
    public void RechazaVersionDistinta() =>
        Assert.Throws<InvalidDataException>(() => BspFile.Read(Build(ValidLumps(), version: 46)));

    [Fact]
    public void RechazaArchivoCortoOLumpFueraDeRango()
    {
        Assert.Throws<InvalidDataException>(() => BspFile.Read(new byte[10]));
        var data = Build(ValidLumps());
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(8 + BspFile.LumpVertices * 8), 99999);
        Assert.Throws<InvalidDataException>(() => BspFile.Read(data));
    }

    [Fact]
    public void RechazaSuperficieConRangosInvalidos()
    {
        var l = ValidLumps();
        l[BspFile.LumpSurfaces] = Surface(0, 0, 1, 3, 3, 0); // vértices 1..4, hay 3
        Assert.Throws<InvalidDataException>(() => BspFile.Read(Build(l)));

        l = ValidLumps();
        l[BspFile.LumpSurfaces] = Surface(9, 0, 0, 3, 3, 0); // material inexistente
        Assert.Throws<InvalidDataException>(() => BspFile.Read(Build(l)));

        l = ValidLumps();
        l[BspFile.LumpSurfaces] = Surface(0, 0, 0, 3, 2, 0); // índices no múltiplo de 3
        Assert.Throws<InvalidDataException>(() => BspFile.Read(Build(l)));
    }

    [Fact]
    public void RechazaEntidadesMalformadas()
    {
        var l = ValidLumps();
        l[BspFile.LumpEntities] = Encoding.Latin1.GetBytes("{ \"a\" \"b\"");
        Assert.Throws<InvalidDataException>(() => BspFile.Read(Build(l)));
    }
}
