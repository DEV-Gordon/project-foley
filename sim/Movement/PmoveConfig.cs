using Foley.Sim.Collision;
using Foley.Sim.Math;

namespace Foley.Sim.Movement;

/// <summary>
/// Constantes del pmove de CoD1. Las direcciones son de gamex86.dll 1.5 (modo un jugador);
/// ver docs/re/pmove.md. Los valores de <c>ps.*</c> los fija el juego en el estado del jugador
/// y se confirmaron además con Timedemo1.dm_2 (docs/formats/demo.md).
/// </summary>
public sealed record PmoveConfig
{
    /// <summary><c>ps.speed</c> = 190 (Timedemo1.dm_2; gamex86.dll @0x95cd0).</summary>
    public int Speed { get; init; } = 190;

    /// <summary><c>ps.gravity</c> = 800 (Timedemo1.dm_2; gamex86.dll @0x95cc0).</summary>
    public int Gravity { get; init; } = 800;

    /// <summary><c>ps.friction</c> = 1 (Timedemo1.dm_2). En el suelo la aceleración se divide por él.</summary>
    public float PsFriction { get; init; } = 1f;

    // Escalas de ps.* (Timedemo1.dm_2).
    public float WalkSpeedScale { get; init; } = 0.4f;
    public float RunSpeedScale { get; init; } = 1f;
    public float StrafeSpeedScale { get; init; } = 0.8f;
    public float BackSpeedScale { get; init; } = 0.7f;
    public float LeanSpeedScale { get; init; } = 0.4f;

    /// <summary>Aceleración en el suelo de pie (@0x2008b37c). Tumbado 19 (@0x2008b374), agachado 12 (@0x2008b378).</summary>
    public float AccelerateStand { get; init; } = 9f;
    public float AccelerateProne { get; init; } = 19f;
    public float AccelerateCrouch { get; init; } = 12f;
    /// <summary>Aceleración resbaladiza / empujado y en el aire (@0x2008b380).</summary>
    public float AccelerateSlick { get; init; } = 1f;

    /// <summary>Fricción en el suelo (@0x2008b38c) y velocidad mínima de control (@0x2008b35c).</summary>
    public float Friction { get; init; } = 5.5f;
    public float StopSpeed { get; init; } = 100f;

    /// <summary>Altura del salto (@0x2009a440): v = sqrt(gravity · 2 · 39) (@0x2009a444 = 78).</summary>
    public float JumpHeight { get; init; } = 39f;
    /// <summary>Tiempo mínimo entre saltos en ms (PM_CheckJump @0x20003be0).</summary>
    public int JumpCooldown { get; init; } = 500;

    /// <summary>Escalón de pie (@0x2009a30c) y tumbado (@0x2009a308).</summary>
    public float StepSize { get; init; } = 18f;
    public float StepSizeProne { get; init; } = 10f;

    /// <summary>Pendiente máxima caminable (@0x2009a1c4).</summary>
    public float MinWalkNormal { get; init; } = 0.7f;

    /// <summary>bg_fallDamageMinHeight / MaxHeight (Timedemo1.dm_2 systeminfo; cadenas de gamex86.dll).</summary>
    public float FallDamageMinHeight { get; init; } = 256f;
    public float FallDamageMaxHeight { get; init; } = 480f;

    /// <summary>Caja de pie: <c>ps.mins</c>/<c>ps.maxs</c> (Timedemo1.dm_2), origen en los pies.</summary>
    public Vec3 Mins { get; init; } = new(-15f, -15f, 0f);
    public Vec3 Maxs { get; init; } = new(15f, 15f, 70f);

    /// <summary><c>ps.standViewHeight</c> = 60 (Timedemo1.dm_2).</summary>
    public float ViewHeight { get; init; } = 60f;

    /// <summary>Duración fija de cada paso (CLAUDE.md; <c>pmove_msec 8</c> visto en Timedemo1.dm_2).</summary>
    public int MsecPerStep { get; init; } = 8;

    /// <summary>Máscara de colisión del jugador (docs/re/colision.md). TODO(fidelidad).</summary>
    public int TraceMask { get; init; } = ClipMap.MaskPlayerSolid;
}
