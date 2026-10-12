namespace Foley.Formats.Materials;

/// <summary>
/// Definición mínima de un shader de <c>scripts/*.shader</c>: solo lo que el importador necesita
/// para elegir la textura y decidir qué no se dibuja. El resto de directivas se ignora.
/// </summary>
public sealed class ShaderDef
{
    public string Name { get; }
    public HashSet<string> SurfaceParms { get; } = new(StringComparer.OrdinalIgnoreCase);
    /// <summary>Primera etapa <c>map</c>/<c>clampmap</c> que no sea un valor <c>$...</c> (p. ej. $lightmap).</summary>
    public string? FirstMap { get; internal set; }
    public string? EditorImage { get; internal set; }
    /// <summary>Prefijo del cubo de cielo (<c>skyParms</c>), sin sufijos de cara.</summary>
    public string? SkyEnv { get; internal set; }

    public ShaderDef(string name) => Name = name;
}

public static class ShaderFile
{
    /// <summary>Lee todas las definiciones de un archivo .shader. Nombres en minúsculas.</summary>
    public static IEnumerable<ShaderDef> Parse(string text)
    {
        var depth = 0;
        ShaderDef? current = null;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw;
            var comment = line.IndexOf("//", StringComparison.Ordinal);
            if (comment >= 0) line = line[..comment];
            line = line.Trim();
            if (line.Length == 0) continue;

            if (line == "{") { depth++; continue; }
            if (line == "}")
            {
                depth--;
                if (depth == 0 && current is not null)
                {
                    yield return current;
                    current = null;
                }
                continue;
            }

            if (depth == 0)
            {
                // Nombre del shader: línea sola antes de la llave.
                current = new ShaderDef(line.ToLowerInvariant());
                continue;
            }
            if (current is null) continue;

            var parts = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) continue;
            switch (parts[0].ToLowerInvariant())
            {
                case "surfaceparm":
                    current.SurfaceParms.Add(parts[1]);
                    break;
                case "qer_editorimage":
                    current.EditorImage ??= parts[1].ToLowerInvariant();
                    break;
                case "skyparms":
                    if (parts[1] != "-") current.SkyEnv ??= parts[1].ToLowerInvariant();
                    break;
                case "map":
                case "clampmap":
                    if (!parts[1].StartsWith('$')) current.FirstMap ??= parts[1].ToLowerInvariant();
                    break;
            }
        }
    }
}
