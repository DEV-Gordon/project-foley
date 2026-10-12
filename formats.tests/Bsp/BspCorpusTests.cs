using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;

namespace Foley.Formats.Tests.Bsp;

/// <summary>
/// Corpus: solo el mapa del vertical slice. Cuando el formato esté contrastado con más mapas
/// se amplía a todos (de uno en uno, para no acumular memoria).
/// </summary>
public class BspCorpusTests
{
    private const string Map = "maps/burnville.bsp";

    private static Pk3Collection Open() =>
        new(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);

    [CorpusFact]
    public void BurnvilleSeLeeYSusCuentasCuadran()
    {
        using var pk3 = Open();
        var bsp = BspFile.Read(pk3.ReadAllBytes(Map));

        // Valores observados al deducir el formato (docs/formats/bsp.md).
        Assert.Equal(404, bsp.Materials.Length);
        Assert.Equal(2424, bsp.Surfaces.Length);
        Assert.Equal(82665, bsp.Vertices.Length);
        Assert.Equal(176601, bsp.Indices.Length);

        // Las superficies cubren todos los vértices e índices sin huecos ni solapes.
        Assert.Equal(bsp.Vertices.Length, bsp.Surfaces.Sum(s => (int)s.VertexCount));
        Assert.Equal(bsp.Indices.Length, bsp.Surfaces.Sum(s => (int)s.IndexCount));
    }

    [CorpusFact]
    public void BurnvilleIndicesYGiroSonCoherentes()
    {
        using var pk3 = Open();
        var bsp = BspFile.Read(pk3.ReadAllBytes(Map));

        var clockwise = 0;
        var total = 0;
        foreach (var s in bsp.Surfaces)
        {
            for (var t = 0; t < s.IndexCount; t += 3)
            {
                var i0 = bsp.Indices[s.FirstIndex + t];
                var i1 = bsp.Indices[s.FirstIndex + t + 1];
                var i2 = bsp.Indices[s.FirstIndex + t + 2];
                Assert.True(i0 < s.VertexCount && i1 < s.VertexCount && i2 < s.VertexCount);

                var a = bsp.Vertices[(int)s.FirstVertex + i0];
                var b = bsp.Vertices[(int)s.FirstVertex + i1];
                var c = bsp.Vertices[(int)s.FirstVertex + i2];
                var e1x = b.X - a.X; var e1y = b.Y - a.Y; var e1z = b.Z - a.Z;
                var e2x = c.X - a.X; var e2y = c.Y - a.Y; var e2z = c.Z - a.Z;
                var nx = e1y * e2z - e1z * e2y;
                var ny = e1z * e2x - e1x * e2z;
                var nz = e1x * e2y - e1y * e2x;
                total++;
                if (nx * a.Nx + ny * a.Ny + nz * a.Nz < 0) clockwise++;
            }
        }
        Assert.Equal(58867, total);
        // Observado en burnville: el 100 % es horario. Se tolera un margen mínimo por mapas con triángulos degenerados.
        Assert.True(clockwise >= total * 0.99, $"horarios {clockwise} de {total}");
    }

    [CorpusFact]
    public void BurnvilleTieneLasEntidadesEsperadas()
    {
        using var pk3 = Open();
        var bsp = BspFile.Read(pk3.ReadAllBytes(Map));
        Assert.Equal(2958, bsp.Entities.Count);
        Assert.Contains(bsp.Entities, e => e.ClassName == "worldspawn");
        Assert.Contains(bsp.Entities, e => e.ClassName == "info_player_start");
    }

    [CorpusFact]
    public void TodosLosMaterialesDeBurnvilleResuelvenATexturaOCielo()
    {
        using var pk3 = Open();
        var bsp = BspFile.Read(pk3.ReadAllBytes(Map));
        var resolver = new MaterialResolver(pk3);

        var used = bsp.Surfaces.Select(s => (int)s.MaterialIndex).Distinct();
        var unresolved = new List<string>();
        foreach (var i in used)
        {
            var r = resolver.Resolve(bsp.Materials[i].Name);
            if (r.TexturePath is null && !r.IsSky && !r.NoDraw) unresolved.Add(r.Name);
        }
        Assert.True(unresolved.Count == 0, "Sin resolver: " + string.Join(", ", unresolved));
    }
}
