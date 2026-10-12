using System.Buffers.Binary;

namespace Foley.Formats.Net;

/// <summary>Mensaje de servidor grabado en una demo: número de secuencia y bytes comprimidos.</summary>
public sealed record DemoMessage(int Sequence, byte[] Data);

/// <summary>
/// Contenedor de demo de id Tech 3: <c>[int32 secuencia][int32 longitud][mensaje]</c>...,
/// terminado por longitud -1. Formato de ioquake3 code/client/cl_main.c (CL_ReadDemoMessage).
/// Ver docs/formats/demo.md.
/// </summary>
public static class DemoFile
{
    /// <summary>MAX_MSGLEN de ioquake3 qcommon.h.</summary>
    public const int MaxMessageLength = 16384;

    public static List<DemoMessage> Read(ReadOnlySpan<byte> data)
    {
        var messages = new List<DemoMessage>();
        var pos = 0;
        while (true)
        {
            if (pos + 8 > data.Length)
                throw new InvalidDataException($"Demo truncada en el byte {pos} (falta el marcador de fin).");
            var sequence = BinaryPrimitives.ReadInt32LittleEndian(data[pos..]);
            var length = BinaryPrimitives.ReadInt32LittleEndian(data[(pos + 4)..]);
            pos += 8;
            if (length == -1) break; // fin de la demo
            if (length < 0 || length > MaxMessageLength)
                throw new InvalidDataException($"Mensaje {messages.Count}: longitud {length} fuera de rango.");
            if (pos + length > data.Length)
                throw new InvalidDataException($"Mensaje {messages.Count}: se sale del archivo.");
            messages.Add(new DemoMessage(sequence, data.Slice(pos, length).ToArray()));
            pos += length;
        }
        if (pos != data.Length)
            throw new InvalidDataException($"Quedan {data.Length - pos} bytes después del marcador de fin.");
        return messages;
    }
}
