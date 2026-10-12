using Foley.Formats.Pk3;

namespace Foley.Formats.Materials;

/// <summary>Resultado de resolver un material del BSP a un archivo de textura.</summary>
public sealed record ResolvedMaterial(
    string Name,
    string? TexturePath,
    bool IsSky,
    bool NoDraw,
    ShaderDef? Shader);

/// <summary>
/// Resuelve nombres de material del BSP a archivos de textura dentro de los pk3.
/// Orden: archivo <c>&lt;nombre&gt;.dds|.tga|.jpg</c>; si no existe, el shader con ese nombre
/// (primer <c>map</c>, luego <c>qer_editorimage</c>).
/// Supuesto sin verificar: si hay varias extensiones para el mismo nombre se prefiere dds, tga, jpg.
/// </summary>
public sealed class MaterialResolver
{
    private static readonly string[] Extensions = { ".dds", ".tga", ".jpg" };

    private readonly Pk3Collection _pk3;
    private readonly Dictionary<string, ShaderDef> _shaders = new(StringComparer.Ordinal);

    public int ShaderCount => _shaders.Count;

    public MaterialResolver(Pk3Collection pk3)
    {
        _pk3 = pk3;
        foreach (var path in pk3.AllPaths)
        {
            if (!path.StartsWith("scripts/", StringComparison.Ordinal) ||
                !path.EndsWith(".shader", StringComparison.Ordinal)) continue;
            foreach (var def in ShaderFile.Parse(pk3.ReadAllText(path)))
                _shaders[def.Name] = def; // el último gana
        }
    }

    public ShaderDef? FindShader(string name) => _shaders.GetValueOrDefault(name.ToLowerInvariant());

    public ResolvedMaterial Resolve(string materialName)
    {
        var name = materialName.ToLowerInvariant();
        _shaders.TryGetValue(name, out var shader);

        var isSky = shader?.SurfaceParms.Contains("sky") == true;
        var noDraw = shader?.SurfaceParms.Contains("nodraw") == true;

        var tex = FindTexture(name);
        if (tex is null && shader is not null)
        {
            if (shader.FirstMap is not null) tex = FindTexture(shader.FirstMap);
            if (tex is null && shader.EditorImage is not null) tex = FindTexture(shader.EditorImage);
        }
        return new ResolvedMaterial(materialName, tex, isSky, noDraw, shader);
    }

    private string? FindTexture(string nameOrPath)
    {
        var stem = nameOrPath;
        foreach (var ext in Extensions)
        {
            if (stem.EndsWith(ext, StringComparison.Ordinal))
            {
                stem = stem[..^ext.Length];
                break;
            }
        }
        foreach (var ext in Extensions)
            if (_pk3.Contains(stem + ext)) return stem + ext;
        return null;
    }
}
