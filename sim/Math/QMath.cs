namespace Foley.Sim.Math;

/// <summary>
/// Funciones matemáticas de id Tech 3 con la misma aritmética que el C original.
/// Portado de ioquake3 code/qcommon/q_math.c y q_shared.h.
/// </summary>
public static class QMath
{
    public const int Pitch = 0;
    public const int Yaw = 1;
    public const int Roll = 2;

    /// <summary>q_shared.h <c>SHORT2ANGLE(x) ((x)*(360.0/65536))</c>: se evalúa en double.</summary>
    public static float ShortToAngle(int x) => (float)(x * (360.0 / 65536));

    /// <summary>q_shared.h <c>ANGLE2SHORT(x) ((int)((x)*65536/360) &amp; 65535)</c>.</summary>
    public static int AngleToShort(float x) => (int)(x * 65536 / 360) & 65535;

    /// <summary>q_math.c <c>AngleVectors</c>. El ángulo y sus senos se calculan en double y se guardan en float.</summary>
    public static void AngleVectors(Vec3 angles, out Vec3 forward, out Vec3 right, out Vec3 up)
    {
        var angle = (float)(angles.Y * (System.Math.PI * 2 / 360));
        var sy = (float)System.Math.Sin(angle);
        var cy = (float)System.Math.Cos(angle);
        angle = (float)(angles.X * (System.Math.PI * 2 / 360));
        var sp = (float)System.Math.Sin(angle);
        var cp = (float)System.Math.Cos(angle);
        angle = (float)(angles.Z * (System.Math.PI * 2 / 360));
        var sr = (float)System.Math.Sin(angle);
        var cr = (float)System.Math.Cos(angle);

        forward = new Vec3(cp * cy, cp * sy, -sp);
        right = new Vec3(-1 * sr * sp * cy + -1 * cr * -sy, -1 * sr * sp * sy + -1 * cr * cy, -1 * sr * cp);
        up = new Vec3(cr * sp * cy + -sr * -sy, cr * sp * sy + -sr * cy, cr * cp);
    }

    /// <summary>q_math.c <c>VectorNormalize</c>: normaliza y devuelve la longitud original.</summary>
    public static float Normalize(ref Vec3 v)
    {
        var length = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        if (length != 0f)
        {
            var ilength = 1 / (float)System.Math.Sqrt(length);
            length *= ilength;
            v = new Vec3(v.X * ilength, v.Y * ilength, v.Z * ilength);
        }
        return length;
    }

    /// <summary>q_math.c <c>VectorNormalize2</c>.</summary>
    public static Vec3 Normalized(Vec3 v)
    {
        var length = v.X * v.X + v.Y * v.Y + v.Z * v.Z;
        if (length == 0f) return Vec3.Zero;
        var ilength = 1 / (float)System.Math.Sqrt(length);
        return new Vec3(v.X * ilength, v.Y * ilength, v.Z * ilength);
    }

    /// <summary>q_shared.h <c>VectorLength</c>: <c>(vec_t)sqrt(x*x+y*y+z*z)</c>.</summary>
    public static float Length(Vec3 v) => (float)System.Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);

    /// <summary>q_shared.h <c>CrossProduct</c>.</summary>
    public static Vec3 Cross(Vec3 a, Vec3 b) =>
        new(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);

    /// <summary>
    /// <c>trap_SnapVector</c> (ioquake3 <c>Sys_SnapVector</c>): redondea cada componente al entero
    /// más cercano con el modo por defecto de la FPU (empates al par), como <c>rint</c>.
    /// </summary>
    public static Vec3 SnapVector(Vec3 v) =>
        new(MathF.Round(v.X, MidpointRounding.ToEven), MathF.Round(v.Y, MidpointRounding.ToEven), MathF.Round(v.Z, MidpointRounding.ToEven));
}
