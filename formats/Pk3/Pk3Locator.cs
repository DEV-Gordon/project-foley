namespace Foley.Formats.Pk3;

public static class Pk3Locator
{
    /// <summary>
    /// Devuelve la carpeta con los pk3: la propia ruta si tiene .pk3, o su subcarpeta "Main".
    /// Null si no hay ninguna.
    /// </summary>
    public static string? FindPk3Directory(string gameRoot)
    {
        if (!Directory.Exists(gameRoot)) return null;
        if (HasPk3(gameRoot)) return gameRoot;
        // En Windows el sistema de archivos no distingue mayúsculas, pero en otros sí.
        foreach (var name in new[] { "Main", "main" })
        {
            var sub = Path.Combine(gameRoot, name);
            if (Directory.Exists(sub) && HasPk3(sub)) return sub;
        }
        return null;
    }

    private static bool HasPk3(string dir) =>
        Directory.EnumerateFiles(dir, "*.pk3").Any();
}
