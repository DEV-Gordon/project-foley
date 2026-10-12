using Foley.Formats.Pk3;

namespace Foley.Formats.Tests.Pk3;

/// <summary>Procesa la instalación real de COD1_PATH. Se salta si la variable no existe.</summary>
public class Pk3CorpusTests
{
    private static Pk3Collection Open() =>
        new(Pk3Locator.FindPk3Directory(Cod1Corpus.Root!)!);

    [CorpusFact]
    public void AbreTodosLosPk3()
    {
        using var c = Open();
        Assert.NotEmpty(c.Archives);
        Assert.All(c.Archives, a => Assert.NotEmpty(a.Entries));
    }

    [CorpusFact]
    public void ElInventarioCuadraConLasEntradas()
    {
        using var c = Open();
        var inv = Pk3Inventory.Build(c.Archives);
        Assert.Equal(c.Archives.Sum(a => a.Entries.Count), inv.Total.Count);
        Assert.Equal(inv.Total.Count, inv.ByType.Values.Sum(t => t.Count));
        Assert.Equal(inv.Total.Size, inv.ByPk3.Values.Sum(t => t.Size));
    }

    [CorpusFact]
    public void LeeCompletaUnaEntradaDeCadaTipo()
    {
        using var c = Open();
        foreach (var g in c.AllPaths.GroupBy(Pk3Inventory.TypeOf))
        {
            // Una de cada tipo, la más pequeña, para que sea rápido.
            var path = g.OrderBy(p => c.FindOwner(p)!.Entries.First(e => e.Path == p).Size).First();
            var owner = c.FindOwner(path)!;
            var expected = owner.Entries.Last(e => e.Path == path).Size;
            Assert.Equal(expected, owner.ReadAllBytes(path).Length);
        }
    }
}
