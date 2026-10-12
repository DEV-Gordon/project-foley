using Foley.Formats.Pk3;

namespace Foley.Formats.XModel;

/// <summary>Una superficie del modelo en la pose de reposo, en el espacio del modelo (unidades y ejes originales).</summary>
public sealed record XModelMeshPart(string Material, float[] Positions, float[] Normals, float[] UVs, int[] Triangles);

/// <summary>Un LOD de un xmodel listo para dibujar: superficies en la pose de reposo.</summary>
public sealed class XModelMesh
{
    public string Name { get; }
    public XModelFile Model { get; }
    public XModelParts Parts { get; }
    public XModelMeshPart[] Surfaces { get; }

    private XModelMesh(string name, XModelFile model, XModelParts parts, XModelMeshPart[] surfaces)
    {
        Name = name;
        Model = model;
        Parts = parts;
        Surfaces = surfaces;
    }

    /// <summary>Carga <c>xmodel/&lt;nombre&gt;</c> y el LOD pedido (0 = el más detallado).</summary>
    public static XModelMesh Load(Pk3Collection pk3, string modelPath, int lod = 0)
    {
        var model = XModelFile.Read(pk3.ReadAllBytes(modelPath));
        if (model.Lods.Length == 0) throw new InvalidDataException($"{modelPath}: sin LODs.");
        var l = model.Lods[Math.Clamp(lod, 0, model.Lods.Length - 1)];
        // Los huesos se leen solo del LOD 0 y los comparten todos los LODs (cargador @0x4802f0).
        var parts = XModelParts.Read(pk3.ReadAllBytes("xmodelparts/" + model.Lods[0].Name));
        var surfs = XModelSurfs.Read(pk3.ReadAllBytes("xmodelsurfs/" + l.Name));
        if (surfs.Surfaces.Length != l.Materials.Length)
            throw new InvalidDataException($"{modelPath}: {surfs.Surfaces.Length} superficies y {l.Materials.Length} materiales.");
        var result = new XModelMeshPart[surfs.Surfaces.Length];
        for (var i = 0; i < result.Length; i++)
            result[i] = Pose(surfs.Surfaces[i], parts, l.Materials[i]);
        return new XModelMesh(modelPath, model, parts, result);
    }

    /// <summary>
    /// Pose de reposo: cada vértice es la suma ponderada de sus influencias (rot_hueso · p + pos_hueso);
    /// la normal se gira con el hueso principal.
    /// </summary>
    public static XModelMeshPart Pose(XModelSurface s, XModelParts parts, string material)
    {
        var bones = parts.Bones;
        var n = s.VertexCount;
        var pos = new float[n * 3];
        var nor = new float[n * 3];
        for (var v = 0; v < n; v++)
        {
            float x = 0, y = 0, z = 0;
            foreach (var w in s.Weights[v])
            {
                var b = bones[w.Bone];
                var p = Quat.Rotate(b.Rotation, w.X, w.Y, w.Z);
                x += w.Weight * (p[0] + b.Position[0]);
                y += w.Weight * (p[1] + b.Position[1]);
                z += w.Weight * (p[2] + b.Position[2]);
            }
            pos[v * 3] = x; pos[v * 3 + 1] = y; pos[v * 3 + 2] = z;
            var nb = bones[s.Weights[v][0].Bone];
            var nn = Quat.Rotate(nb.Rotation, s.Normals[v * 3], s.Normals[v * 3 + 1], s.Normals[v * 3 + 2]);
            nor[v * 3] = nn[0]; nor[v * 3 + 1] = nn[1]; nor[v * 3 + 2] = nn[2];
        }
        return new XModelMeshPart(material, pos, nor, s.UVs, s.Triangles);
    }
}
