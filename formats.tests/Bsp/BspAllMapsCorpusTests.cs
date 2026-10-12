using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;
using Xunit.Abstractions;

namespace Foley.Formats.Tests.Bsp;

/// <summary>
/// Corpus completo: todos los BSP de la instalación, leídos de uno en uno (se suelta cada
/// archivo antes de leer el siguiente). Criterio de cierre del hito 2.
/// </summary>
public class BspAllMapsCorpusTests
{
    /// <summary>
    /// BSP de marcador (4248 bytes en la instalación revisada): una sola caja cerrada de
    /// <c>textures/common/blackish</c> con un <c>info_player_start</c> dentro. Se excluyen de las
    /// comprobaciones de mapa jugable, pero se verifica que sigan siendo exactamente eso.
    /// </summary>
    private static readonly HashSet<string> Stubs = new(StringComparer.Ordinal)
    {
        "maps/allied_start.bsp",
        "maps/ru_stalingrad.bsp",
        "maps/uk_6ab.bsp",
        "maps/uk_sas.bsp",
        "maps/us_intro.bsp",
        "maps/us_mid.bsp",
    };

    /// <summary>
    /// Materiales que los BSP referencian pero que no existen en ningún pk3 (ni textura ni shader).
    /// Faltan en los datos del juego mismo, no es un fallo del lector. Cómo los dibuja el original
    /// está sin determinar (afecta solo a la presentación).
    /// </summary>
    private static readonly HashSet<string> MissingInGameData = new(StringComparer.OrdinalIgnoreCase)
    {
        "textures/normandy/windows/glass@window2a",
        "textures/industrial/glass@factorywindow4a_broken",
        "textures/industrial/glass@factorywindow4z_broken",
        "textures/industrial/glass@factorywindow4o_broken",
    };

    private readonly ITestOutputHelper _output;

    public BspAllMapsCorpusTests(ITestOutputHelper output) => _output = output;

    [CorpusFact]
    public void TodosLosBspSeLeenYSonCoherentes()
    {
        using var pk3 = new Pk3Collection(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);
        var resolver = new MaterialResolver(pk3);
        var maps = pk3.AllPaths.Where(p => p.EndsWith(".bsp", StringComparison.Ordinal))
            .OrderBy(p => p, StringComparer.Ordinal).ToList();

        var failures = new List<string>();
        var real = 0;
        foreach (var path in maps)
        {
            BspFile bsp;
            try
            {
                bsp = BspFile.Read(pk3.ReadAllBytes(path));
            }
            catch (Exception e)
            {
                failures.Add($"{path}: {e.Message}");
                continue;
            }

            if (Stubs.Contains(path))
            {
                var isBox = bsp.Surfaces.Length == 1
                            && bsp.Vertices.Length == 24 && bsp.Indices.Length == 36
                            && bsp.Materials[bsp.Surfaces[0].MaterialIndex].Name == "textures/common/blackish"
                            && bsp.Entities.Select(e => e.ClassName).OrderBy(c => c)
                                .SequenceEqual(new[] { "info_player_start", "worldspawn" });
                if (!isBox) failures.Add($"{path}: marcado como stub pero no es la caja de marcador");
                _output.WriteLine($"{path}: stub (caja de marcador)");
                continue;
            }

            real++;
            var problems = Check(bsp, resolver);
            _output.WriteLine($"{path}: {bsp.Materials.Length} mat, {bsp.Surfaces.Length} sup, " +
                              $"{bsp.Vertices.Length} vért, {bsp.Indices.Length} índ, {bsp.Entities.Count} ent" +
                              (problems.Count == 0 ? "" : " -> " + string.Join("; ", problems)));
            failures.AddRange(problems.Select(p => $"{path}: {p}"));
        }

        _output.WriteLine($"Total: {maps.Count} BSP, {real} jugables, {maps.Count - real} stubs");
        Assert.Equal(Stubs.Count, maps.Count(Stubs.Contains));
        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static List<string> Check(BspFile bsp, MaterialResolver resolver)
    {
        var problems = new List<string>();
        if (bsp.Surfaces.Length == 0) problems.Add("sin superficies");

        // Las superficies cubren vértices e índices exactamente (observado en burnville).
        long sumV = 0, sumI = 0;
        foreach (var s in bsp.Surfaces) { sumV += s.VertexCount; sumI += s.IndexCount; }
        if (sumV != bsp.Vertices.Length) problems.Add($"suma de vértices {sumV} != {bsp.Vertices.Length}");
        if (sumI != bsp.Indices.Length) problems.Add($"suma de índices {sumI} != {bsp.Indices.Length}");

        // Índices relativos a su superficie y dentro de rango.
        var outOfRange = 0;
        foreach (var s in bsp.Surfaces)
            for (var k = 0; k < s.IndexCount; k++)
                if (bsp.Indices[s.FirstIndex + k] >= s.VertexCount) outOfRange++;
        if (outOfRange > 0) problems.Add($"{outOfRange} índices fuera de su superficie");

        if (!bsp.Entities.Any(e => e.ClassName == "worldspawn")) problems.Add("sin worldspawn");

        var unresolved = bsp.Surfaces.Select(s => (int)s.MaterialIndex).Distinct()
            .Select(i => resolver.Resolve(bsp.Materials[i].Name))
            .Where(r => r.TexturePath is null && !r.IsSky && !r.NoDraw && !MissingInGameData.Contains(r.Name))
            .Select(r => r.Name).ToList();
        if (unresolved.Count > 0) problems.Add($"materiales sin resolver: {string.Join(", ", unresolved)}");

        return problems;
    }
}
