using System.Buffers.Binary;
using Foley.Formats.Bsp;
using Foley.Formats.Pk3;
using Foley.Formats.XModel;

namespace Foley.Formats.Tests.XModel;

/// <summary>Lector de xmodel / xmodelparts / xmodelsurfs (docs/formats/xmodel.md).</summary>
public class XModelTests
{
    [Fact]
    public void LaTiraSeConvierteComoEnElOriginal()
    {
        var o = new List<int>();
        // Tira con el primer índice repetido (fija el giro): quad 0-1-2-3.
        XModelSurfs.StripToTriangles(new[] { 0, 0, 1, 2, 3 }, o);
        Assert.Equal(new[] { 1, 0, 2, 1, 2, 3 }, o);

        o.Clear();
        XModelSurfs.StripToTriangles(new[] { 0, 1, 2 }, o);
        Assert.Equal(new[] { 0, 1, 2 }, o);

        o.Clear();
        // Abanico codificado con degenerados: 5f,5f,60,61,60,62,60,63,64 (visto en health_medium0).
        XModelSurfs.StripToTriangles(new[] { 5, 5, 6, 7, 6, 8, 6, 9, 10 }, o);
        Assert.Equal(new[] { 6, 5, 7, 6, 7, 8, 6, 8, 9, 6, 9, 10 }, o);
    }

    [Fact]
    public void SuperficieRigidaSintetica()
    {
        var b = new List<byte>();
        void U8(int v) => b.Add((byte)v);
        void U16(int v) { var t = new byte[2]; BinaryPrimitives.WriteInt16LittleEndian(t, (short)v); b.AddRange(t); }
        void F(float v) { var t = new byte[4]; BinaryPrimitives.WriteSingleLittleEndian(t, v); b.AddRange(t); }
        U16(14); U16(1);                 // versión, 1 superficie
        U8(0); U16(3); U16(1); U16(1); U16(0); // modo, 3 vértices, 1 triángulo, 1 tira, hueso 0
        U8(3); U16(0); U16(1); U16(2);
        for (var v = 0; v < 3; v++) { F(0); F(0); F(1); F(v); F(0); F(v * 10); F(0); F(5); }

        var s = XModelSurfs.Read(b.ToArray()).Surfaces[0];
        Assert.Equal(0, s.Bone);
        Assert.Equal(new[] { 0, 1, 2 }, s.Triangles);
        Assert.Equal(20f, s.Weights[2][0].X);
        Assert.Equal(5f, s.Weights[2][0].Z);
    }

    [CorpusFact]
    public void TodosLosXmodelsDeLaInstalacionSeLeenEnteros()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var models = pk3.AllPaths.Where(p => p.StartsWith("xmodel/") && p.Length > "xmodel/".Length).ToList();
        Assert.True(models.Count > 900, $"solo {models.Count} xmodels");
        int weighted = 0, missing = 0;
        foreach (var path in models)
        {
            // De uno en uno: cada malla se descarta al terminar.
            var model = XModelFile.Read(pk3.ReadAllBytes(path));
            for (var lod = 0; lod < model.Lods.Length; lod++)
            {
                // Algunos LODs podrían apuntar a archivos que la instalación no trae.
                var name = model.Lods[lod].Name;
                if (!pk3.Contains("xmodelparts/" + model.Lods[0].Name) || !pk3.Contains("xmodelsurfs/" + name)) { missing++; continue; }
                var mesh = XModelMesh.Load(pk3, path, lod);
                foreach (var surf in XModelSurfs.Read(pk3.ReadAllBytes("xmodelsurfs/" + model.Lods[lod].Name)).Surfaces)
                {
                    if (surf.Bone != -1) continue;
                    weighted++;
                    foreach (var ws in surf.Weights)
                        Assert.InRange(ws.Sum(w => w.Weight), 0.98f, 1.02f);
                }
                Assert.All(mesh.Surfaces, s => Assert.Equal(s.Positions.Length / 3 * 2, s.UVs.Length));
            }
        }
        Assert.True(weighted > 0);
        Assert.True(missing < 20, $"LODs sin archivos: {missing}");
    }

    [CorpusFact]
    public void LosModelosDeBurnvilleTienenGiroHorarioYSusTexturas()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var bsp = BspFile.Read(pk3.ReadAllBytes("maps/burnville.bsp"));
        var paths = bsp.Entities.Select(e => e.Get("model")).Where(m => m is not null && !m.StartsWith('*')).Distinct().ToList();
        Assert.Equal(81, paths.Count);

        int total = 0, clockwise = 0;
        foreach (var path in paths)
        {
            var mesh = XModelMesh.Load(pk3, path!);
            foreach (var s in mesh.Surfaces)
            {
                Assert.True(pk3.Contains("skins/" + s.Material), $"{path}: falta skins/{s.Material}");
                for (var t = 0; t < s.Triangles.Length; t += 3)
                {
                    int a = s.Triangles[t] * 3, b = s.Triangles[t + 1] * 3, c = s.Triangles[t + 2] * 3;
                    float e1x = s.Positions[b] - s.Positions[a], e1y = s.Positions[b + 1] - s.Positions[a + 1], e1z = s.Positions[b + 2] - s.Positions[a + 2];
                    float e2x = s.Positions[c] - s.Positions[a], e2y = s.Positions[c + 1] - s.Positions[a + 1], e2z = s.Positions[c + 2] - s.Positions[a + 2];
                    var nx = e1y * e2z - e1z * e2y; var ny = e1z * e2x - e1x * e2z; var nz = e1x * e2y - e1y * e2x;
                    var sn = s.Normals[a] + s.Normals[b] + s.Normals[c];
                    var sy = s.Normals[a + 1] + s.Normals[b + 1] + s.Normals[c + 1];
                    var sz = s.Normals[a + 2] + s.Normals[b + 2] + s.Normals[c + 2];
                    total++;
                    if (nx * sn + ny * sy + nz * sz < 0) clockwise++;
                }
            }
        }
        // Mismo giro que el BSP: horario respecto a la normal de los vértices.
        Assert.True(clockwise >= total * 0.95, $"horarios {clockwise} de {total}");
    }
}
