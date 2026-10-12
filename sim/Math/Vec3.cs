namespace Foley.Sim.Math;

/// <summary>
/// Vector de 3 floats con operaciones escritas a mano, en el mismo orden que las macros de
/// Quake 3 (<c>DotProduct</c>, <c>VectorMA</c>...), para que la aritmética sea determinista.
/// No se usa System.Numerics: su producto escalar puede vectorizarse y sumar en otro orden.
/// </summary>
public readonly record struct Vec3(float X, float Y, float Z)
{
    public static readonly Vec3 Zero = new(0f, 0f, 0f);

    public float this[int i] => i switch
    {
        0 => X,
        1 => Y,
        2 => Z,
        _ => throw new ArgumentOutOfRangeException(nameof(i)),
    };

    public Vec3 With(int i, float value) => i switch
    {
        0 => this with { X = value },
        1 => this with { Y = value },
        2 => this with { Z = value },
        _ => throw new ArgumentOutOfRangeException(nameof(i)),
    };

    /// <summary>ioquake3 <c>DotProduct</c>: <c>x[0]*y[0]+x[1]*y[1]+x[2]*y[2]</c>.</summary>
    public static float Dot(Vec3 a, Vec3 b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, float s) => new(a.X * s, a.Y * s, a.Z * s);

    /// <summary>ioquake3 <c>VectorMA</c>: <c>v + s * b</c>.</summary>
    public static Vec3 MA(Vec3 v, float s, Vec3 b) => new(v.X + b.X * s, v.Y + b.Y * s, v.Z + b.Z * s);

    public override string ToString() => $"({X}, {Y}, {Z})";
}
