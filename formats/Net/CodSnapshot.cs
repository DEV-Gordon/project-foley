using System.Buffers.Binary;

namespace Foley.Formats.Net;

/// <summary>Estado del jugador de CoD1 en bruto: los bytes de playerState_t, accesibles por offset.</summary>
public sealed class CodPlayerState
{
    public byte[] Raw { get; }

    public CodPlayerState() => Raw = new byte[CodNetFields.PlayerStateSize];
    private CodPlayerState(byte[] raw) => Raw = raw;

    public CodPlayerState Clone() => new((byte[])Raw.Clone());

    public int Int(int offset) => BinaryPrimitives.ReadInt32LittleEndian(Raw.AsSpan(offset));
    public float Float(int offset) => BinaryPrimitives.ReadSingleLittleEndian(Raw.AsSpan(offset));
    internal void SetInt(int offset, int value) => BinaryPrimitives.WriteInt32LittleEndian(Raw.AsSpan(offset), value);

    // Offsets de docs/re/red.md (playerStateFields 1.5).
    public int CommandTime => Int(0);
    public int PmType => Int(4);
    public int PmFlags => Int(12);
    public float OriginX => Float(20);
    public float OriginY => Float(24);
    public float OriginZ => Float(28);
    public float VelocityX => Float(32);
    public float VelocityY => Float(36);
    public float VelocityZ => Float(40);
    public int Gravity => Int(60);
    public int Speed => Int(68);
    public int GroundEntityNum => Int(84);
    public float Pitch => Float(192);
    public float Yaw => Float(196);
    public float Roll => Float(200);
    public float MinsX => Float(804);
    public float MinsY => Float(808);
    public float MinsZ => Float(812);
    public float MaxsX => Float(816);
    public float MaxsY => Float(820);
    public float MaxsZ => Float(824);
    public int ProneViewHeight => Int(828);
    public int CrouchViewHeight => Int(832);
    public int StandViewHeight => Int(836);
    public float Friction => Float(892);
}

public sealed record CodSnapshot(int MessageNum, int ServerTime, int DeltaNum, int SnapFlags, int FieldCount, CodPlayerState PlayerState);

/// <summary>
/// Lectura de instantáneas de demo de CoD1 hasta el estado del jugador (no lee entidades).
/// Formato de CoDMP.exe 1.5: CL_ParseSnapshot @0x4173e0 y lectura del estado @0x44a630.
/// Ver docs/formats/demo.md.
/// </summary>
public static class CodSnapshotReader
{
    /// <summary>
    /// Recorre los mensajes de una demo y devuelve el estado del jugador de cada instantánea,
    /// resolviendo los deltas contra las instantáneas anteriores.
    /// </summary>
    public static List<CodSnapshot> ReadAll(IReadOnlyList<DemoMessage> messages)
    {
        var result = new List<CodSnapshot>();
        var byMessage = new Dictionary<int, CodPlayerState>();
        foreach (var m in messages)
        {
            var msg = CodMessage.FromDemo(m, out _);
            while (true)
            {
                var cmd = msg.ReadByte();
                if (cmd < 0 || cmd == (int)ServerCommand.Eof) break;
                if (cmd == (int)ServerCommand.ServerCommand)
                {
                    msg.ReadLong();   // secuencia
                    msg.ReadString(); // texto del comando
                    continue;
                }
                if (cmd == (int)ServerCommand.Snapshot)
                {
                    var snap = ReadSnapshot(msg, m.Sequence, byMessage);
                    if (snap is not null)
                    {
                        byMessage[m.Sequence] = snap.PlayerState;
                        result.Add(snap);
                    }
                }
                // gamestate, baselines y el resto de la instantánea (entidades) no se leen todavía.
                break;
            }
        }
        return result;
    }

    private static CodSnapshot? ReadSnapshot(CodMessage msg, int messageNum, Dictionary<int, CodPlayerState> history)
    {
        var serverTime = msg.ReadLong();
        var deltaByte = msg.ReadByte();
        var snapFlags = msg.ReadByte();

        CodPlayerState? from = null;
        if (deltaByte != 0)
        {
            // delta respecto al mensaje messageNum - deltaByte; si no lo tenemos, no se puede reconstruir
            if (!history.TryGetValue(messageNum - deltaByte, out from)) return null;
        }

        var ps = ReadDeltaPlayerState(msg, from, out var lc);
        return new CodSnapshot(messageNum, serverTime, deltaByte, snapFlags, lc, ps);
    }

    /// <summary>
    /// Campos del estado del jugador en delta (CoDMP.exe 1.5 @0x44a630): byte lc y, para cada uno de
    /// los lc primeros campos, un bit "cambiado" y su valor. El resto se copia del estado base. Los
    /// bloques posteriores (estadísticas, munición, objetivos, HUD) no se leen.
    /// </summary>
    public static CodPlayerState ReadDeltaPlayerState(CodMessage msg, CodPlayerState? from, out int lc)
    {
        var to = from?.Clone() ?? new CodPlayerState();
        lc = msg.ReadByte();
        var fields = CodNetFields.PlayerState;
        if (lc < 0 || lc > fields.Length)
            throw new InvalidDataException($"Cantidad de campos del jugador inválida: {lc}.");

        for (var i = 0; i < lc; i++)
        {
            var f = fields[i];
            if (!msg.ReadBit()) continue; // sin cambios: ya copiado del estado base
            var value = f.Bits == 0 ? msg.ReadFieldFloatBits() : msg.ReadFieldInt(f.Bits);
            to.SetInt(f.Offset, value);
        }
        if (msg.Overflowed) throw new InvalidDataException("El estado del jugador se sale del mensaje.");
        return to;
    }
}
