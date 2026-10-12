namespace Foley.Formats.XModel;

/// <summary>Hueso con su pose local (relativa al padre) y la de reposo en el espacio del modelo.</summary>
public sealed record XModelBone(
    string Name,
    int Parent,               // -1 en los huesos absolutos
    float[] LocalOffset,
    float[] LocalQuat,        // x, y, z, w
    float[] Position,         // pose de reposo, espacio del modelo
    float[] Rotation);        // cuaternión x, y, z, w

/// <summary>Archivo <c>xmodelparts/&lt;lod&gt;</c> (versión 14). Ver docs/formats/xmodel.md.</summary>
public sealed class XModelParts
{
    public XModelBone[] Bones { get; }
    public byte[] PartClassification { get; }

    private XModelParts(XModelBone[] bones, byte[] classification)
    {
        Bones = bones;
        PartClassification = classification;
    }

    public static XModelParts Read(ReadOnlySpan<byte> data)
    {
        var r = new XReader(data);
        var version = r.U16();
        if (version != XModelFile.Version)
            throw new InvalidDataException($"xmodelparts: versión {version}, se esperaba {XModelFile.Version}.");
        var relCount = r.U16();
        var absCount = r.U16();
        var total = relCount + absCount;

        var parent = new int[total];
        var offset = new float[total][];
        var quat = new float[total][];
        var pos = new float[total][];
        var rot = new float[total][];
        for (var i = 0; i < absCount; i++)
        {
            parent[i] = -1;
            offset[i] = pos[i] = new float[3];
            quat[i] = rot[i] = new float[] { 0, 0, 0, 1 };
        }
        for (var i = absCount; i < total; i++)
        {
            var p = r.I8();
            if (p < 0 || p >= i)
                throw new InvalidDataException($"xmodelparts: hueso {i} con padre inválido {p}.");
            parent[i] = p;
            offset[i] = new[] { r.F32(), r.F32(), r.F32() };
            float qx = r.I16() / 32767f, qy = r.I16() / 32767f, qz = r.I16() / 32767f;
            var ww = 1f - qx * qx - qy * qy - qz * qz;
            quat[i] = new[] { qx, qy, qz, ww > 0 ? MathF.Sqrt(ww) : 0f };

            // Pose de reposo: pos = pos_padre + rot_padre · desplazamiento; rot = rot_padre · rot_local.
            var rp = rot[p];
            var o = Quat.Rotate(rp, offset[i]);
            pos[i] = new[] { pos[p][0] + o[0], pos[p][1] + o[1], pos[p][2] + o[2] };
            rot[i] = Quat.Multiply(rp, quat[i]);
        }

        var bones = new XModelBone[total];
        for (var i = 0; i < total; i++)
        {
            var name = r.CString();
            r.Skip(24); // límites del hueso
            bones[i] = new XModelBone(name, parent[i], offset[i], quat[i], pos[i], rot[i]);
        }
        var classification = new byte[total];
        for (var i = 0; i < total; i++) classification[i] = r.U8();
        r.ExpectEnd("xmodelparts");
        return new XModelParts(bones, classification);
    }
}

/// <summary>Cuaterniones (x, y, z, w) en float, solo para la pose de presentación.</summary>
public static class Quat
{
    public static float[] Multiply(float[] a, float[] b) => new[]
    {
        a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
        a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
        a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
        a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
    };

    public static float[] Rotate(float[] q, float[] v) => Rotate(q, v[0], v[1], v[2]);

    public static float[] Rotate(float[] q, float vx, float vy, float vz)
    {
        float x = q[0], y = q[1], z = q[2], w = q[3];
        var tx = 2 * (y * vz - z * vy);
        var ty = 2 * (z * vx - x * vz);
        var tz = 2 * (x * vy - y * vx);
        return new[]
        {
            vx + w * tx + y * tz - z * ty,
            vy + w * ty + z * tx - x * tz,
            vz + w * tz + x * ty - y * tx,
        };
    }
}
