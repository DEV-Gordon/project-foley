using System.Text;

namespace Foley.Formats.Net;

/// <summary>
/// Lectura de un mensaje comprimido de id Tech 3 (modo "bitstream", no OOB).
/// Portado de ioquake3 code/qcommon/msg.c: MSG_ReadBits, MSG_ReadByte, MSG_ReadShort,
/// MSG_ReadLong, MSG_ReadString, MSG_ReadBigString.
/// </summary>
public sealed class MsgReader
{
    private readonly byte[] _data;
    private readonly Huffman _huffman;

    public int CurSize { get; }
    /// <summary>Posición en bits (<c>msg-&gt;bit</c>).</summary>
    public int Bit { get; set; }
    /// <summary><c>msg-&gt;readcount</c>; si supera <see cref="CurSize"/> el mensaje se terminó.</summary>
    public int ReadCount { get; private set; }

    public bool Overflowed => ReadCount > CurSize;

    public MsgReader(byte[] data, Huffman? huffman = null)
    {
        _data = data;
        CurSize = data.Length;
        _huffman = huffman ?? Huffman.Quake3;
    }

    /// <summary>MSG_ReadBits (rama comprimida). <paramref name="bits"/> negativo = con signo.</summary>
    public int ReadBits(int bits)
    {
        if (ReadCount > CurSize) return 0;

        var value = 0;
        var sgn = false;
        if (bits < 0)
        {
            bits = -bits;
            sgn = true;
        }

        var nbits = 0;
        var bit = Bit;
        if ((bits & 7) != 0)
        {
            nbits = bits & 7;
            if (bit + nbits > CurSize << 3)
            {
                ReadCount = CurSize + 1;
                return 0;
            }
            for (var i = 0; i < nbits; i++)
                value |= Huffman.GetBit(_data, ref bit) << i;
            bits -= nbits;
        }
        if (bits != 0)
        {
            for (var i = 0; i < bits; i += 8)
            {
                var get = _huffman.Receive(_data, ref bit, CurSize << 3);
                value = (int)((uint)value | ((uint)get << (i + nbits)));
                if (bit > CurSize << 3)
                {
                    Bit = bit;
                    ReadCount = CurSize + 1;
                    return 0;
                }
            }
        }
        Bit = bit;
        ReadCount = (bit >> 3) + 1;

        // Como en el original, la extensión de signo usa 'bits' ya reducido por los bits sueltos.
        if (sgn && bits > 0 && bits < 32)
        {
            if ((value & (1 << (bits - 1))) != 0) value |= -1 ^ ((1 << bits) - 1);
        }
        return value;
    }

    /// <summary>MSG_ReadByte: -1 si se acabó el mensaje.</summary>
    public int ReadByte()
    {
        var c = (byte)ReadBits(8);
        return ReadCount > CurSize ? -1 : c;
    }

    /// <summary>MSG_ReadShort.</summary>
    public int ReadShort()
    {
        var c = (short)ReadBits(16);
        return ReadCount > CurSize ? -1 : c;
    }

    /// <summary>MSG_ReadLong.</summary>
    public int ReadLong()
    {
        var c = ReadBits(32);
        return ReadCount > CurSize ? -1 : c;
    }

    /// <summary>
    /// MSG_ReadString / MSG_ReadBigString sin las sustituciones de seguridad del original
    /// ('%' por '.', bytes &gt; 127 por '.'), para no perder datos. Termina en 0 o al acabarse el mensaje.
    /// </summary>
    public string ReadString(int maxLength = 8192)
    {
        var sb = new StringBuilder();
        while (true)
        {
            var c = ReadByte();
            if (c is -1 or 0) break;
            if (sb.Length < maxLength - 1) sb.Append((char)c);
        }
        return sb.ToString();
    }
}
