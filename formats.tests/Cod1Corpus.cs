namespace Foley.Formats.Tests;

/// <summary>Ruta de la instalación del juego, tomada de COD1_PATH.</summary>
public static class Cod1Corpus
{
    public static string? Root { get; } = ResolveRoot();

    private static string? ResolveRoot()
    {
        var p = Environment.GetEnvironmentVariable("COD1_PATH");
        return !string.IsNullOrWhiteSpace(p) && Directory.Exists(p) ? p : null;
    }
}

/// <summary>Test de corpus: se salta (no falla) si COD1_PATH no existe o no tiene pk3.</summary>
public sealed class CorpusFactAttribute : FactAttribute
{
    public CorpusFactAttribute()
    {
        if (Cod1Corpus.Root is null)
            Skip = "COD1_PATH no está definida o no existe.";
        else if (Foley.Formats.Pk3.Pk3Locator.FindPk3Directory(Cod1Corpus.Root) is null)
            Skip = "COD1_PATH no contiene archivos .pk3.";
    }
}
