using Foley.Formats.Net;

namespace Foley.Formats.Tests.Net;

/// <summary>
/// Demo incluida en la instalación (Main/demos/Timedemo1.dm_2). Se salta si COD1_PATH no existe.
/// Los valores esperados se observaron al decodificarla (docs/formats/demo.md).
/// </summary>
public class DemoCorpusTests
{
    private static List<DemoMessage> Load()
    {
        var root = Foley.Formats.Pk3.Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!;
        var path = Path.Combine(root, "demos", "Timedemo1.dm_2");
        return DemoFile.Read(File.ReadAllBytes(path));
    }

    [CorpusFact]
    public void ElContenedorTiene1534MensajesConsecutivos()
    {
        var msgs = Load();
        Assert.Equal(1534, msgs.Count);
        for (var i = 1; i < msgs.Count; i++) Assert.Equal(msgs[i - 1].Sequence + 1, msgs[i].Sequence);
    }

    [CorpusFact]
    public void ElEstadoDeJuegoSeDecodificaConElHuffmanDeQuake3()
    {
        var gs = ServerMessage.ReadGameStatePrefix(Load()[0]);
        Assert.Equal(37, gs.ReliableAcknowledge);
        Assert.Equal(520, gs.ServerCommandSequence);
        Assert.Equal(259, gs.ConfigStrings.Count);
        Assert.Equal("mp_dawnville", gs.ServerInfo("mapname"));
        Assert.Equal("2", gs.ServerInfo("protocol"));
        Assert.Equal("0", gs.SystemInfo("pmove_fixed"));
        Assert.Equal("8", gs.SystemInfo("pmove_msec"));
        Assert.Equal(ServerCommand.Baseline, gs.NextCommand);
    }

    [CorpusFact]
    public void LasInstantaneasVanCada50msDuranteUnos77Segundos()
    {
        var times = Load().Skip(1).Select(ServerMessage.ReadSnapshotServerTime).Where(t => t is not null).Select(t => t!.Value).ToList();
        Assert.Equal(1499, times.Count);
        Assert.Equal(3240800, times[0]);
        Assert.Equal(3317500, times[^1]);
        var deltas = times.Zip(times.Skip(1), (a, b) => b - a).ToList();
        Assert.All(deltas, d => Assert.Equal(0, d % 50));
        Assert.True(deltas.Count(d => d == 50) > 1400);
    }

    [CorpusFact]
    public void ElEstadoDelJugadorSeDecodificaEnTodasLasInstantaneas()
    {
        var snaps = CodSnapshotReader.ReadAll(Load());
        Assert.Equal(1533, snaps.Count);

        var first = snaps[0].PlayerState;
        Assert.Equal(3240725, first.CommandTime);
        Assert.Equal(-2139.38f, first.OriginX, 2);
        Assert.Equal(-16258.09f, first.OriginY, 2);

        // Valores de movimiento que el servidor envía (docs/formats/demo.md).
        var alive = snaps.Select(s => s.PlayerState).Where(p => p.PmType == 0).ToList();
        Assert.All(alive, p => Assert.Equal(190, p.Speed));
        Assert.All(alive, p => Assert.Equal(800, p.Gravity));
        Assert.All(snaps, s => Assert.Equal((-15f, -15f, 0f), (s.PlayerState.MinsX, s.PlayerState.MinsY, s.PlayerState.MinsZ)));
        Assert.All(snaps, s => Assert.Equal((15f, 15f, 70f), (s.PlayerState.MaxsX, s.PlayerState.MaxsY, s.PlayerState.MaxsZ)));
        Assert.All(snaps, s => Assert.Equal((11, 40, 60), (s.PlayerState.ProneViewHeight, s.PlayerState.CrouchViewHeight, s.PlayerState.StandViewHeight)));

        // commandTime no retrocede mientras el jugador está vivo. Solo retrocede una vez, al pasar
        // a pm_type 5 (intermedio de fin de ronda, mensaje 27490): comportamiento del juego.
        for (var i = 1; i < snaps.Count; i++)
        {
            var a = snaps[i - 1].PlayerState;
            var b = snaps[i].PlayerState;
            if (a.PmType == 0 && b.PmType == 0) Assert.True(b.CommandTime >= a.CommandTime);
        }
    }

    [CorpusFact]
    public void LasPosicionesCaenDentroDelMapaDeLaDemo()
    {
        // Validación cruzada: si los campos estuvieran mal decodificados, el origen saldría del mundo.
        using var pk3 = new Foley.Formats.Pk3.Pk3Collection(Foley.Formats.Pk3.Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var world = Foley.Formats.Bsp.BspFile.Read(pk3.ReadAllBytes("maps/mp/mp_dawnville.bsp")).Models[0];
        foreach (var s in CodSnapshotReader.ReadAll(Load()).Where(s => s.PlayerState.PmType == 0))
        {
            var p = s.PlayerState;
            Assert.InRange(p.OriginX, world.MinX, world.MaxX);
            Assert.InRange(p.OriginY, world.MinY, world.MaxY);
            Assert.InRange(p.OriginZ, world.MinZ, world.MaxZ);
        }
    }
}
