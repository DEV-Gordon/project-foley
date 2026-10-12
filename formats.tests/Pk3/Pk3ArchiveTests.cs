using System.IO.Compression;
using System.Text;
using Foley.Formats.Pk3;

namespace Foley.Formats.Tests.Pk3;

public class Pk3ArchiveTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "foley-pk3-" + Guid.NewGuid().ToString("N"));

    public Pk3ArchiveTests() => Directory.CreateDirectory(_dir);
    public void Dispose() => Directory.Delete(_dir, recursive: true);

    // Los zips se generan aquí con contenido sintético; no se usan archivos del juego.
    private string MakePk3(string name, params (string path, string content)[] files)
    {
        var path = Path.Combine(_dir, name);
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (p, c) in files)
        {
            var e = zip.CreateEntry(p);
            using var w = new StreamWriter(e.Open(), Encoding.Latin1);
            w.Write(c);
        }
        return path;
    }

    [Fact]
    public void ListaEntradasConRutaNormalizada()
    {
        var f = MakePk3("a.pk3", ("Maps/Test.BSP", "xx"), ("maps\\mp\\x.gsc", "hola"));
        using var pk3 = new Pk3Archive(f);
        Assert.Equal(2, pk3.Entries.Count);
        Assert.Contains(pk3.Entries, e => e.Path == "maps/test.bsp" && e.Size == 2);
        Assert.True(pk3.Contains("MAPS/mp/X.gsc"));
    }

    [Fact]
    public void LeeContenido()
    {
        var f = MakePk3("a.pk3", ("a/b.txt", "contenido"));
        using var pk3 = new Pk3Archive(f);
        Assert.Equal("contenido", pk3.ReadAllText("a/b.txt"));
        Assert.Throws<FileNotFoundException>(() => pk3.ReadAllBytes("no/existe"));
    }

    [Fact]
    public void ColeccionGanaElUltimoPk3EnOrdenAlfabetico()
    {
        MakePk3("pak0.pk3", ("x.txt", "viejo"), ("solo0.txt", "0"));
        MakePk3("pak1.pk3", ("x.txt", "nuevo"));
        using var c = new Pk3Collection(_dir);
        Assert.Equal("nuevo", c.ReadAllText("x.txt"));
        Assert.Equal("pak1.pk3", c.FindOwner("x.txt")!.Name);
        Assert.Equal("0", c.ReadAllText("solo0.txt"));
    }

    [Theory]
    [InlineData("maps/mp/a.gsc", ".gsc")]
    [InlineData("a.dir/modelo", Pk3Inventory.NoExtension)]
    [InlineData("x/y.tar.gz", ".gz")]
    public void TipoPorExtension(string path, string esperado) =>
        Assert.Equal(esperado, Pk3Inventory.TypeOf(path));

    [Fact]
    public void InventarioSumaPorPk3YPorTipo()
    {
        MakePk3("pak0.pk3", ("a.gsc", "12"), ("b.gsc", "345"), ("c.bsp", "1"));
        MakePk3("pak1.pk3", ("d.gsc", "1234"));
        using var c = new Pk3Collection(_dir);
        var inv = Pk3Inventory.Build(c.Archives);
        Assert.Equal(4, inv.Total.Count);
        Assert.Equal(3, inv.ByType[".gsc"].Count);
        Assert.Equal(9, inv.ByType[".gsc"].Size);
        Assert.Equal(3, inv.ByPk3["pak0.pk3"].Count);
        Assert.Equal(1, inv.ByPk3AndType["pak1.pk3"][".gsc"].Count);
    }

    [Fact]
    public void LocalizaCarpetaMain()
    {
        var root = Directory.CreateDirectory(Path.Combine(_dir, "juego")).FullName;
        var main = Directory.CreateDirectory(Path.Combine(root, "Main")).FullName;
        File.Copy(MakePk3("tmp.pk3", ("a.txt", "x")), Path.Combine(main, "pak0.pk3"));
        Assert.Equal(main, Pk3Locator.FindPk3Directory(root));
        Assert.Null(Pk3Locator.FindPk3Directory(Path.Combine(_dir, "nada")));
    }
}
