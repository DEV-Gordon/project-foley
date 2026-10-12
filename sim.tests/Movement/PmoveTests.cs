using Foley.Sim.Collision;
using Foley.Sim.Math;
using Foley.Sim.Movement;

namespace Foley.Sim.Tests.Movement;

/// <summary>
/// Pmove de CoD1 contra geometría sintética. Los valores esperados salen de las fórmulas y
/// constantes de gamex86.dll documentadas en docs/re/pmove.md. Validan el port del algoritmo;
/// la comparación con trayectorias reales es aparte (demo).
/// </summary>
public class PmoveTests
{
    private static ClipMap Boxes(params (Vec3 min, Vec3 max)[] boxes)
    {
        var planes = new List<CollisionPlane>();
        var brushes = new List<CollisionBrush>();
        foreach (var (min, max) in boxes)
        {
            var first = planes.Count;
            for (var axis = 0; axis < 3; axis++)
            {
                planes.Add(CollisionPlane.Create(Vec3.Zero.With(axis, -1f), -min[axis]));
                planes.Add(CollisionPlane.Create(Vec3.Zero.With(axis, 1f), max[axis]));
            }
            brushes.Add(new CollisionBrush(first, 6, ClipMap.ContentsSolid, min, max));
        }
        return new ClipMap(planes.ToArray(), brushes.ToArray());
    }

    private static readonly (Vec3, Vec3) Floor = (new Vec3(-4096, -4096, -64), new Vec3(4096, 4096, 0));

    private static readonly PmoveConfig Cfg = new();

    // Con mins.z = 0 (origen en los pies) el origen apoyado queda a SURFACE_CLIP_EPSILON sobre el suelo.
    private const float RestZ = 0.125f;
    private static PlayerState Standing(float x = 0) => new() { Origin = new Vec3(x, 0, RestZ), OnGround = true, JumpTime = -10000 };

    private static void Run(Pmove pm, PlayerState ps, int steps, sbyte forward = 0, sbyte right = 0, sbyte up = 0, int yaw = 0)
    {
        for (var i = 0; i < steps; i++)
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, yaw, 0, forward, right, up));
    }

    [Fact]
    public void QuietoSobreElSueloNoSeMueve()
    {
        var ps = Standing();
        Run(new Pmove(Cfg, Boxes(Floor)), ps, 125);
        Assert.Equal(new Vec3(0, 0, RestZ), ps.Origin);
        Assert.Equal(Vec3.Zero, ps.Velocity);
        Assert.True(ps.OnGround);
    }

    [Fact]
    public void CaminarAlcanzaLaVelocidadMaxima()
    {
        // Régimen: fricción 0.008·5.5·190 = 8.36 por paso < aceleración 190·0.008·9 = 13.68 -> se repone a 190.
        var ps = Standing();
        Run(new Pmove(Cfg, Boxes(Floor)), ps, 250, forward: 127);
        Assert.Equal(Cfg.Speed, ps.Velocity.X, 3);
        Assert.Equal(0f, ps.Velocity.Y, 3);
        Assert.True(ps.OnGround);
        Assert.Equal(RestZ, ps.Origin.Z);
    }

    [Fact]
    public void ElArmaEscalaLaVelocidad()
    {
        // Timedemo1.dm_2: con la Luger (moveSpeedScale 1.2) la velocidad estable es 228 = 190 · 1.2.
        var ps = Standing();
        ps.WeaponMoveSpeedScale = 1.2f;
        Run(new Pmove(Cfg, Boxes(Floor)), ps, 250, forward: 127);
        Assert.Equal(228f, ps.Velocity.X, 2);
    }

    [Fact]
    public void HaciaAtrasYDeLadoEsMasLento()
    {
        // backSpeedScale 0.7 y strafeSpeedScale 0.8 (escalan la velocidad deseada en PM_CmdScale).
        var back = Standing();
        Run(new Pmove(Cfg, Boxes(Floor)), back, 250, forward: -127);
        Assert.Equal(-Cfg.Speed * Cfg.BackSpeedScale, back.Velocity.X, 2);

        var side = Standing();
        Run(new Pmove(Cfg, Boxes(Floor)), side, 250, right: 127);
        Assert.Equal(-Cfg.Speed * Cfg.StrafeSpeedScale, side.Velocity.Y, 2);
    }

    [Fact]
    public void AlSoltarLaFriccionLoDetiene()
    {
        var ps = Standing();
        var pm = new Pmove(Cfg, Boxes(Floor));
        Run(pm, ps, 250, forward: 127);
        Run(pm, ps, 125);
        Assert.Equal(0f, ps.Velocity.X);
        Assert.Equal(0f, ps.Velocity.Y);
    }

    [Fact]
    public void ElSaltoSubeTreintaYNueveUnidades()
    {
        // v0 = sqrt(800 · 78) = 249.8; altura = v0² / 2g = 39. Integración con velocidad media: parábola exacta.
        var ps = Standing();
        var pm = new Pmove(Cfg, Boxes(Floor));
        var apex = ps.Origin.Z;
        var landedAt = -1;
        float v0 = 0;
        for (var i = 0; i < 150; i++)
        {
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 0, 0, i == 0 ? (sbyte)127 : (sbyte)0));
            if (i == 0) v0 = ps.Velocity.Z + Cfg.Gravity * 0.008f;
            apex = MathF.Max(apex, ps.Origin.Z);
            if (i > 0 && ps.OnGround && landedAt < 0) landedAt = i;
        }
        Assert.Equal(MathF.Sqrt(800f * 78f), v0, 2);
        Assert.InRange(apex - RestZ, 38.8f, 39.01f);
        // Tiempo de vuelo 2·v0/g = 0.6245 s = ~78 pasos de 8 ms.
        Assert.InRange(landedAt, 76, 80);
        Assert.Equal(RestZ, ps.Origin.Z, 3);
    }

    [Fact]
    public void NoSePuedeVolverASaltarAntesDe500ms()
    {
        var ps = Standing();
        var pm = new Pmove(Cfg, Boxes(Floor));
        pm.Move(ps, new UserCmd(8, 0, 0, 0, 0, 0, 127));
        var firstJump = ps.JumpTime;
        Assert.Equal(8, firstJump);
        // soltar y volver a pulsar en cuanto toca el suelo: no salta hasta pasados 500 ms del primero
        var jumps = new List<int>();
        for (var i = 0; i < 200; i++)
        {
            var wasOnGround = ps.OnGround;
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 0, 0, (sbyte)(i % 2 == 0 ? 127 : 0)));
            if (wasOnGround && !ps.OnGround && ps.Velocity.Z > 200) jumps.Add(ps.CommandTime);
        }
        Assert.NotEmpty(jumps);
        Assert.True(jumps[0] - firstJump >= Cfg.JumpCooldown, $"saltó a los {jumps[0] - firstJump} ms");
    }

    [Fact]
    public void AlAterrizarDeUnSaltoFrenaAl67PorCiento()
    {
        // PM_CrashLand: altura de caída >= 12 sin daño -> velocidad × 0.67.
        var ps = Standing();
        var pm = new Pmove(Cfg, Boxes(Floor));
        Run(pm, ps, 250, forward: 127);
        float before = 0;
        var wasAir = false;
        for (var i = 0; i < 120; i++)
        {
            var vxBefore = ps.Velocity.X;
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 127, 0, (sbyte)(i == 0 ? 127 : 0)));
            if (wasAir && ps.OnGround) { before = vxBefore; break; }
            wasAir = !ps.OnGround;
        }
        Assert.True(before > 150, $"velocidad antes de aterrizar {before}");
        // tras aterrizar: ×0.67 y luego el resto del paso (fricción/aceleración ya aplicadas en el paso siguiente)
        Assert.InRange(ps.Velocity.X, before * 0.6f, before * 0.75f);
    }

    [Fact]
    public void MantenerElSaltoNoRepiteElSalto()
    {
        var ps = Standing();
        Run(new Pmove(Cfg, Boxes(Floor)), ps, 200, up: 127);
        Assert.True(ps.OnGround);
        Assert.Equal(0f, ps.Velocity.Z);
    }

    [Fact]
    public void SubeEscalonesDeHasta18()
    {
        var ps = Standing();
        Run(new Pmove(Cfg, Boxes(Floor, (new Vec3(64, -512, 0), new Vec3(1024, 512, 16)))), ps, 125, forward: 127);
        Assert.True(ps.Origin.X > 100, $"no avanzó: {ps.Origin}");
        Assert.Equal(16 + RestZ, ps.Origin.Z, 3);
    }

    [Fact]
    public void NoSubeEscalonesMasAltosQueElStepSize()
    {
        var ps = Standing();
        Run(new Pmove(Cfg, Boxes(Floor, (new Vec3(64, -512, 0), new Vec3(1024, 512, 24)))), ps, 125, forward: 127);
        Assert.InRange(ps.Origin.X, 48.8f, 49f);
        Assert.Equal(RestZ, ps.Origin.Z, 3);
    }

    [Fact]
    public void SeDeslizaPorUnaParedAlCaminarEnDiagonal()
    {
        var ps = Standing();
        Run(new Pmove(Cfg, Boxes(Floor, (new Vec3(64, -4096, 0), new Vec3(128, 4096, 512)))), ps, 250, forward: 127, right: -127);
        Assert.InRange(ps.Origin.X, 48.8f, 49f);
        Assert.True(ps.Origin.Y > 200, $"no se deslizó: {ps.Origin}");
    }

    [Fact]
    public void CaidaAltaHaceDanoYAturde()
    {
        // Desde ~300 de altura: daño = (int)((h - 256) / (480 - 256) · 100) ≈ 19; aturdimiento 19·35 + 500 = 1165 ms.
        var ps = new PlayerState { Origin = new Vec3(0, 0, 300) };
        var pm = new Pmove(Cfg, Boxes(Floor));
        for (var i = 0; i < 300 && !ps.OnGround; i++)
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 0, 0, 0));
        Assert.True(ps.OnGround);
        Assert.InRange(ps.LastFallDamage, 18, 20);
        Assert.True((ps.Flags & PmFlags.LandStun) != 0);
        Assert.InRange(ps.PmTime, 1100, 1210);
    }

    [Fact]
    public void CaidaBajaNoHaceDano()
    {
        var ps = new PlayerState { Origin = new Vec3(0, 0, 100) };
        var pm = new Pmove(Cfg, Boxes(Floor));
        for (var i = 0; i < 300 && !ps.OnGround; i++)
            pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, 0, 0, 0, 0, 0));
        Assert.True(ps.OnGround);
        Assert.Equal(0, ps.LastFallDamage);
        Assert.Equal(PmFlags.None, ps.Flags & PmFlags.LandStun);
    }

    [Fact]
    public void ElPitchSeLimitaA16000()
    {
        var ps = new PlayerState();
        Pmove.UpdateViewAngles(ps, new UserCmd(0, 20000, 0, 0, 0, 0, 0));
        Assert.Equal((float)(16000 * (double)0.005493164f), ps.ViewAngles.X);
        Assert.Equal(16000 - 20000, ps.DeltaPitch);
    }

    [Fact]
    public void UnComandoLargoSeTroceaEnPasosDe8ms()
    {
        var a = Standing();
        var b = Standing();
        var pm = new Pmove(Cfg, Boxes(Floor));
        pm.Move(a, new UserCmd(80, 0, 0, 0, 127, 0, 0));
        for (var t = 8; t <= 80; t += 8) pm.Move(b, new UserCmd(t, 0, 0, 0, 127, 0, 0));
        Assert.Equal(b.Origin, a.Origin);
        Assert.Equal(b.Velocity, a.Velocity);
    }

    [Fact]
    public void EsDeterminista()
    {
        PlayerState RunOnce()
        {
            var ps = Standing();
            var pm = new Pmove(Cfg, Boxes(Floor, (new Vec3(64, -512, 0), new Vec3(1024, 512, 16))));
            for (var i = 0; i < 500; i++)
                pm.Move(ps, new UserCmd(ps.CommandTime + 8, 0, (i * 37) & 65535, 0, 127, (sbyte)((i % 3) * 60 - 60), (sbyte)(i % 90 == 0 ? 127 : 0)));
            return ps;
        }
        var a = RunOnce();
        var b = RunOnce();
        Assert.Equal(a.Origin, b.Origin);
        Assert.Equal(a.Velocity, b.Velocity);
    }
}
