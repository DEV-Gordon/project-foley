using Foley.Formats.Bsp;
using Foley.Formats.Pk3;
using Foley.Sim.Collision;
using Foley.Sim.Math;
using Foley.Sim.Movement;

namespace Foley.Sim.Tests.Collision;

/// <summary>Colisión de terreno de CoD1 (cápsula contra malla de triángulos). Ver docs/re/colision.md.</summary>
public class TerrainTests
{
    private static readonly Vec3 Mins = new(-15, -15, 0);
    private static readonly Vec3 Maxs = new(15, 15, 70);

    // Cuadrado de 512x512 en z = 0 formado por dos triángulos, con la normal hacia arriba.
    private static ClipMap FlatTerrain(float z = 0)
    {
        var verts = new[] { new Vec3(-256, -256, z), new Vec3(256, -256, z), new Vec3(-256, 256, z), new Vec3(256, 256, z) };
        // (v2 - v0) × (v1 - v0) debe apuntar a +Z: v0=(-,-), v1=(-,+), v2=(+,-)
        var idx = new[] { 0, 2, 1, 1, 2, 3 };
        var t = new TerrainCollide(verts, idx, ClipMap.ContentsSolid, 0);
        return new ClipMap(Array.Empty<CollisionPlane>(), Array.Empty<CollisionBrush>(), new[] { t });
    }

    [Fact]
    public void LaNormalDelTerrenoPlanoApuntaArriba()
    {
        var t = FlatTerrain().Terrains[0];
        Assert.False(t.FacesDown);
    }

    [Fact]
    public void LaCajaCaeYSeApoyaComoSobreUnBrush()
    {
        // La esfera inferior de la cápsula (radio 15 + 0.125) está a 15 sobre los pies: se apoya a ~0.125.
        var tr = FlatTerrain().Trace(new Vec3(0, 0, 50), new Vec3(0, 0, -50), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.True(tr.Hit);
        Assert.InRange(tr.EndPos.Z, 0.12f, 0.13f);
        Assert.Equal(1f, tr.Plane.Normal.Z, 4);
        Assert.False(tr.StartSolid);
    }

    [Fact]
    public void ElTrazadoDePosicionNoDetectaElTerreno()
    {
        // Con delta nulo el barrido del original no entra en ningún caso (denominador 0).
        var tr = FlatTerrain().Trace(new Vec3(0, 0, -5), new Vec3(0, 0, -5), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.False(tr.StartSolid);
        Assert.False(tr.Hit);
    }

    [Fact]
    public void ElJugadorCaminaSobreElTerreno()
    {
        var pm = new Pmove(new PmoveConfig(), FlatTerrain());
        var ps = new PlayerState { Origin = new Vec3(0, 0, 20), JumpTime = -10000 };
        for (var i = 0; i < 250; i++)
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, i > 100 ? (sbyte)127 : (sbyte)0, 0, 0));
        Assert.True(ps.OnGround);
        Assert.InRange(ps.Origin.Z, 0.1f, 0.2f);
        Assert.True(ps.Origin.X > 100);
    }

    [CorpusFact]
    public void CasiTodoElSueloVisibleDeBurnvilleTieneColision()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var map = ClipMap.FromBsp(bsp);
        Assert.Equal(1112, map.Terrains.Length);

        int total = 0, miss = 0;
        foreach (var s in bsp.Surfaces)
        {
            for (var t = 0; t < s.IndexCount; t += 3 * 7)
            {
                var a = bsp.Vertices[(int)s.FirstVertex + bsp.Indices[s.FirstIndex + t]];
                var b = bsp.Vertices[(int)s.FirstVertex + bsp.Indices[s.FirstIndex + t + 1]];
                var c = bsp.Vertices[(int)s.FirstVertex + bsp.Indices[s.FirstIndex + t + 2]];
                if (a.Nz < 0.7f) continue;
                var cx = (a.X + b.X + c.X) / 3; var cy = (a.Y + b.Y + c.Y) / 3; var cz = (a.Z + b.Z + c.Z) / 3;
                var tr = map.Trace(new Vec3(cx, cy, cz + 64), new Vec3(cx, cy, cz - 8), Mins, Maxs, ClipMap.MaskPlayerSolid);
                total++;
                if (!tr.Hit || tr.EndPos.Z < cz - 4) miss++;
            }
        }
        // Sin terreno faltaba el 69 %. Lo que queda son sobre todo curvas (aún sin portar).
        Assert.True(miss < total * 0.03, $"sin colisión {miss} de {total}");
    }
}
