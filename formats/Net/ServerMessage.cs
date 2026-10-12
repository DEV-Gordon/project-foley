namespace Foley.Formats.Net;

/// <summary>Comandos de mensaje de servidor (<c>svc_ops_e</c>). Valores de ioquake3 qcommon.h, confirmados en CoD1 para 2, 3, 4 y 7.</summary>
public enum ServerCommand
{
    Bad = 0,
    Nop = 1,
    GameState = 2,
    ConfigString = 3,
    Baseline = 4,
    ServerCommand = 5,
    Download = 6,
    Snapshot = 7,
    Eof = 8,
}

/// <summary>Lo que se pudo leer del estado de juego (svc_gamestate) sin las tablas de campos de CoD1.</summary>
public sealed record GameStatePrefix(
    int ReliableAcknowledge,
    int ServerCommandSequence,
    IReadOnlyDictionary<int, string> ConfigStrings,
    ServerCommand NextCommand)
{
    /// <summary>Valor de una clave de la cadena de configuración 0 (serverinfo, formato <c>\clave\valor</c>).</summary>
    public string? ServerInfo(string key) => InfoValue(ConfigStrings.GetValueOrDefault(0), key);

    /// <summary>Valor de una clave de la cadena 1 (systeminfo).</summary>
    public string? SystemInfo(string key) => InfoValue(ConfigStrings.GetValueOrDefault(1), key);

    private static string? InfoValue(string? info, string key)
    {
        if (info is null) return null;
        var parts = info.Split('\\');
        for (var i = 1; i + 1 < parts.Length; i += 2)
            if (string.Equals(parts[i], key, StringComparison.OrdinalIgnoreCase)) return parts[i + 1];
        return null;
    }
}

/// <summary>
/// Cabeceras de los mensajes de servidor de CoD1. Ver docs/formats/demo.md.
/// Basado en ioquake3 code/client/cl_parse.c (CL_ParseServerMessage, CL_ParseGamestate, CL_ParseSnapshot),
/// con la diferencia confirmada de que en CoD1 el reliableAcknowledge va sin comprimir.
/// </summary>
public static class ServerMessage
{
    /// <summary>
    /// CoD1: los 4 primeros bytes son el reliableAcknowledge sin comprimir (int32 little endian);
    /// el resto del mensaje va comprimido con Huffman. En ioquake3 ese entero también va comprimido.
    /// </summary>
    public static MsgReader Open(DemoMessage message, out int reliableAcknowledge)
    {
        if (message.Data.Length < 4) throw new InvalidDataException("Mensaje de servidor demasiado corto.");
        reliableAcknowledge = BitConverter.ToInt32(message.Data, 0);
        return new MsgReader(message.Data) { Bit = 32 };
    }

    /// <summary>
    /// Lee el principio de svc_gamestate: la secuencia de comandos y las cadenas de configuración,
    /// hasta el primer comando que no sea svc_configstring (normalmente svc_baseline, que necesita
    /// las tablas de campos de entidad de CoD1, aún sin determinar).
    /// </summary>
    public static GameStatePrefix ReadGameStatePrefix(DemoMessage message)
    {
        var r = Open(message, out var ack);
        var cmd = (ServerCommand)r.ReadByte();
        if (cmd != ServerCommand.GameState)
            throw new InvalidDataException($"Se esperaba svc_gamestate y llegó {cmd}.");
        var sequence = r.ReadLong();

        var configStrings = new SortedDictionary<int, string>();
        while (true)
        {
            var next = r.ReadByte();
            if (r.Overflowed) throw new InvalidDataException("svc_gamestate termina sin svc_EOF.");
            if ((ServerCommand)next != ServerCommand.ConfigString)
                return new GameStatePrefix(ack, sequence, configStrings, (ServerCommand)next);
            var index = r.ReadShort();
            configStrings[index] = r.ReadString();
        }
    }

    /// <summary>Si el mensaje empieza por svc_snapshot devuelve la hora del servidor (ms); si no, null.</summary>
    public static int? ReadSnapshotServerTime(DemoMessage message)
    {
        var r = Open(message, out _);
        if ((ServerCommand)r.ReadByte() != ServerCommand.Snapshot) return null;
        return r.ReadLong();
    }
}
