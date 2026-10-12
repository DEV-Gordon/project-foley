namespace Foley.Formats.Pk3;

/// <summary>
/// Conjunto de pk3 de un directorio, con la precedencia de id Tech 3:
/// los pk3 se ordenan alfabéticamente y el último que contiene una ruta gana.
/// </summary>
public sealed class Pk3Collection : IDisposable
{
    private readonly List<Pk3Archive> _archives;
    private readonly Dictionary<string, Pk3Archive> _owner = new(StringComparer.Ordinal);

    public IReadOnlyList<Pk3Archive> Archives => _archives;

    public Pk3Collection(string directory)
    {
        var files = Directory.GetFiles(directory, "*.pk3");
        Array.Sort(files, StringComparer.OrdinalIgnoreCase);
        _archives = new List<Pk3Archive>(files.Length);
        foreach (var f in files)
        {
            var a = new Pk3Archive(f);
            _archives.Add(a);
            foreach (var e in a.Entries) _owner[e.Path] = a;
        }
    }

    public IEnumerable<string> AllPaths => _owner.Keys;

    public bool Contains(string path) => _owner.ContainsKey(Pk3Archive.Normalize(path));

    /// <summary>pk3 que aporta la versión efectiva de la ruta, o null.</summary>
    public Pk3Archive? FindOwner(string path) =>
        _owner.GetValueOrDefault(Pk3Archive.Normalize(path));

    public byte[] ReadAllBytes(string path) =>
        (FindOwner(path) ?? throw new FileNotFoundException(path)).ReadAllBytes(path);

    public string ReadAllText(string path) =>
        (FindOwner(path) ?? throw new FileNotFoundException(path)).ReadAllText(path);

    public void Dispose()
    {
        foreach (var a in _archives) a.Dispose();
    }
}
