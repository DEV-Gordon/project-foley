using Foley.Sim.Collision;
using Foley.Sim.Math;

namespace Foley.Sim.Movement;

/// <summary>Función de trazado que usa el pmove (equivalente a <c>pm-&gt;trace</c>).</summary>
public delegate TraceResult PmoveTrace(Vec3 start, Vec3 mins, Vec3 maxs, Vec3 end, int mask);

/// <summary>
/// Movimiento del jugador de CoD1 (modo un jugador), escrito a partir del análisis de
/// gamex86.dll 1.5 documentado en docs/re/pmove.md. La estructura general es la de id Tech 3
/// (ioquake3 code/game/bg_pmove.c y bg_slidemove.c), pero las funciones siguen a CoD1:
/// PmoveSingle @0x20007fd0, PM_GroundTrace @0x20004f30, PM_CorrectAllSolid @0x20004dc0,
/// PM_CrashLand @0x200049b0, PM_WalkMove @0x20003f60, PM_AirMove @0x20003df0,
/// PM_Friction @0x200034c0, PM_Accelerate @0x200035e0, PM_CmdScale @0x20003760 (suelo) y
/// @0x200036a0 (aire), PM_CheckJump @0x20003be0, PM_SlideMove @0x20008730,
/// PM_StepSlideMove @0x20008ff0, PM_ClipVelocity @0x20003410, PM_UpdateViewAngles @0x20006fd0.
///
/// Alcance: de pie, sin agua, escaleras, tumbado/agachado, inclinación, armas ni entidades.
///
/// Aritmética: el original usa la FPU x87 (80 bits) y solo redondea a float al guardar. Aquí
/// las expresiones se evalúan en double y se redondean a float en los mismos puntos de guardado:
/// es la aproximación más cercana disponible (docs/re/pmove.md, "Aritmética").
/// </summary>
public sealed class Pmove
{
    private const int MaxClipPlanes = 8;       // PM_SlideMove: corta con 8 planos ("MAX_CLIP_PLANES")
    private const float Overclip = 1.001f;     // @0x2009a3ac
    private const float InvOverclip = 0.99900097f; // @0x2009a550, usado en PM_WalkMove/PM_AirMove

    private readonly PmoveConfig _cfg;
    private readonly PmoveTrace _trace;

    // Variables locales de un paso (pml_t).
    private Vec3 _forward, _right;
    private float _frameTime;
    private int _msec;
    private bool _walking, _groundPlane;
    private TraceResult _groundTrace;
    private Vec3 _previousOrigin, _previousVelocity;

    private PlayerState _ps = null!;
    private UserCmd _cmd;

    public Pmove(PmoveConfig config, PmoveTrace trace)
    {
        _cfg = config;
        _trace = trace;
    }

    public Pmove(PmoveConfig config, ClipMap map)
        : this(config, (s, mn, mx, e, mask) => map.Trace(s, e, mn, mx, mask))
    {
    }

    public PmoveConfig Config => _cfg;

    /// <summary>
    /// Avanza <paramref name="ps"/> hasta <c>cmd.ServerTime</c> en pasos de <see cref="PmoveConfig.MsecPerStep"/>
    /// (equivalente a <c>pmove_fixed 1</c>; docs/re/tick.md).
    /// </summary>
    public void Move(PlayerState ps, UserCmd cmd)
    {
        var finalTime = cmd.ServerTime;
        if (finalTime < ps.CommandTime) return;
        if (finalTime > ps.CommandTime + 1000) ps.CommandTime = finalTime - 1000;

        while (ps.CommandTime != finalTime)
        {
            var msec = finalTime - ps.CommandTime;
            if (msec > _cfg.MsecPerStep) msec = _cfg.MsecPerStep;
            cmd = cmd with { ServerTime = ps.CommandTime + msec };
            MoveSingle(ps, cmd);
            if ((ps.Flags & PmFlags.JumpHeld) != 0) cmd = cmd with { UpMove = 20 };
        }
    }

    /// <summary>PmoveSingle @0x20007fd0, rama de movimiento normal.</summary>
    public void MoveSingle(PlayerState ps, UserCmd cmd)
    {
        _ps = ps;
        _cmd = cmd;
        _walking = _groundPlane = false;
        _groundTrace = default;

        _msec = cmd.ServerTime - ps.CommandTime;
        if (_msec < 1) _msec = 1;
        else if (_msec > 200) _msec = 200;
        ps.CommandTime = cmd.ServerTime;

        _previousOrigin = ps.Origin;
        _previousVelocity = ps.Velocity;
        _frameTime = (float)(_msec * (double)0.001f); // fild msec; fmul float 0.001 (@0x2009a1d8)

        UpdateViewAngles(ps, cmd);
        QMath.AngleVectors(ps.ViewAngles, out _forward, out _right, out _);

        if (ps.Noclip)
        {
            NoclipMove();
            return;
        }

        if (_cmd.UpMove < 10) ps.Flags &= ~PmFlags.JumpHeld;

        ps.ViewHeight = _cfg.ViewHeight; // de pie (PM_CheckDuck de CoD1 sin portar)

        // Orden de PmoveSingle: nivel de agua, postura, suelo, banderas de arma/escalera, temporizador, movimiento.
        GroundTrace();
        DropTimers();

        if (_walking) WalkMove();
        else AirMove();

        GroundTrace();

        // Si el desplazamiento real fue mucho menor que la velocidad (bloqueado), la velocidad pasa a ser
        // el desplazamiento (final de PmoveSingle). Sustituye al trap_SnapVector de Quake 3, que CoD1 no tiene.
        var dx = (double)ps.Origin.X - _previousOrigin.X;
        var dy = (double)ps.Origin.Y - _previousOrigin.Y;
        var dz = (double)ps.Origin.Z - _previousOrigin.Z;
        var v = ps.Velocity;
        var fdx = (float)dx;
        var fdy = (float)dy;
        var fdz = (float)dz;
        if (((double)fdx * fdx + (double)fdy * fdy + (double)fdz * fdz) / ((double)_frameTime * _frameTime)
            < ((double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z) * 0.25)
        {
            var inv = 1.0 / _frameTime;
            ps.Velocity = new Vec3((float)(inv * fdx), (float)(inv * fdy), (float)(inv * fdz));
        }
    }

    /// <summary>
    /// Noclip (pm_type PM_NOCLIP). Portado de ioquake3 code/game/bg_pmove.c, PM_NoclipMove y
    /// PM_CmdScale. Es un truco de depuración, no jugabilidad.
    /// TODO(fidelidad): fricción (6 · 1.5) y aceleración (10) son las de Quake 3; las de CoD1 sin verificar.
    /// </summary>
    private void NoclipMove()
    {
        const float pmFriction = 6f;
        const float pmAccelerate = 10f;
        _ps.ViewHeight = _cfg.ViewHeight;
        _ps.OnGround = false;

        var v = _ps.Velocity;
        var speed = (float)System.Math.Sqrt((double)v.X * v.X + (double)v.Y * v.Y + (double)v.Z * v.Z);
        if (speed < 1f)
        {
            _ps.Velocity = Vec3.Zero;
        }
        else
        {
            var friction = pmFriction * 1.5f; // fricción extra
            var control = speed < _cfg.StopSpeed ? _cfg.StopSpeed : speed;
            var drop = control * friction * _frameTime;
            var newspeed = speed - drop;
            if (newspeed < 0) newspeed = 0;
            _ps.Velocity = v * (newspeed / speed);
        }

        // PM_CmdScale de Quake 3
        float fmove = _cmd.ForwardMove, smove = _cmd.RightMove, umove = _cmd.UpMove;
        var max = System.Math.Max(System.Math.Abs(fmove), System.Math.Max(System.Math.Abs(smove), System.Math.Abs(umove)));
        var scale = 0f;
        if (max > 0)
        {
            var total = (float)System.Math.Sqrt(fmove * fmove + smove * smove + umove * umove);
            scale = _cfg.Speed * max / (127f * total);
        }

        var wishvel = new Vec3(
            _forward.X * fmove + _right.X * smove,
            _forward.Y * fmove + _right.Y * smove,
            _forward.Z * fmove + _right.Z * smove + umove);
        var wishspeed = QMath.Normalize(ref wishvel) * scale;

        // PM_Accelerate de Quake 3 (sin las variantes de CoD1)
        var cur = _ps.Velocity;
        var addspeed = wishspeed - Vec3.Dot(cur, wishvel);
        if (addspeed > 0)
        {
            var accelspeed = pmAccelerate * _frameTime * wishspeed;
            if (accelspeed > addspeed) accelspeed = addspeed;
            _ps.Velocity = Vec3.MA(cur, accelspeed, wishvel);
        }

        _ps.Origin = Vec3.MA(_ps.Origin, _frameTime, _ps.Velocity);
    }

    /// <summary>PM_UpdateViewAngles @0x20006fd0 (parte común, sin escaleras ni tumbado).</summary>
    public static void UpdateViewAngles(PlayerState ps, UserCmd cmd)
    {
        var angles = ps.ViewAngles;
        for (var i = 0; i < 3; i++)
        {
            var temp = (short)(cmd.Angle(i) + ps.DeltaAngle(i));
            if (i == QMath.Pitch)
            {
                if (temp > 16000)
                {
                    ps.SetDeltaAngle(i, 16000 - cmd.Angle(i));
                    temp = 16000;
                }
                else if (temp < -16000)
                {
                    ps.SetDeltaAngle(i, -16000 - cmd.Angle(i));
                    temp = -16000;
                }
            }
            // (float)(int)temp * 0.005493164f
            angles = angles.With(i, (float)((float)temp * (double)0.005493164f));
        }
        ps.ViewAngles = angles;
    }

    private TraceResult Trace(Vec3 start, Vec3 end) => _trace(start, _cfg.Mins, _cfg.Maxs, end, _cfg.TraceMask);

    /// <summary>
    /// PM_ClipVelocity @0x20003410: backoff = dot; si backoff &gt;= 0, backoff /= overbounce; si no, *= overbounce.
    /// El producto escalar suma en orden x·nx + z·nz + y·ny, como el original.
    /// </summary>
    public static Vec3 ClipVelocity(Vec3 input, Vec3 normal, float overbounce)
    {
        double backoff = (double)input.X * normal.X + (double)normal.Z * input.Z + (double)input.Y * normal.Y;
        backoff = backoff >= 0 ? backoff / overbounce : backoff * overbounce;
        var b = (float)backoff;
        return new Vec3(
            (float)(input.X - (double)b * normal.X),
            (float)(input.Y - (double)b * normal.Y),
            (float)(input.Z - (double)b * normal.Z));
    }

    /// <summary>
    /// Recorte en línea de PM_WalkMove/PM_AirMove: multiplica por 0.99900097 (backoff &gt;= 0) o 1.001,
    /// que no es exactamente igual a dividir. <paramref name="dot"/> lo calcula el llamador en su orden.
    /// </summary>
    private static Vec3 ClipInline(Vec3 v, Vec3 n, double dot)
    {
        var b = (float)(dot * (dot >= 0 ? InvOverclip : Overclip));
        return new Vec3((float)(v.X - (double)n.X * b), (float)(v.Y - (double)n.Y * b), (float)(v.Z - (double)n.Z * b));
    }

    /// <summary>PM_Friction @0x200034c0 (sin agua).</summary>
    private void Friction()
    {
        var vel = _ps.Velocity;
        var vz = _walking ? 0f : vel.Z;
        var speed = (float)System.Math.Sqrt((double)vel.X * vel.X + (double)vel.Y * vel.Y + (double)vz * vz);
        if (speed < 1f)
        {
            _ps.Velocity = vel with { X = 0, Y = 0 };
            return;
        }

        double drop = 0;
        if (_walking && (_groundTrace.SurfaceFlags & 2) == 0 && (_ps.Flags & PmFlags.Knockback) == 0)
        {
            double control = speed < _cfg.StopSpeed ? _cfg.StopSpeed : speed;
            if ((_ps.Flags & PmFlags.LandStun) != 0) control *= 0.3f;
            else if ((_ps.Flags & PmFlags.JumpLand) != 0) control += control;
            drop = _frameTime * (double)_cfg.Friction * control;
        }

        var newspeed = (float)(speed - drop);
        if (newspeed < 0) newspeed = 0;
        newspeed = (float)((double)newspeed / speed);
        _ps.Velocity = new Vec3(
            (float)((double)newspeed * vel.X),
            (float)((double)newspeed * vel.Y),
            (float)((double)newspeed * vel.Z));
    }

    /// <summary>
    /// PM_Accelerate @0x200035e0. Diferencias con Quake 3: la velocidad de control es
    /// max(wishspeed, 100) y, en el suelo, se divide por ps.friction.
    /// </summary>
    private void Accelerate(Vec3 wishdir, float wishspeed, float accel)
    {
        var v = _ps.Velocity;
        var addspeed = (float)(wishspeed - ((double)wishdir.X * v.X + (double)v.Y * wishdir.Y + (double)v.Z * wishdir.Z));
        if (addspeed <= 0) return;

        var control = wishspeed < _cfg.StopSpeed ? _cfg.StopSpeed : wishspeed;
        var accelspeed = (float)((double)control * _frameTime * accel);
        if (addspeed < accelspeed) accelspeed = addspeed;
        if (_ps.OnGround) accelspeed = (float)((1.0 / _cfg.PsFriction) * accelspeed);
        if (addspeed < accelspeed) accelspeed = addspeed;

        _ps.Velocity = new Vec3(
            (float)((double)accelspeed * wishdir.X + v.X),
            (float)((double)accelspeed * wishdir.Y + v.Y),
            (float)((double)accelspeed * wishdir.Z + v.Z));
    }

    /// <summary>PM_CmdScale de suelo @0x20003760 (de pie, sin agua; con el moveSpeedScale del arma).</summary>
    private float CmdScaleWalk()
    {
        int fwd = _cmd.ForwardMove;
        int rgt = _cmd.RightMove;
        var f = fwd < 0 ? (float)(fwd * (double)_cfg.BackSpeedScale) : fwd;
        var r = System.Math.Abs((float)(rgt * (double)_cfg.StrafeSpeedScale));
        var max = System.Math.Abs(f) <= r ? r : System.Math.Abs(f);
        if (max == 0) return 0;

        var total = (float)System.Math.Sqrt((float)(rgt * rgt + fwd * fwd));
        double scale = (double)_cfg.Speed * max / (total * 127.0);
        scale = (float)scale;
        scale *= (_ps.Flags & PmFlags.WalkSpeed) != 0 ? _cfg.WalkSpeedScale : _cfg.RunSpeedScale;
        scale = (float)scale;
        // inclinación (leanf != 0) y postura: sin portar en esta versión (de pie, sin inclinarse)
        if (_ps.WeaponMoveSpeedScale > 0) scale = (float)(scale * _ps.WeaponMoveSpeedScale);
        return (float)scale;
    }

    /// <summary>PM_CmdScale de aire @0x200036a0.</summary>
    private float CmdScaleAir()
    {
        int fwd = _cmd.ForwardMove;
        int rgt = _cmd.RightMove;
        int up = _cmd.UpMove;
        var max = System.Math.Abs(fwd);
        if (System.Math.Abs(rgt) > max) max = System.Math.Abs(rgt);
        if (System.Math.Abs(up) > max) max = System.Math.Abs(up);
        if (max == 0) return 0;

        var s = (_ps.Flags & PmFlags.WalkSpeed) != 0 ? _cfg.WalkSpeedScale : _cfg.RunSpeedScale;
        var scale = (double)_cfg.Speed * max / (System.Math.Sqrt(up * up + rgt * rgt + fwd * fwd) * 127.0) * s;
        return (float)scale;
    }

    /// <summary>PM_CheckJump @0x20003be0 (de pie, sin escalera).</summary>
    private bool CheckJump()
    {
        if (_cmd.ServerTime - _ps.JumpTime < _cfg.JumpCooldown) return false;
        if ((_ps.Flags & (PmFlags.NoJump | PmFlags.JumpLand)) != 0) return false;
        if (_cmd.UpMove < 10) return false;
        // solo de pie (la función de postura @0x20003460 debe devolver 0)

        if ((_ps.Flags & PmFlags.JumpHeld) != 0)
        {
            _cmd = _cmd with { UpMove = 0 };
            return false;
        }

        _groundPlane = false;
        _walking = false;
        _ps.Flags |= PmFlags.JumpHeld;
        _ps.OnGround = false;
        // fild gravity; fmul 78.0 (@0x2009a444); fsqrt
        var vz = (float)System.Math.Sqrt((float)(_cfg.Gravity * (double)(2f * _cfg.JumpHeight)));
        _ps.Velocity = _ps.Velocity with { Z = vz };
        _ps.JumpPeak = (float)(_ps.Origin.Z + (double)_cfg.JumpHeight);
        return true;
    }

    /// <summary>PM_AirMove @0x20003df0.</summary>
    private void AirMove()
    {
        Friction();
        var scale = CmdScaleAir();

        var forward = _forward with { Z = 0 };
        var right = _right with { Z = 0 };
        QMath.Normalize(ref forward);
        QMath.Normalize(ref right);

        float fmove = _cmd.ForwardMove;
        float smove = _cmd.RightMove;
        var wishdir = new Vec3(
            (float)((double)right.X * smove + (double)forward.X * fmove),
            (float)((double)right.Y * smove + (double)forward.Y * fmove),
            0);
        var wishspeed = QMath.Normalize(ref wishdir);

        Accelerate(wishdir, (float)((double)wishspeed * scale), _cfg.AccelerateSlick);

        if (_groundPlane)
        {
            var n = _groundTrace.Plane.Normal;
            var v = _ps.Velocity;
            var dot = (double)n.X * v.X + (double)n.Z * v.Z + (double)n.Y * v.Y;
            _ps.Velocity = ClipInline(v, n, dot);
        }

        StepSlideMove(gravity: true);
    }

    /// <summary>PM_WalkMove @0x20003f60 (de pie).</summary>
    private void WalkMove()
    {
        if (CheckJump())
        {
            AirMove();
            _ps.JumpTime = _cmd.ServerTime;
            return;
        }

        Friction();
        var scale = CmdScaleWalk();

        float fmove = _cmd.ForwardMove;
        float smove = _cmd.RightMove;
        var n = _groundTrace.Plane.Normal;

        // proyectar adelante y derecha (con z = 0) sobre el plano del suelo
        var f = _forward with { Z = 0 };
        var dotF = (double)n.X * f.X + (double)n.Y * f.Y + (double)n.Z * 0f;
        var forward = ClipInline(f, n, dotF);
        var r = _right with { Z = 0 };
        var dotR = (double)r.Y * n.Y + (double)r.X * n.X + (double)n.Z * 0f;
        var right = ClipInline(r, n, dotR);
        QMath.Normalize(ref forward);
        QMath.Normalize(ref right);

        var wishdir = new Vec3(
            (float)((double)right.X * smove + (double)forward.X * fmove),
            (float)((double)right.Y * smove + (double)forward.Y * fmove),
            (float)((double)right.Z * smove + (double)forward.Z * fmove));
        var wishspeed = QMath.Normalize(ref wishdir);
        var wish = (float)((double)wishspeed * scale);

        var slick = (_groundTrace.SurfaceFlags & 2) != 0 || (_ps.Flags & PmFlags.Knockback) != 0;
        var accel = slick ? _cfg.AccelerateSlick : _cfg.AccelerateStand;
        if ((_ps.Flags & PmFlags.LandStun) != 0) accel = (float)(accel * 0.25);
        Accelerate(wishdir, wish, accel);

        if (slick)
            _ps.Velocity = _ps.Velocity with { Z = (float)(_ps.Velocity.Z - _cfg.Gravity * (double)_frameTime) };

        var old = _ps.Velocity;
        var vel = (float)System.Math.Sqrt((double)old.X * old.X + (double)old.Y * old.Y + (double)old.Z * old.Z);

        // deslizarse por el plano del suelo
        var dot = (double)n.Z * old.Z + (double)n.Y * old.Y + (double)n.X * old.X;
        _ps.Velocity = ClipInline(old, n, dot);

        // conservar la rapidez solo si no se invirtió la dirección (Quake 3 lo hace siempre)
        var nv = _ps.Velocity;
        if ((double)old.Z * nv.Z + (double)old.Y * nv.Y + (double)old.X * nv.X > 0)
        {
            QMath.Normalize(ref nv);
            _ps.Velocity = new Vec3((float)((double)vel * nv.X), (float)((double)vel * nv.Y), (float)((double)vel * nv.Z));
        }

        if (_ps.Velocity.X != 0 || _ps.Velocity.Y != 0) StepSlideMove(gravity: false);
    }

    /// <summary>PM_CorrectAllSolid @0x20004dc0: prueba 26 desplazamientos y mueve al jugador al primero libre.</summary>
    private static readonly Vec3[] JitterOffsets =
    {
        new(0, 0, 1), new(-1, 0, 1), new(0, -1, 1), new(1, 0, 1), new(0, 1, 1),
        new(-1, 0, 0), new(0, -1, 0), new(1, 0, 0), new(0, 1, 0),
        new(0, 0, -1), new(-1, 0, -1), new(0, -1, -1), new(1, 0, -1), new(0, 1, -1),
        new(-1, -1, 1), new(1, -1, 1), new(1, 1, 1), new(-1, 1, 1),
        new(-1, -1, 0), new(1, -1, 0), new(1, 1, 0), new(-1, 1, 0),
        new(-1, -1, -1), new(1, -1, -1), new(1, 1, -1), new(-1, 1, -1),
    };

    private bool CorrectAllSolid(ref TraceResult trace)
    {
        foreach (var off in JitterOffsets)
        {
            var point = new Vec3(off.X + _ps.Origin.X, off.Y + _ps.Origin.Y, off.Z + _ps.Origin.Z);
            trace = Trace(point, point);
            if (!trace.StartSolid)
            {
                _ps.Origin = point;
                var down = point with { Z = point.Z - 1f };
                trace = Trace(point, down);
                _ps.Origin = trace.EndPos;
                return true;
            }
        }
        _ps.OnGround = false;
        _groundPlane = false;
        _walking = false;
        _ps.JumpPeak = 0;
        return false;
    }

    /// <summary>
    /// PM_GroundTrace @0x20004f30. Traza desde 0.25 por encima del origen hasta 0.25 por debajo
    /// (Quake 3 empieza en el origen); si empieza en sólido reintenta desde 0.001 por debajo.
    /// </summary>
    private void GroundTrace()
    {
        var start = _ps.Origin with { Z = (float)(_ps.Origin.Z + 0.25) };
        var end = _ps.Origin with { Z = (float)(_ps.Origin.Z - 0.25) };
        var trace = Trace(start, end);
        _groundTrace = trace;

        if (trace.AllSolid && !CorrectAllSolid(ref trace)) return;

        if (trace.StartSolid)
        {
            start = _ps.Origin with { Z = (float)(_ps.Origin.Z - 0.001) };
            end = _ps.Origin with { Z = (float)(_ps.Origin.Z - 0.25) };
            trace = Trace(start, end);
            if (trace.StartSolid)
            {
                SetAirborne();
                return;
            }
            _groundTrace = trace;
        }

        if (trace.Fraction == 1f)
        {
            SetAirborne();
            return;
        }

        _ps.JumpPeak = 0;

        var n = trace.Plane.Normal;
        var v = _ps.Velocity;
        if ((_ps.Flags & PmFlags.Ladder) == 0 && v.Z > 0 &&
            (double)n.Z * v.Z + (double)n.X * v.X + (double)n.Y * v.Y > 10)
        {
            SetAirborne(); // "kickoff"
            return;
        }

        if (n.Z < _cfg.MinWalkNormal)
        {
            // "steep": hay plano de suelo pero no se camina
            _ps.OnGround = false;
            _walking = false;
            _groundPlane = true;
            return;
        }

        _groundPlane = true;
        _walking = true;
        if (!_ps.OnGround) CrashLand(); // "Land"
        _ps.OnGround = true;
    }

    private void SetAirborne()
    {
        _ps.OnGround = false;
        _groundPlane = false;
        _walking = false;
    }

    /// <summary>
    /// PM_CrashLand @0x200049b0: altura de caída a partir de la posición y velocidad del inicio del
    /// paso; frena al aterrizar y calcula el daño por caída (lo aplica el juego, no el pmove).
    /// </summary>
    private void CrashLand()
    {
        float g = _cfg.Gravity;
        var a = (float)(-(double)g * 0.5);                    // fVar4
        var vz0 = _previousVelocity.Z;
        var disc = (float)((double)vz0 * vz0 - ((double)_previousOrigin.Z - _ps.Origin.Z) * a * 4.0);
        if (disc < 0) return;

        var t = (float)((-(double)vz0 - System.Math.Sqrt(disc)) / ((double)a + a));
        var vland = (float)((t * -(double)g + vz0) * -1.0);
        var height = (float)((double)vland * vland / ((double)g + g));

        var min = _cfg.FallDamageMinHeight;
        var max = _cfg.FallDamageMaxHeight;
        var damage = 0;
        if (height > min && (_groundTrace.SurfaceFlags & 1) == 0)
        {
            damage = height >= max ? 100 : (int)((height - (double)min) / ((double)max - min) * 100.0);
            if (damage < 0) damage = 0;
            else if (damage > 100) damage = 100;
        }
        _ps.LastFallDamage = damage;

        if (_ps.JumpPeak < -0.001f || _ps.JumpPeak > 0.001f)
        {
            _ps.PmTime = 200;
            _ps.Flags |= PmFlags.JumpLand;
        }

        var v = _ps.Velocity;
        if (damage == 0)
        {
            if (height > 4f && height >= 12f)
                _ps.Velocity = new Vec3((float)(v.X * 0.67), (float)(v.Y * 0.67), (float)(v.Z * 0.67));
            return;
        }

        if (damage < 100 && (_groundTrace.SurfaceFlags & 2) == 0)
        {
            var stun = damage * 35 + 500;
            double mult;
            if (stun > 2000) { stun = 2000; mult = 0.2f; }
            else if (stun < 501) mult = 0.5f;
            else if (stun < 1500) mult = 0.5f - (stun - 500.0) * 0.001f * 0.3f;
            else mult = 0.2f;
            var m = (float)mult;
            _ps.PmTime = stun;
            _ps.Flags |= PmFlags.LandStun;
            _ps.Velocity = new Vec3((float)((double)m * v.X), (float)((double)m * v.Y), (float)((double)m * v.Z));
        }
        else
        {
            _ps.Velocity = new Vec3((float)(v.X * 0.67), (float)(v.Y * 0.67), (float)(v.Z * 0.67));
        }
    }

    /// <summary>Temporizador pm_time de PmoveSingle: al vencer limpia las banderas temporales.</summary>
    private void DropTimers()
    {
        if (_ps.PmTime == 0) return;
        if (_msec < _ps.PmTime)
        {
            _ps.PmTime -= _msec;
        }
        else
        {
            _ps.Flags &= ~PmFlags.AllTimes;
            _ps.PmTime = 0;
        }
    }

    /// <summary>PM_SlideMove @0x20008730. Devuelve true si chocó con algo.</summary>
    private bool SlideMove(bool gravity)
    {
        Span<Vec3> planes = stackalloc Vec3[MaxClipPlanes + 1];
        var primal = _ps.Velocity;
        var endVelocity = _ps.Velocity;

        if (gravity)
        {
            var endZ = (float)(_ps.Velocity.Z - (float)_cfg.Gravity * (double)_frameTime);
            endVelocity = _ps.Velocity with { Z = endZ };
            _ps.Velocity = _ps.Velocity with { Z = (float)(((double)endZ + _ps.Velocity.Z) * 0.5) };
            primal = primal with { Z = endZ };
            if (_groundPlane) _ps.Velocity = ClipVelocity(_ps.Velocity, _groundTrace.Plane.Normal, Overclip);
        }

        var numplanes = 0;
        if (_groundPlane) planes[numplanes++] = _groundTrace.Plane.Normal;
        planes[numplanes++] = QMath.Normalized(_ps.Velocity);

        var timeLeft = _frameTime;
        int bump;
        for (bump = 0; bump < 4; bump++)
        {
            var end = new Vec3(
                (float)((double)timeLeft * _ps.Velocity.X + _ps.Origin.X),
                (float)((double)timeLeft * _ps.Velocity.Y + _ps.Origin.Y),
                (float)((double)timeLeft * _ps.Velocity.Z + _ps.Origin.Z));
            var trace = Trace(_ps.Origin, end);

            if (trace.AllSolid)
            {
                _ps.Velocity = _ps.Velocity with { Z = 0 };
                return true;
            }
            if (trace.Fraction > 0) _ps.Origin = trace.EndPos;
            if (trace.Fraction == 1f) break;

            timeLeft = (float)(timeLeft - (double)trace.Fraction * timeLeft);

            if (numplanes >= MaxClipPlanes)
            {
                _ps.Velocity = Vec3.Zero;
                return true;
            }

            // mismo plano que antes: empujar la velocidad hacia fuera
            var n = trace.Plane.Normal;
            var same = false;
            for (var i = 0; i < numplanes; i++)
            {
                var p = planes[i];
                if ((double)n.Z * p.Z + (double)n.X * p.X + (double)n.Y * p.Y > 0.999f)
                {
                    _ps.Velocity = new Vec3(n.X + _ps.Velocity.X, n.Y + _ps.Velocity.Y, n.Z + _ps.Velocity.Z);
                    same = true;
                    break;
                }
            }
            if (same) continue;

            planes[numplanes++] = n;

            for (var i = 0; i < numplanes; i++)
            {
                var pi = planes[i];
                var v = _ps.Velocity;
                var into = (float)((double)pi.X * v.X + (double)pi.Z * v.Z + (double)pi.Y * v.Y);
                if (into >= 0.1) continue;

                var clip = ClipVelocity(v, pi, Overclip);
                var endClip = ClipVelocity(endVelocity, pi, Overclip);

                for (var j = 0; j < numplanes; j++)
                {
                    if (j == i) continue;
                    var pj = planes[j];
                    if ((double)clip.Y * pj.Y + (double)clip.X * pj.X + (double)clip.Z * pj.Z >= 0.1) continue;

                    clip = ClipVelocity(clip, pj, Overclip);
                    endClip = ClipVelocity(endClip, pj, Overclip);

                    if ((double)clip.Y * pi.Y + (double)clip.Z * pi.Z + (double)clip.X * pi.X >= 0) continue;

                    // deslizar la velocidad original por la arista
                    var dir = new Vec3(
                        (float)((double)pi.Y * pj.Z - (double)pj.Y * pi.Z),
                        (float)((double)pj.X * pi.Z - (double)pi.X * pj.Z),
                        (float)((double)pj.Y * pi.X - (double)pj.X * pi.Y));
                    QMath.Normalize(ref dir);
                    var d = (float)((double)dir.Z * v.Z + (double)dir.Y * v.Y + (double)dir.X * v.X);
                    clip = new Vec3((float)((double)dir.X * d), (float)((double)dir.Y * d), (float)((double)dir.Z * d));
                    var de = (float)((double)dir.X * endVelocity.X + (double)dir.Y * endVelocity.Y + (double)dir.Z * endVelocity.Z);
                    endClip = new Vec3((float)((double)dir.X * de), (float)((double)dir.Y * de), (float)((double)dir.Z * de));

                    // un tercer plano: detenerse en seco
                    for (var k = 0; k < numplanes; k++)
                    {
                        if (k == i || k == j) continue;
                        var pk = planes[k];
                        if ((double)clip.Y * pk.Y + (double)clip.X * pk.X + (double)clip.Z * pk.Z < 0.1)
                        {
                            _ps.Velocity = Vec3.Zero;
                            return true;
                        }
                    }
                }

                _ps.Velocity = clip;
                endVelocity = endClip;
                break;
            }
        }

        if (gravity) _ps.Velocity = endVelocity;
        if (_ps.PmTime != 0) _ps.Velocity = primal;
        return bump != 0;
    }

    /// <summary>
    /// PM_StepSlideMove @0x20008ff0. Distinto de Quake 3: subida con altura limitada por la cima
    /// del salto, bajada pegada al suelo (medio escalón), descarte del escalón si no avanza más,
    /// reajuste de la velocidad del salto y frenado proporcional a lo subido.
    /// </summary>
    private void StepSlideMove(bool gravity)
    {
        var stepSize = 0f;                                  // local_88
        var jumpStep = false;                               // bVar3
        var downStep = _groundPlane && (_ps.Flags & PmFlags.Ladder) == 0; // bVar2

        var startO = _ps.Origin;                            // local_80..78
        var startV = _ps.Velocity;                          // local_50..48
        var moved = SlideMove(gravity);

        var ss = (_ps.Flags & PmFlags.Prone) != 0 ? _cfg.StepSizeProne : _cfg.StepSize; // local_8c

        if (!moved || (_ps.JumpPeak >= -0.001f && _ps.JumpPeak <= 0.001f))
        {
            if (!_ps.OnGround)
            {
                if ((_ps.Flags & PmFlags.Ladder) == 0) return;
                if (_ps.Velocity.Z <= 0) return;
            }
        }
        else if (!_ps.OnGround)
        {
            if (_ps.JumpPeak <= startO.Z)
            {
                if ((_ps.Flags & PmFlags.Ladder) == 0) return;
                if (_ps.Velocity.Z <= 0) return;
            }
            else
            {
                ss = _cfg.StepSize;
                if (_ps.JumpPeak < startO.Z + _cfg.StepSize)
                {
                    ss = (float)((double)_ps.JumpPeak - startO.Z);
                    if (ss < 1f) return;
                }
                jumpStep = true;
            }
        }

        var afterO = _ps.Origin;                            // local_74..6c
        var afterV = _ps.Velocity;                          // local_5c..54
        var deltaX = (float)((double)afterO.X - startO.X);  // local_38
        var deltaY = (float)((double)afterO.Y - startO.Y);  // local_34

        if (moved)
        {
            var up = startO with { Z = (float)((double)ss + startO.Z + 1.0) };
            var t = Trace(startO, up);
            stepSize = (float)(((double)ss + 1.0) * t.Fraction - 1.0);
            if (stepSize >= 1f)
            {
                _ps.Origin = new Vec3(startO.X, startO.Y, (float)((double)startO.Z + stepSize));
                _ps.Velocity = startV;
                SlideMove(gravity);
            }
            else
            {
                stepSize = 0;
            }
        }

        if (downStep || stepSize != 0)
        {
            var downZ = (float)((double)_ps.Origin.Z - stepSize);
            if (downStep) downZ = (float)(downZ - (double)ss * 0.5);
            var t = Trace(_ps.Origin, _ps.Origin with { Z = downZ });
            // (si toca una entidad < 4 se descarta el escalón; sin entidades en esta versión)
            if (t.Fraction >= 1f)
            {
                if (stepSize != 0) _ps.Origin = _ps.Origin with { Z = (float)((double)_ps.Origin.Z - stepSize) };
            }
            else
            {
                _ps.Origin = t.EndPos;
                _ps.Velocity = ClipVelocity(_ps.Velocity, t.Plane.Normal, Overclip);
            }
        }

        // ¿avanzó más con el escalón que sin él?
        var v = _ps.Velocity;
        var gained = ((double)_ps.Origin.Y - startO.Y) * v.Y + ((double)_ps.Origin.X - startO.X) * v.X;
        var plain = (double)deltaY * v.Y + (double)deltaX * v.X + 0.001f;
        var useStep = gained > plain;
        if (useStep && jumpStep && _ps.JumpPeak <= _ps.Origin.Z) useStep = false;

        if (!useStep)
        {
            _ps.Origin = afterO;
            _ps.Velocity = afterV;
            if (downStep)
            {
                var t = Trace(_ps.Origin, _ps.Origin with { Z = (float)((double)_ps.Origin.Z - (double)ss * 0.5) });
                if (t.Fraction < 1f)
                {
                    _ps.Origin = t.EndPos;
                    _ps.Velocity = ClipVelocity(_ps.Velocity, t.Plane.Normal, Overclip);
                }
            }
        }
        else if (!jumpStep)
        {
            ApplyStepSlowdown(startO.Z, afterO.Z, ss);
            return;
        }

        if (jumpStep && (float)((double)_ps.Origin.Z - afterO.Z) > 0)
        {
            var h = (float)((double)_ps.JumpPeak - _ps.Origin.Z);
            if (h >= 0.1f)
            {
                var jv = (float)System.Math.Sqrt((double)(float)((double)h + h) * _cfg.Gravity);
                if (jv < _ps.Velocity.Z) _ps.Velocity = _ps.Velocity with { Z = jv };
            }
            else
            {
                _ps.Velocity = _ps.Velocity with { Z = 0 };
            }
        }

        ApplyStepSlowdown(startO.Z, afterO.Z, ss);
    }

    /// <summary>
    /// Final de PM_StepSlideMove: si el escalón cambió la altura más de 0.5 (de pie, no tumbado),
    /// la velocidad se multiplica por (1 - |Δz desde el inicio| / escalón) · 0.8 + 0.2.
    /// </summary>
    private void ApplyStepSlowdown(float startZ, float afterSlideZ, float ss)
    {
        var dz = (float)((double)_ps.Origin.Z - afterSlideZ);
        if (!(System.Math.Abs(dz) > 0.5)) return;
        // FUN_20008650: redondeo al entero más cercano; con |dz| > 0.5 nunca da 0
        var scale = (float)((1.0 - System.Math.Abs((double)_ps.Origin.Z - startZ) / ss) * 0.8 + 0.19999999f);
        var v = _ps.Velocity;
        _ps.Velocity = new Vec3((float)((double)scale * v.X), (float)((double)scale * v.Y), (float)((double)scale * v.Z));
    }
}
