using Foley.Sim.Collision;
using Foley.Sim.Math;
using Foley.Sim.Movement;

namespace Foley.Sim.Tests.Movement;

/// <summary>Noclip (PM_NoclipMove de ioquake3): truco de depuración que vuela sin colisión.</summary>
public class NoclipTests
{
    [Fact]
    public void AtraviesaParedesYNoCae()
    {
        // Pared sólida delante (x 64..96).
        var planes = new[]
        {
            CollisionPlane.Create(new Vec3(-1, 0, 0), -64), CollisionPlane.Create(new Vec3(1, 0, 0), 96),
            CollisionPlane.Create(new Vec3(0, -1, 0), 512), CollisionPlane.Create(new Vec3(0, 1, 0), 512),
            CollisionPlane.Create(new Vec3(0, 0, -1), 512), CollisionPlane.Create(new Vec3(0, 0, 1), 512),
        };
        var map = new ClipMap(planes, new[] { new CollisionBrush(0, 6, ClipMap.ContentsSolid, new Vec3(64, -512, -512), new Vec3(96, 512, 512)) });
        var pm = new Pmove(new PmoveConfig(), map);
        var ps = new PlayerState { Origin = new Vec3(0, 0, 100), Noclip = true };
        for (var i = 0; i < 250; i++) pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 127, 0, 0));
        Assert.True(ps.Origin.X > 200, $"x = {ps.Origin.X}");
        Assert.Equal(100f, ps.Origin.Z, 3);
        Assert.False(ps.OnGround);

        // Con noclip desactivado la misma pared lo detiene (0.8 s: aún cayendo a la altura de la pared).
        var walker = new PlayerState { Origin = new Vec3(0, 0, 100) };
        for (var i = 0; i < 100; i++) pm.Move(walker, new UserCmd(walker.CommandTime + 8, 0, 0, 0, 127, 0, 0));
        Assert.True(walker.Origin.X < 64);
    }
}
