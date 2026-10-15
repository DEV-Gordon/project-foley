using Foley.Formats.Pk3;

namespace Foley.Formats.Materials;

/// <summary>Superficie del mapa (BSP) o de un modelo (xmodel): eligen carpeta de plantillas distinta.</summary>
public enum MaterialKind { World, Model }

/// <summary>De dónde salió el shader de un material.</summary>
public enum MaterialSource
{
    /// <summary>Un shader con el mismo nombre en <c>scripts/*.shader</c>.</summary>
    Shader,
    /// <summary>La plantilla <c>shadertypes/&lt;world|model&gt;/&lt;tipo&gt;.stype</c> del prefijo del nombre.</summary>
    ShaderType,
    /// <summary>Sin shader ni plantilla: una etapa con la textura (como el shader por defecto de id Tech 3).</summary>
    Default,
}

/// <summary>Resultado de resolver un material: su shader (siempre presente) y su textura principal.</summary>
public sealed record ResolvedMaterial(
    string Name,
    ShaderDef Shader,
    MaterialSource Source,
    string? ShaderType,
    string? TexturePath,
    bool IsSky,
    bool NoDraw);

/// <summary>
/// Resuelve materiales a shaders y archivos de textura. Orden (docs/formats/shader.md):
/// 1. shader con ese nombre en <c>scripts/*.shader</c>;
/// 2. plantilla del tipo de superficie (lo que va antes de '@' en el último componente del nombre),
///    con <c>$texturename</c> = el propio material;
/// 3. shader por defecto con la textura del mismo nombre.
/// Texturas: <c>&lt;ruta&gt;.dds|.tga|.jpg</c> (supuesto sin verificar: preferencia dds, tga, jpg).
/// </summary>
public sealed class MaterialResolver
{
    private static readonly string[] Extensions = { ".dds", ".tga", ".jpg" };

    private readonly Pk3Collection _pk3;
    private readonly Dictionary<string, ShaderDef> _shaders = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ShaderDef?> _types = new(StringComparer.Ordinal);

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

    public ShaderDef? FindShader(string name) => _shaders.GetValueOrDefault(StripExtension(name.ToLowerInvariant()));

    /// <summary>Plantilla <c>shadertypes/&lt;kind&gt;/&lt;tipo&gt;.stype</c>, o null si no existe.</summary>
    public ShaderDef? FindShaderType(MaterialKind kind, string type)
    {
        var key = (kind == MaterialKind.World ? "world/" : "model/") + type.ToLowerInvariant();
        if (_types.TryGetValue(key, out var cached)) return cached;
        var path = "shadertypes/" + key + ".stype";
        var def = _pk3.Contains(path) ? ShaderFile.ParseType(key, _pk3.ReadAllText(path)) : null;
        _types[key] = def;
        return def;
    }

    /// <summary>Tipo de superficie del nombre: lo que va antes de '@' en el último componente, o null.</summary>
    public static string? SurfaceType(string materialName)
    {
        var last = materialName.Replace('\\', '/');
        last = last[(last.LastIndexOf('/') + 1)..];
        var at = last.IndexOf('@');
        return at > 0 ? last[..at].ToLowerInvariant() : null;
    }

    /// <param name="materialName">material del BSP (<c>textures/...</c>) o ruta de la skin del modelo (<c>skins/...</c>)</param>
    public ResolvedMaterial Resolve(string materialName, MaterialKind kind = MaterialKind.World)
    {
        var name = StripExtension(materialName.Replace('\\', '/').ToLowerInvariant());
        var type = SurfaceType(name);

        ShaderDef shader;
        MaterialSource source;
        if (_shaders.TryGetValue(name, out var explicitShader))
        {
            shader = explicitShader;
            source = MaterialSource.Shader;
        }
        else if (type is not null && FindShaderType(kind, type) is { } template)
        {
            shader = template.Instantiate(name, name);
            source = MaterialSource.ShaderType;
        }
        else
        {
            shader = new ShaderDef(name);
            var stage = new ShaderStage();
            stage.Main.Map = name;
            shader.Stages.Add(stage);
            source = MaterialSource.Default;
        }

        var isSky = shader.SurfaceParms.Contains("sky");
        var noDraw = shader.SurfaceParms.Contains("nodraw");

        // Textura principal: la primera de las etapas; si no hay, la del editor (como antes).
        var tex = FindTexture(name);
        if (source == MaterialSource.Shader)
        {
            tex = null;
            if (shader.FirstMap is { } first) tex = FindTexture(first);
            if (tex is null && shader.EditorImage is not null) tex = FindTexture(shader.EditorImage);
            if (tex is null) tex = FindTexture(name);
        }
        return new ResolvedMaterial(materialName, shader, source, source == MaterialSource.ShaderType ? type : null, tex, isSky, noDraw);
    }

    /// <summary>Archivo de textura de una ruta de shader (con o sin extensión), o null.</summary>
    public string? FindTexture(string nameOrPath)
    {
        var stem = StripExtension(nameOrPath.Replace('\\', '/').ToLowerInvariant());
        foreach (var ext in Extensions)
            if (_pk3.Contains(stem + ext)) return stem + ext;
        return null;
    }

    private static string StripExtension(string s)
    {
        foreach (var ext in Extensions)
            if (s.EndsWith(ext, StringComparison.Ordinal)) return s[..^ext.Length];
        return s;
    }
}
