using System.Buffers.Binary;
using Foley.Formats.Net;

namespace Foley.Formats.Tests.Net;

/// <summary>Contenedor de demo y lector de bits con datos sintéticos.</summary>
public class DemoTests
{
    private static byte[] Container(params (int seq, byte[] data)[] messages)
    {
        using var ms = new MemoryStream();
        var buf = new byte[4];
        foreach (var (seq, data) in messages)
        {
            BinaryPrimitives.WriteInt32LittleEndian(buf, seq); ms.Write(buf);
            BinaryPrimitives.WriteInt32LittleEndian(buf, data.Length); ms.Write(buf);
            ms.Write(data);
        }
        BinaryPrimitives.WriteInt32LittleEndian(buf, -1); ms.Write(buf);
        ms.Write(buf);
        return ms.ToArray();
    }

    [Fact]
    public void LeeElContenedor()
    {
        var msgs = DemoFile.Read(Container((10, new byte[] { 1, 2, 3 }), (11, new byte[] { 4 })));
        Assert.Equal(2, msgs.Count);
        Assert.Equal(10, msgs[0].Sequence);
        Assert.Equal(new byte[] { 1, 2, 3 }, msgs[0].Data);
        Assert.Equal(11, msgs[1].Sequence);
    }

    [Fact]
    public void RechazaDemosTruncadasOConBasura()
    {
        var ok = Container((1, new byte[] { 9 }));
        Assert.Throws<InvalidDataException>(() => DemoFile.Read(ok.AsSpan(0, ok.Length - 8)));
        Assert.Throws<InvalidDataException>(() => DemoFile.Read(ok.Concat(new byte[] { 0 }).ToArray()));
    }

    [Fact]
    public void LosBitsSueltosSeLeenSinComprimirLsbPrimero()
    {
        // ReadBits(3) y ReadBits(5) leen bits crudos (bits & 7): 0b101 y luego 0b10110.
        var r = new MsgReader(new byte[] { 0b1011_0101 });
        Assert.Equal(0b101, r.ReadBits(3));
        Assert.Equal(0b10110, r.ReadBits(5));
    }

    [Fact]
    public void ElArbolDeQuake3TieneLas256Hojas()
    {
        // Decodificar desde cualquier bit de un buffer aleatorio siempre llega a una hoja:
        // un byte (0..255) o la hoja NYT (256), que en id Tech 3 sigue en el árbol.
        var rnd = new Random(1);
        var data = new byte[4096];
        rnd.NextBytes(data);
        var bit = 0;
        var seen = new HashSet<int>();
        while (bit < data.Length * 8 - 64)
        {
            var s = Huffman.Quake3.Receive(data, ref bit, data.Length * 8);
            Assert.InRange(s, 0, Huffman.Nyt);
            seen.Add(s);
        }
        Assert.True(seen.Count > 200, $"solo {seen.Count} símbolos distintos");
    }
}
