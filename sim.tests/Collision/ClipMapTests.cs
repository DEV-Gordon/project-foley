using Foley.Sim.Collision;
using Foley.Sim.Math;

namespace Foley.Sim.Tests.Collision;

/// <summary>
/// Trazado contra brushes sintéticos. Los valores esperados salen de aplicar a mano las fórmulas
/// de ioquake3 cm_trace.c (CM_TraceThroughBrush) con SURFACE_CLIP_EPSILON = 0.125.
/// </summary>
public class ClipMapTests
{
    private static ClipMap Map(params (Vec3 min, Vec3 max, int contents, (Vec3 n, float d)[] extra)[] boxes)
    {
        var planes = new List<CollisionPlane>();
        var brushes = new List<CollisionBrush>();
        foreach (var (min, max, contents, extra) in boxes)
        {
            var first = planes.Count;
            for (var axis = 0; axis < 3; axis++)
            {
                planes.Add(CollisionPlane.Create(Vec3.Zero.With(axis, -1f), -min[axis]));
                planes.Add(CollisionPlane.Create(Vec3.Zero.With(axis, 1f), max[axis]));
            }
            foreach (var (n, d) in extra) planes.Add(CollisionPlane.Create(n, d));
            brushes.Add(new CollisionBrush(first, planes.Count - first, contents, min, max));
        }
        return new ClipMap(planes.ToArray(), brushes.ToArray());
    }

    private static readonly (Vec3 n, float d)[] None = Array.Empty<(Vec3, float)>();

    // Suelo: caja de 200x200 con la cara superior en z = 0.
    private static ClipMap Floor(int contents = ClipMap.ContentsSolid) =>
        Map((new Vec3(-100, -100, -16), new Vec3(100, 100, 0), contents, None));

    [Fact]
    public void PuntoCayendoSeDetieneAEpsilonDelSuelo()
    {
        var t = Floor().Trace(new Vec3(0, 0, 50), new Vec3(0, 0, -50), Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        // d1 = 50, d2 = -50: f = (50 - 0.125) / 100 = 0.49875 -> z = 50 - 0.49875 * 100 = 0.125
        Assert.Equal(0.49875f, t.Fraction);
        Assert.Equal(0.125f, t.EndPos.Z, 5);
        Assert.Equal(new Vec3(0, 0, 1), t.Plane.Normal);
        Assert.Equal(0f, t.Plane.Dist);
        Assert.False(t.StartSolid);
    }

    [Fact]
    public void CajaUsaLaEsquinaCorrectaDelPlano()
    {
        // Caja con los pies en el origen (mins.z = 0): debe apoyarse a 0.125 sobre el suelo.
        var t = Floor().Trace(new Vec3(0, 0, 50), new Vec3(0, 0, -50),
            new Vec3(-15, -15, 0), new Vec3(15, 15, 70), ClipMap.MaskPlayerSolid);
        Assert.Equal(0.125f, t.EndPos.Z, 5);
    }

    [Fact]
    public void SinObstaculoRecorreTodo()
    {
        var end = new Vec3(0, 0, 10);
        var t = Floor().Trace(new Vec3(0, 0, 50), end, Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        Assert.Equal(1f, t.Fraction);
        Assert.Equal(end, t.EndPos);
        Assert.False(t.Hit);
    }

    [Fact]
    public void EmpezarDentroEsStartSolidYQuedarseDentroEsAllSolid()
    {
        var map = Floor();
        var inside = map.Trace(new Vec3(0, 0, -8), new Vec3(10, 0, -8), Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        Assert.True(inside.StartSolid);
        Assert.True(inside.AllSolid);
        Assert.Equal(0f, inside.Fraction);

        var leaving = map.Trace(new Vec3(0, 0, -8), new Vec3(0, 0, 40), Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        Assert.True(leaving.StartSolid);
        Assert.False(leaving.AllSolid);
    }

    [Fact]
    public void PruebaDePosicion()
    {
        var map = Floor();
        var box = (min: new Vec3(-15, -15, 0), max: new Vec3(15, 15, 70));
        Assert.True(map.Trace(new Vec3(0, 0, -1), new Vec3(0, 0, -1), box.min, box.max, ClipMap.MaskPlayerSolid).AllSolid);
        Assert.False(map.Trace(new Vec3(0, 0, 1), new Vec3(0, 0, 1), box.min, box.max, ClipMap.MaskPlayerSolid).StartSolid);
    }

    [Fact]
    public void LaMascaraFiltraContenidos()
    {
        // Un brush de solo "clip de IA" (0x20000) no bloquea al jugador.
        var t = Floor(contents: 0x20000).Trace(new Vec3(0, 0, 50), new Vec3(0, 0, -50), Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        Assert.False(t.Hit);
        var clip = Floor(contents: ClipMap.ContentsPlayerClip).Trace(new Vec3(0, 0, 50), new Vec3(0, 0, -50), Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        Assert.True(clip.Hit);
    }

    [Fact]
    public void PlanoInclinadoNoAxial()
    {
        // Rampa: caja recortada por un plano a 45 grados (normal (-0.7071, 0, 0.7071), dist 0).
        var n = new Vec3(-0.70710677f, 0, 0.70710677f);
        var map = Map((new Vec3(0, -100, -100), new Vec3(100, 100, 100), ClipMap.ContentsSolid, new[] { (n, 0f) }));
        var t = map.Trace(new Vec3(50, 0, 200), new Vec3(50, 0, 0), Vec3.Zero, Vec3.Zero, ClipMap.MaskPlayerSolid);
        Assert.True(t.Hit);
        Assert.Equal(n, t.Plane.Normal);
        // La superficie en x = 50 está en z = 50; se detiene 0.125 por delante del plano.
        Assert.InRange(t.EndPos.Z, 50.1f, 50.2f);
    }

    [Fact]
    public void EsDeterminista()
    {
        var map = Floor();
        var a = map.Trace(new Vec3(3.3f, 1.7f, 77.1f), new Vec3(-9.1f, 4.2f, -60.3f), new Vec3(-15, -15, 0), new Vec3(15, 15, 70), ClipMap.MaskPlayerSolid);
        var b = map.Trace(new Vec3(3.3f, 1.7f, 77.1f), new Vec3(-9.1f, 4.2f, -60.3f), new Vec3(-15, -15, 0), new Vec3(15, 15, 70), ClipMap.MaskPlayerSolid);
        Assert.Equal(a, b);
    }
}
