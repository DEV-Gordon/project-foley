using System.Globalization;
using Foley.Formats.Bsp;
using Foley.Formats.Pk3;
using Foley.Sim.Collision;
using Foley.Sim.Math;
using Foley.Sim.Movement;

namespace Foley.Sim.Tests.Movement;

/// <summary>Pmove en el mundo real de burnville. Se salta si COD1_PATH no existe.</summary>
public class PmoveCorpusTests
{
    [CorpusFact]
    public void CaminarPorLaTrincheraNoAtraviesaNadaNiSeQuedaEnSolido()
    {
        BspFile bsp;
        using (var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!))
            bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var map = ClipMap.FromBsp(bsp);
        var start = bsp.Entities.First(e => e.ClassName == "info_player_start");
        var o = start.Get("origin")!.Split(' ').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        var yaw = float.Parse(start.Get("angle") ?? "0", CultureInfo.InvariantCulture);

        var cfg = new PmoveConfig();
        var pm = new Pmove(cfg, map);
        // info_player_start marca el suelo; el origen de la caja de Quake 3 va 24 por encima de los pies.
        var ps = new PlayerState { Origin = new Vec3(o[0], o[1], o[2] - cfg.Mins.Z + 1) };

        // 10 s caminando en varias direcciones, saltando de vez en cuando.
        for (var i = 0; i < 1250; i++)
        {
            var cmdYaw = QMath.AngleToShort(yaw + (i / 125) * 45f);
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, cmdYaw, 0, 127, 0, (sbyte)(i % 200 == 0 ? 127 : 0)));
            var inside = map.Trace(ps.Origin, ps.Origin, cfg.Mins, cfg.Maxs, cfg.TraceMask);
            Assert.False(inside.StartSolid, $"paso {i}: dentro de un sólido en {ps.Origin}");
        }

        // Sigue dentro de la zona jugable (no cayó fuera del mapa).
        Assert.InRange(ps.Origin.Z, -1000f, 2000f);
    }
}
