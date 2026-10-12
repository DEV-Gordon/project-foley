using Foley.Sim.Math;

namespace Foley.Sim.Movement;

/// <summary>Comando de usuario (subconjunto de <c>usercmd_t</c>).</summary>
public readonly record struct UserCmd(
    int ServerTime,
    int AnglePitch, int AngleYaw, int AngleRoll,   // ángulos en "shorts" (ANGLE2SHORT)
    sbyte ForwardMove, sbyte RightMove, sbyte UpMove)
{
    public int Angle(int i) => i switch
    {
        QMath.Pitch => AnglePitch,
        QMath.Yaw => AngleYaw,
        QMath.Roll => AngleRoll,
        _ => throw new ArgumentOutOfRangeException(nameof(i)),
    };
}

/// <summary>
/// Banderas <c>pm_flags</c> de CoD1 (modo un jugador) que usa el pmove. Los valores salen de
/// gamex86.dll 1.5; los nombres son deducidos de su uso (docs/re/pmove.md).
/// </summary>
[Flags]
public enum PmFlags
{
    None = 0,
    Prone = 0x1,          // aceleración 19, escalón 10
    Ducked = 0x2,         // aceleración 12
    JumpHeld = 0x8,       // puesto al saltar, quitado con upmove < 10
    Ladder = 0x10,        // en escalera
    WalkSpeed = 0x80,     // CmdScale usa walkSpeedScale en vez de runSpeedScale
    LandStun = 0x100,     // aterrizaje con daño: fricción ×0.3, aceleración ×0.25
    Knockback = 0x200,    // aceleración 1, sin fricción, con gravedad en el suelo
    NoJump = 0x800,       // impide saltar (origen sin determinar)
    JumpLand = 0x2000,    // tras aterrizar de un salto (200 ms): fricción ×2, sin saltar
    /// <summary>Banderas que se limpian al terminar pm_time (máscara 0xfffedcff en PmoveSingle).</summary>
    AllTimes = 0x100 | 0x200 | 0x1000 | 0x10000,
}

/// <summary>Estado del jugador que lee y escribe el pmove (subconjunto de <c>playerState_t</c> de CoD1).</summary>
public sealed class PlayerState
{
    public int CommandTime;
    public Vec3 Origin;
    public Vec3 Velocity;
    public PmFlags Flags;
    public int PmTime;
    public bool OnGround;          // groundEntityNum != ENTITYNUM_NONE (0x7ff)
    public int DeltaPitch, DeltaYaw, DeltaRoll;
    public Vec3 ViewAngles;
    public float ViewHeight;
    public int JumpTime;           // ps.jumpTime (0x64): hora del último salto
    public float JumpPeak;         // ps.fJumpPeak (0x68): altura máxima del salto en curso, 0 si no salta
    public int LastFallDamage;     // daño por caída del último aterrizaje (lo aplica el juego, no el pmove)
    /// <summary>
    /// <c>moveSpeedScale</c> del arma en la mano (archivo weapons/...), 0 = sin arma. PM_CmdScale lo aplica
    /// en el suelo si es &gt; 0. Confirmado con Timedemo1.dm_2: Luger 1.2 -> 228 u/s.
    /// </summary>
    public float WeaponMoveSpeedScale;

    public int DeltaAngle(int i) => i switch
    {
        QMath.Pitch => DeltaPitch,
        QMath.Yaw => DeltaYaw,
        _ => DeltaRoll,
    };

    public void SetDeltaAngle(int i, int value)
    {
        switch (i)
        {
            case QMath.Pitch: DeltaPitch = value; break;
            case QMath.Yaw: DeltaYaw = value; break;
            default: DeltaRoll = value; break;
        }
    }

    public PlayerState Clone() => (PlayerState)MemberwiseClone();
}
