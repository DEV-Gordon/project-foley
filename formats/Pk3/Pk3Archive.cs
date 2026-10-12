using System.IO.Compression;

namespace Foley.Formats.Pk3;

/// <summary>Entrada de un pk3: ruta lógica (minúsculas, separador '/') y tamaños.</summary>
public sealed record Pk3Entry(string Path, long Size, long CompressedSize);

/// <summary>
/// Lector de un archivo pk3 (zip renombrado). Solo lectura; no extrae a disco.
/// </summary>
public sealed class Pk3Archive : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _byPath;

    public string FilePath { get; }
    public string Name => System.IO.Path.GetFileName(FilePath);
    public IReadOnlyList<Pk3Entry> Entries { get; }

    public Pk3Archive(string filePath)
    {
        FilePath = filePath;
        _zip = ZipFile.OpenRead(filePath);
        _byPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.Ordinal);
        var entries = new List<Pk3Entry>();
        foreach (var e in _zip.Entries)
        {
            if (e.FullName.EndsWith('/')) continue; // directorio
            var path = Normalize(e.FullName);
            // Si el zip repite una ruta, gana la última (como el resto del motor).
            _byPath[path] = e;
            entries.Add(new Pk3Entry(path, e.Length, e.CompressedLength));
        }
        Entries = entries;
    }

    /// <summary>Rutas lógicas se comparan sin distinguir mayúsculas, con '/'.</summary>
    public static string Normalize(string path) =>
        path.Replace('\\', '/').TrimStart('/').ToLowerInvariant();

    public bool Contains(string path) => _byPath.ContainsKey(Normalize(path));

    /// <summary>Abre el contenido de una entrada. Lanza si no existe.</summary>
    public Stream Open(string path)
    {
        if (!_byPath.TryGetValue(Normalize(path), out var e))
            throw new FileNotFoundException($"'{path}' no está en {Name}");
        return e.Open();
    }

    public byte[] ReadAllBytes(string path)
    {
        using var s = Open(path);
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        return ms.ToArray();
    }

    public string ReadAllText(string path)
    {
        using var s = Open(path);
        using var r = new StreamReader(s, System.Text.Encoding.Latin1);
        return r.ReadToEnd();
    }

    public void Dispose() => _zip.Dispose();
}
