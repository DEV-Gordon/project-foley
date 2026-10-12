using Foley.Formats.Bsp;
using Foley.Formats.Pk3;
using Foley.Sim.Collision;
using Foley.Sim.Math;
using Foley.Sim.Movement;

namespace Foley.Sim.Tests.Collision;

/// <summary>Colisión de curvas de CoD1 (port de cm_patch.c de ioquake3 con las diferencias de CoD). Ver docs/re/colision.md.</summary>
public class PatchTests
{
    private static readonly Vec3 Mins = new(-15, -15, 0);
    private static readonly Vec3 Maxs = new(15, 15, 70);

    private static ClipMap Single(PatchCollide p) =>
        new(Array.Empty<CollisionPlane>(), Array.Empty<CollisionBrush>(), null, new[] { p });

    // Rejilla 3x3 de 256 de lado; height(i) da la altura de cada columna de puntos de control.
    private static List<Vec3> Grid(Func<int, float> height)
    {
        var pts = new List<Vec3>();
        for (var j = 0; j < 3; j++)
            for (var i = 0; i < 3; i++)
                pts.Add(new Vec3(i * 128 - 128, j * 128 - 128, height(i)));
        return pts;
    }

    private static PatchCollide Flat() => new(3, 3, Grid(_ => 0), 4, ClipMap.ContentsSolid, 0);

    [Fact]
    public void LaCajaSeApoyaSobreUnaCurvaPlanaConElEpsilonDeQuake3()
    {
        var tr = Single(Flat()).Trace(new Vec3(10, 20, 50), new Vec3(10, 20, -50), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.True(tr.Hit);
        Assert.InRange(tr.EndPos.Z, 0.12f, 0.13f);
        Assert.Equal(1f, tr.Plane.Normal.Z, 4);
    }

    [Fact]
    public void LaCaraDeAtrasNoRecorta()
    {
        var tr = Single(Flat()).Trace(new Vec3(0, 0, -120), new Vec3(0, 0, 50), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.False(tr.Hit);
    }

    [Fact]
    public void UnTrazadoDePosicionNoDetectaLaCurva()
    {
        // Como el terreno: el barrido de CoD1 con desplazamiento nulo no recorta.
        var tr = Single(Flat()).Trace(new Vec3(0, 0, -5), new Vec3(0, 0, -5), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.False(tr.Hit);
        Assert.False(tr.StartSolid);
    }

    [Fact]
    public void ElUmbralDeLaPiezaControlaLaSubdivision()
    {
        // Arco: el punto de control central sube 64; la curva real llega a 32 en el centro.
        var pts = Grid(i => i == 1 ? 64 : 0);
        var fine = new PatchCollide(3, 3, pts, 2, ClipMap.ContentsSolid, 0);
        var coarse = new PatchCollide(3, 3, pts, 64, ClipMap.ContentsSolid, 0);
        Assert.True(fine.FacetCount > coarse.FacetCount);

        // Con subdivisión fina la caja se apoya en la cima de la curva (32) más el épsilon.
        var tr = Single(fine).Trace(new Vec3(0, 0, 100), new Vec3(0, 0, -50), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.InRange(tr.EndPos.Z, 29f, 33f);
    }

    [Fact]
    public void ElJugadorCaminaSobreUnaCurva()
    {
        var pm = new Pmove(new PmoveConfig(), Single(Flat()));
        var ps = new PlayerState { Origin = new Vec3(-60, 0, 20), JumpTime = -10000 };
        for (var i = 0; i < 125; i++)
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, i > 40 ? (sbyte)127 : (sbyte)0, 0, 0));
        Assert.True(ps.OnGround);
        Assert.InRange(ps.Origin.Z, 0.1f, 0.2f);
        Assert.True(ps.Origin.X > 0);
    }

    [CorpusFact]
    public void LasCurvasDeBurnvilleSeGeneranDentroDeLosLimites()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var map = ClipMap.FromBsp(BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp")));
        Assert.Equal(512, map.Curves.Length);
        Assert.All(map.Curves, c => Assert.True(c.FacetCount > 0));
        // MAX_FACETS 1024 y 4 + 6 + 16 bordes por faceta (@0x414950, @0x414240)
        Assert.True(map.Curves.Max(c => c.MaxBorderCount) <= 26);
    }
}
