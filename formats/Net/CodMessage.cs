using System.Buffers.Binary;
using System.Text;

namespace Foley.Formats.Net;

/// <summary>
/// Mensaje de servidor de CoD1 ya descomprimido, con su lector. Ver docs/formats/demo.md y
/// docs/re/red.md.
///
/// Diferencias con ioquake3 (msg.c), observadas en CoDMP.exe 1.5:
/// - El mensaje se descomprime entero (Huffman de id Tech 3) y luego se lee sobre bytes planos.
/// - Los bytes se leen en orden (<c>readcount</c>). Los bits salen de un byte "reserva": cuando
///   el cursor de bits está alineado (bit &amp; 7 == 0) se toma el siguiente byte del flujo y se
///   consumen sus bits LSB primero. Bytes y reservas de bits quedan intercalados.
/// </summary>
public sealed class CodMessage
{
    private readonly byte[] _data;
    private int _bit;

    public int ReadCount { get; private set; }
    public int Length => _data.Length;
    public bool Overflowed => ReadCount > _data.Length;

    public CodMessage(byte[] decompressed) => _data = decompressed;

    /// <summary>
    /// Descomprime un mensaje de demo: 4 bytes sin comprimir (reliableAcknowledge) y el resto con
    /// Huffman, símbolo a símbolo hasta agotar los bits. Los bytes de relleno finales pueden
    /// producir símbolos sobrantes tras svc_EOF; no se leen.
    /// </summary>
    public static CodMessage FromDemo(DemoMessage message, out int reliableAcknowledge, Huffman? huffman = null)
    {
        var data = message.Data;
        if (data.Length < 4) throw new InvalidDataException("Mensaje de servidor demasiado corto.");
        reliableAcknowledge = BinaryPrimitives.ReadInt32LittleEndian(data);
        huffman ??= Huffman.Quake3;

        var payload = data.AsSpan(4);
        var maxBit = payload.Length * 8;
        var output = new List<byte>(payload.Length * 2);
        var bit = 0;
        while (bit < maxBit)
        {
            var symbol = huffman.Receive(payload, ref bit, maxBit);
            if (bit > maxBit) break; // código incompleto al final: relleno
            output.Add((byte)symbol);
        }
        return new CodMessage(output.ToArray());
    }

    public int ReadByte()
    {
        if (ReadCount >= _data.Length)
        {
            ReadCount = _data.Length + 1;
            return -1;
        }
        return _data[ReadCount++];
    }

    public int ReadShort()
    {
        if (ReadCount + 2 > _data.Length)
        {
            ReadCount = _data.Length + 1;
            return -1;
        }
        var v = BinaryPrimitives.ReadInt16LittleEndian(_data.AsSpan(ReadCount));
        ReadCount += 2;
        return v;
    }

    public int ReadLong()
    {
        if (ReadCount + 4 > _data.Length)
        {
            ReadCount = _data.Length + 1;
            return -1;
        }
        var v = BinaryPrimitives.ReadInt32LittleEndian(_data.AsSpan(ReadCount));
        ReadCount += 4;
        return v;
    }

    public float ReadFloat() => BitConverter.Int32BitsToSingle(ReadLong());

    public string ReadString()
    {
        var sb = new StringBuilder();
        while (true)
        {
            var c = ReadByte();
            if (c is -1 or 0) break;
            sb.Append((char)c);
        }
        return sb.ToString();
    }

    /// <summary>Lee <paramref name="count"/> bits de la reserva, LSB primero (CoDMP.exe 1.5 @0x4475e0).</summary>
    public int ReadBits(int count)
    {
        var value = 0;
        for (var i = 0; i < count; i++)
        {
            if ((_bit & 7) == 0)
            {
                // tomar un byte nuevo del flujo como reserva de bits
                _bit = ReadCount * 8;
                ReadCount++;
            }
            if ((_bit >> 3) >= _data.Length)
            {
                ReadCount = _data.Length + 1;
                return 0;
            }
            value |= ((_data[_bit >> 3] >> (_bit & 7)) & 1) << i;
            _bit++;
        }
        return value;
    }

    public bool ReadBit() => ReadBits(1) != 0;

    /// <summary>
    /// Entero de <paramref name="bits"/> bits de un campo de red: primero <c>bits &amp; 7</c> bits de la
    /// reserva y después bytes completos, LSB primero (CoDMP.exe 1.5 @0x44a630). Sin extensión de signo.
    /// </summary>
    public int ReadFieldInt(int bits)
    {
        if (bits < 0) bits = -bits;
        var low = bits & 7;
        var value = low == 0 ? 0 : ReadBits(low);
        for (var shift = low; shift < bits; shift += 8)
        {
            var b = ReadByte();
            value |= (b & 0xFF) << shift;
        }
        return value;
    }

    /// <summary>
    /// Float de un campo de red (bits = 0): un bit elige entre float completo (4 bytes) o entero de
    /// 13 bits: 5 bits de la reserva más un byte, valor = bajos + (byte - 128) * 32 (CoDMP.exe 1.5 @0x44a630).
    /// Devuelve los bits crudos del float.
    /// </summary>
    public int ReadFieldFloatBits()
    {
        if (ReadBit()) return ReadLong();
        var low = ReadBits(5);
        var high = ReadByte();
        var trunc = low + (high - 0x80) * 0x20;
        return BitConverter.SingleToInt32Bits(trunc);
    }
}
