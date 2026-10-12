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

    [Theory]
    [InlineData(124f, -16084f)]   // justo sobre la arista
    [InlineData(124.5f, -16084f)]
    [InlineData(123.5f, -16084f)]
    public void LaCapsulaChocaConLaAristaEntreDosTriangulos(float x, float y)
    {
        // Cresta suave de burnville (pieza 594): el contacto cae fuera de ambos triángulos y solo lo
        // detecta la prueba contra la arista (antes las aristas quedaban sin enlazar y se atravesaba).
        var v = new[] { new Vec3(112, -16112, 16), new Vec3(112, -16072, 20), new Vec3(152, -16112, 16), new Vec3(152, -16072, 16) };
        var map = new ClipMap(Array.Empty<CollisionPlane>(), Array.Empty<CollisionBrush>(),
            new[] { new TerrainCollide(v, new[] { 0, 1, 2, 2, 1, 3 }, ClipMap.ContentsSolid, 0) });
        var tr = map.Trace(new Vec3(x, y, 60), new Vec3(x, y, -300), Mins, Maxs, ClipMap.MaskPlayerSolid);
        Assert.True(tr.Hit);
        Assert.InRange(tr.EndPos.Z, 18.5f, 19.5f);
        Assert.True(tr.Plane.Normal.Z > 0.99f);
    }

    [Theory]
    [InlineData(8f)]
    [InlineData(30f)]
    public void SobreUnaPendienteLimpiaElJugadorNoDespegaNiResbala(float degrees)
    {
        var tan = MathF.Tan(degrees * MathF.PI / 180f);
        Vec3 P(float x, float y) => new(x, y, x * tan);
        var verts = new[] { P(-2048, -2048), P(2048, -2048), P(-2048, 2048), P(2048, 2048) };
        var map = new ClipMap(Array.Empty<CollisionPlane>(), Array.Empty<CollisionBrush>(),
            new[] { new TerrainCollide(verts, new[] { 0, 2, 1, 1, 2, 3 }, ClipMap.ContentsSolid, 0) });
        var pm = new Pmove(new PmoveConfig(), map);
        var ps = new PlayerState { Origin = new Vec3(0, 0, 40), JumpTime = -10000 };
        for (var i = 0; i < 100; i++) pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 0, 0, 0));
        var rest = ps.Origin;
        var air = 0;
        for (var i = 0; i < 300; i++)
        {
            // quieto 1 s, luego cuesta abajo (yaw 180°)
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 32768, 0, i < 125 ? (sbyte)0 : (sbyte)127, 0, 0));
            if (i == 124) Assert.True(MathF.Abs(ps.Origin.X - rest.X) < 0.01f, "resbala estando quieto");
            if (!ps.OnGround) air++;
        }
        Assert.Equal(0, air);
        // rapidez de Quake 3 a lo largo de la pendiente: 190 en total, 190·cos θ en horizontal
        var h = MathF.Sqrt(ps.Velocity.X * ps.Velocity.X + ps.Velocity.Y * ps.Velocity.Y);
        var expected = 190f * MathF.Cos(degrees * MathF.PI / 180f);
        Assert.InRange(h, expected - 1, expected + 1);
    }

    [CorpusFact]
    public void CaminandoPorElTerrenoDeBurnvilleCasiNuncaSePierdeElSuelo()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var map = ClipMap.FromBsp(bsp);
        var pm = new Pmove(new PmoveConfig(), map);
        var rnd = new Random(1);
        int steps = 0, hopSteps = 0;
        foreach (var s in bsp.Surfaces.Where(s => bsp.Materials[s.MaterialIndex].Name.Contains("/ground/")).Take(400))
        {
            var a = bsp.Vertices[(int)s.FirstVertex + bsp.Indices[(int)s.FirstIndex]];
            if (a.Nz < 0.85f) continue;
            var ps = new PlayerState { Origin = new Vec3(a.X, a.Y, a.Z + 40), JumpTime = -10000 };
            for (var i = 0; i < 125; i++) pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 0, 0, 0));
            if (!ps.OnGround) continue;
            var yaw = rnd.Next(65536);
            int streak = 0;
            var takeoffZ = 0f;
            for (var i = 0; i < 125; i++)
            {
                var z = ps.Origin.Z;
                pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, yaw, 0, 127, 0, 0));
                steps++;
                if (!ps.OnGround)
                {
                    if (streak++ == 0) takeoffZ = z;
                }
                else if (streak > 0)
                {
                    // saltito: pocos pasos en el aire y aterriza a la misma altura (no es una caída real)
                    if (streak <= 10 && MathF.Abs(ps.Origin.Z - takeoffZ) < 4) hopSteps += streak;
                    streak = 0;
                }
            }
        }
        // Con las aristas del terreno sin enlazar el jugador perdía el suelo en ~26 % de los pasos y daba
        // saltitos constantes; lo que queda son empujones contra paredes empinadas (normal z < 0.7).
        Assert.True(steps > 5000, $"solo {steps} pasos");
        Assert.True(hopSteps < steps * 0.01, $"saltitos {hopSteps} de {steps}");
    }

    [CorpusFact]
    public void CasiTodoElSueloVisibleDeBurnvilleTieneColision()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var map = ClipMap.FromBsp(bsp);
        Assert.Equal(1112, map.Terrains.Length);
        Assert.Equal(512, map.Curves.Length);

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
        // Sin terreno faltaba el 69 %; sin curvas, el 1.9 %. Lo que queda son superficies decorativas sobre
        // un suelo sólido algo más bajo (p. ej. tablones del granero a 48 con el suelo a 42).
        Assert.True(miss < total * 0.01, $"sin colisión {miss} de {total}");
    }
}
