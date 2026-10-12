using System.Globalization;
using Foley.Formats.Bsp;
using Foley.Formats.Pk3;
using Foley.Sim.Collision;
using Foley.Sim.Math;

namespace Foley.Sim.Tests.Collision;

/// <summary>Trazado contra el mundo real de burnville. Se salta si COD1_PATH no existe.</summary>
public class ClipMapCorpusTests
{
    private static (ClipMap map, Vec3 start) Load()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var origin = bsp.Entities.First(e => e.ClassName == "info_player_start").Get("origin")!
            .Split(' ').Select(s => float.Parse(s, CultureInfo.InvariantCulture)).ToArray();
        return (ClipMap.FromBsp(bsp), new Vec3(origin[0], origin[1], origin[2]));
    }

    // Caja solo para el test; las dimensiones reales del jugador de CoD1 son TODO(fidelidad) del pmove.
    private static readonly Vec3 Mins = new(-15, -15, 0);
    private static readonly Vec3 Maxs = new(15, 15, 60);

    [CorpusFact]
    public void ElMundoTieneLosBrushesDelModeloCero()
    {
        var (map, _) = Load();
        Assert.Equal(4946, map.Brushes.Length);
    }

    [CorpusFact]
    public void ElPuntoDeInicioTieneSueloDebajoYNoEstaEnSolido()
    {
        var (map, start) = Load();
        var lifted = start + new Vec3(0, 0, 8);
        var position = map.Trace(lifted, lifted, Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.False(position.StartSolid, $"El inicio {lifted} está dentro de un sólido");

        var down = map.Trace(lifted, lifted - new Vec3(0, 0, 256), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.True(down.Hit, "No hay suelo bajo el punto de inicio");
        Assert.True(down.Plane.Normal.Z > 0.7f, $"El suelo no es caminable: normal {down.Plane.Normal}");
    }

    [CorpusFact]
    public void ElInicioEstaEncerradoEnHorizontal()
    {
        // Burnville empieza en una trinchera: en las 8 direcciones debe haber pared en menos de 4096 unidades.
        var (map, start) = Load();
        var eye = start + new Vec3(0, 0, 32);
        for (var i = 0; i < 8; i++)
        {
            var a = i * MathF.PI / 4f;
            var dir = new Vec3(MathF.Cos(a), MathF.Sin(a), 0) * 4096f;
            var t = map.Trace(eye, eye + dir, Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
            Assert.True(t.Hit, $"Sin pared en la dirección {i}");
        }
    }
}
