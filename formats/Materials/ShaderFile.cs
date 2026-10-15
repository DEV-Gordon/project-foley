using System.Globalization;

namespace Foley.Formats.Materials;

/// <summary>Cara que se descarta (<c>cull</c>). Por defecto se dibuja solo la cara frontal.</summary>
public enum ShaderCull { Front, None, Back }

/// <summary><c>alphaFunc</c>: prueba de alfa de la etapa.</summary>
public enum ShaderAlphaFunc { None, Gt0, Lt128, Ge128 }

/// <summary>Factores de <c>blendFunc</c> (nombres de OpenGL sin el prefijo GL_).</summary>
public enum BlendFactor
{
    Zero, One, SrcColor, OneMinusSrcColor, DstColor, OneMinusDstColor,
    SrcAlpha, OneMinusSrcAlpha, DstAlpha, OneMinusDstAlpha, SrcAlphaSaturate,
}

public enum WaveFunc { Sin, Triangle, Square, Sawtooth, InverseSawtooth, Noise }

/// <summary>Onda de los generadores y deformaciones: base + amplitud · f((tiempo + fase) · frecuencia).</summary>
public readonly record struct ShaderWave(WaveFunc Func, float Base, float Amplitude, float Phase, float Frequency);

/// <summary><c>rgbGen</c>.</summary>
public enum RgbGen { Identity, IdentityLighting, Vertex, ExactVertex, LightingDiffuse, LightingPrecalc, Const, ConstLighting, Wave, Other }

/// <summary><c>alphaGen</c>.</summary>
public enum AlphaGen { Identity, Vertex, Const, Wave, Other }

public enum TcModKind { Scroll, Scale, Rotate, Turb, Stretch, Transform }

/// <summary>
/// <c>tcMod</c>. Args: scroll (s/s, t/s), scale (s, t), rotate (grados/s),
/// transform (m00 m01 m10 m11 t0 t1); turb y stretch usan <see cref="Wave"/>.
/// </summary>
public sealed record TcMod(TcModKind Kind, float[] Args, ShaderWave? Wave = null);

/// <summary>Recorte de las coordenadas de textura (modificadores de <c>map</c>/<c>clampmap</c>).</summary>
public enum TextureClamp { None, Both, X, Y }

/// <summary>Una textura de la etapa (la principal o la de <c>nextbundle</c>).</summary>
public sealed class TextureBundle
{
    /// <summary>Ruta, o un valor especial: <c>$lightmap</c>, <c>$texturename</c>, <c>$whiteimage</c>, <c>*white</c>.</summary>
    public string? Map { get; internal set; }
    public TextureClamp Clamp { get; internal set; }
    public bool NoMipmaps { get; internal set; }
    /// <summary><c>animMap</c>: fotogramas por segundo y rutas de los fotogramas.</summary>
    public float AnimFrequency { get; internal set; }
    public List<string> AnimFrames { get; } = new();
    public List<TcMod> TcMods { get; } = new();
    /// <summary>
    /// <c>blendFunc</c> escrito después de <c>nextbundle</c>: cómo se combina este bundle con el anterior
    /// dentro de la etapa (multitextura). No cambia la mezcla de la etapa con el fondo.
    /// </summary>
    public BlendFactor? CombineSrc { get; internal set; }
    public BlendFactor? CombineDst { get; internal set; }
    /// <summary><c>tcGen</c> (sin interpretar: environment, vector...). null = coordenadas de la superficie.</summary>
    public string? TcGen { get; internal set; }

    public bool IsLightmap => string.Equals(Map, "$lightmap", StringComparison.OrdinalIgnoreCase);
    public bool IsAnimated => AnimFrames.Count > 0;
}

/// <summary>Una etapa (<c>{ ... }</c> dentro del shader).</summary>
public sealed class ShaderStage
{
    public List<TextureBundle> Bundles { get; } = new() { new TextureBundle() };
    public TextureBundle Main => Bundles[0];
    /// <summary>null = sin <c>blendFunc</c> (opaca: GL_ONE, GL_ZERO).</summary>
    public BlendFactor? BlendSrc { get; internal set; }
    public BlendFactor? BlendDst { get; internal set; }
    public ShaderAlphaFunc AlphaFunc { get; internal set; }
    public RgbGen RgbGen { get; internal set; } = RgbGen.Identity;
    public float[] RgbConst { get; internal set; } = { 1, 1, 1 };
    public ShaderWave? RgbWave { get; internal set; }
    public AlphaGen AlphaGen { get; internal set; } = AlphaGen.Identity;
    public float AlphaConst { get; internal set; } = 1;
    public ShaderWave? AlphaWave { get; internal set; }
    public bool DepthWrite { get; internal set; }
    public string? DepthFunc { get; internal set; }

    /// <summary>La etapa solo dibuja el lightmap (el proyecto no usa lightmaps).</summary>
    public bool IsLightmapOnly => Main.IsLightmap;
    public bool IsBlended => BlendSrc is not null && !(BlendSrc == BlendFactor.One && BlendDst == BlendFactor.Zero);
}

/// <summary>Un shader de <c>scripts/*.shader</c> o una plantilla de <c>shadertypes/*.stype</c>.</summary>
public sealed class ShaderDef
{
    public string Name { get; }
    public HashSet<string> SurfaceParms { get; } = new(StringComparer.OrdinalIgnoreCase);
    public string? EditorImage { get; internal set; }
    /// <summary>Prefijo del cubo de cielo (<c>skyParms</c>), sin sufijos de cara.</summary>
    public string? SkyEnv { get; internal set; }
    public float SkyCloudHeight { get; internal set; }
    /// <summary><c>sunfile</c>: nombre del archivo <c>scripts/&lt;nombre&gt;.sun</c>.</summary>
    public string? SunFile { get; internal set; }
    public ShaderCull Cull { get; internal set; } = ShaderCull.Front;
    public bool PolygonOffset { get; internal set; }
    public bool NoMipmaps { get; internal set; }
    public bool NoPicmip { get; internal set; }
    public bool RadialNormals { get; internal set; }
    public string? Sort { get; internal set; }
    /// <summary><c>deformVertexes</c>, cada uno con sus argumentos en minúsculas (p. ej. ["autosprite2"]).</summary>
    public List<string[]> Deforms { get; } = new();
    public List<ShaderStage> Stages { get; } = new();

    /// <summary>Primera textura de etapa que no sea un valor <c>$...</c> ni <c>*...</c>.</summary>
    public string? FirstMap =>
        Stages.SelectMany(s => s.Bundles)
              .Select(b => b.IsAnimated ? b.AnimFrames[0] : b.Map)
              .FirstOrDefault(m => m is not null && !m.StartsWith('$') && !m.StartsWith('*'));

    public ShaderDef(string name) => Name = name;

    /// <summary>Copia con <c>$texturename</c> sustituido por <paramref name="texture"/> (instancia de plantilla).</summary>
    public ShaderDef Instantiate(string name, string texture)
    {
        var copy = new ShaderDef(name)
        {
            EditorImage = EditorImage, SkyEnv = SkyEnv, SkyCloudHeight = SkyCloudHeight, SunFile = SunFile,
            Cull = Cull, PolygonOffset = PolygonOffset, NoMipmaps = NoMipmaps, NoPicmip = NoPicmip,
            RadialNormals = RadialNormals, Sort = Sort,
        };
        copy.SurfaceParms.UnionWith(SurfaceParms);
        copy.Deforms.AddRange(Deforms);
        foreach (var s in Stages)
        {
            var ns = new ShaderStage
            {
                BlendSrc = s.BlendSrc, BlendDst = s.BlendDst, AlphaFunc = s.AlphaFunc, RgbGen = s.RgbGen,
                RgbConst = s.RgbConst, RgbWave = s.RgbWave, AlphaGen = s.AlphaGen, AlphaConst = s.AlphaConst,
                AlphaWave = s.AlphaWave, DepthWrite = s.DepthWrite, DepthFunc = s.DepthFunc,
            };
            ns.Bundles.Clear();
            foreach (var b in s.Bundles)
            {
                var nb = new TextureBundle
                {
                    Map = string.Equals(b.Map, "$texturename", StringComparison.OrdinalIgnoreCase) ? texture : b.Map,
                    Clamp = b.Clamp, NoMipmaps = b.NoMipmaps, AnimFrequency = b.AnimFrequency, TcGen = b.TcGen,
                };
                nb.AnimFrames.AddRange(b.AnimFrames);
                nb.TcMods.AddRange(b.TcMods);
                ns.Bundles.Add(nb);
            }
            copy.Stages.Add(ns);
        }
        return copy;
    }
}

/// <summary>
/// Lector de shaders de id Tech 3 con las extensiones de CoD1 que aparecen en los datos
/// (<c>nextbundle</c>, modificadores de <c>map</c>, plantillas <c>.stype</c>). Ver docs/formats/shader.md.
/// Las directivas no reconocidas se ignoran.
/// </summary>
public static class ShaderFile
{
    /// <summary>Lee todas las definiciones de un archivo .shader. Nombres en minúsculas.</summary>
    public static IEnumerable<ShaderDef> Parse(string text)
    {
        var lines = Tokenize(text);
        var i = 0;
        while (i < lines.Count)
        {
            var line = lines[i];
            if (line.Length == 1 && line[0] == "{")
            {
                // Bloque sin nombre: se salta entero.
                i = SkipBlock(lines, i);
                continue;
            }
            var name = line[0].ToLowerInvariant();
            i++;
            if (line.Length > 1 && line[1] == "{")
            {
                var def = new ShaderDef(name);
                i = ParseBlock(lines, i, def);
                yield return def;
                continue;
            }
            if (i < lines.Count && lines[i].Length >= 1 && lines[i][0] == "{")
            {
                var def = new ShaderDef(name);
                i = ParseBlock(lines, i + 1, def);
                yield return def;
            }
        }
    }

    /// <summary>Lee una plantilla <c>.stype</c>: un solo bloque <c>{ ... }</c> sin nombre.</summary>
    public static ShaderDef ParseType(string name, string text)
    {
        var lines = Tokenize(text);
        var def = new ShaderDef(name.ToLowerInvariant());
        var start = lines.FindIndex(l => l.Length >= 1 && l[0] == "{");
        if (start >= 0) ParseBlock(lines, start + 1, def);
        return def;
    }

    // Tokens por línea, sin comentarios // ni /* */. Las llaves son tokens aparte.
    private static List<string[]> Tokenize(string text)
    {
        var result = new List<string[]>();
        var inBlockComment = false;
        foreach (var raw in text.Split('\n'))
        {
            var line = raw;
            var sb = new System.Text.StringBuilder();
            for (var k = 0; k < line.Length; k++)
            {
                if (inBlockComment)
                {
                    if (k + 1 < line.Length && line[k] == '*' && line[k + 1] == '/') { inBlockComment = false; k++; }
                    continue;
                }
                if (k + 1 < line.Length && line[k] == '/' && line[k + 1] == '/') break;
                if (k + 1 < line.Length && line[k] == '/' && line[k + 1] == '*') { inBlockComment = true; k++; continue; }
                var c = line[k];
                if (c == '{' || c == '}' || c == '(' || c == ')') { sb.Append(' ').Append(c).Append(' '); continue; }
                sb.Append(c);
            }
            var parts = sb.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            // Cada llave va en su propia "línea" para simplificar el recorrido.
            var current = new List<string>();
            foreach (var p in parts)
            {
                if (p == "{" || p == "}")
                {
                    if (current.Count > 0) { result.Add(current.ToArray()); current.Clear(); }
                    result.Add(new[] { p });
                }
                else current.Add(p);
            }
            if (current.Count > 0) result.Add(current.ToArray());
        }
        return result;
    }

    private static int SkipBlock(List<string[]> lines, int i)
    {
        var depth = 0;
        for (; i < lines.Count; i++)
        {
            if (lines[i][0] == "{") depth++;
            else if (lines[i][0] == "}" && --depth == 0) return i + 1;
        }
        return i;
    }

    // Cuerpo del shader a partir de la línea siguiente a su "{". Devuelve la línea siguiente a su "}".
    private static int ParseBlock(List<string[]> lines, int i, ShaderDef def)
    {
        for (; i < lines.Count; i++)
        {
            var p = lines[i];
            if (p[0] == "}") return i + 1;
            if (p[0] == "{")
            {
                var stage = new ShaderStage();
                i = ParseStage(lines, i + 1, stage) - 1;
                def.Stages.Add(stage);
                continue;
            }
            var key = p[0].ToLowerInvariant();
            switch (key)
            {
                case "surfaceparm" when p.Length > 1:
                    def.SurfaceParms.Add(p[1]);
                    break;
                case "qer_editorimage" when p.Length > 1:
                    def.EditorImage ??= NormPath(p[1]);
                    break;
                case "skyparms" when p.Length > 1:
                    if (p[1] != "-") def.SkyEnv ??= NormPath(p[1]);
                    if (p.Length > 2 && p[2] != "-") def.SkyCloudHeight = F(p[2]);
                    break;
                case "sunfile" when p.Length > 1:
                    def.SunFile = p[1].ToLowerInvariant();
                    break;
                case "cull" when p.Length > 1:
                    def.Cull = p[1].ToLowerInvariant() switch
                    {
                        "none" or "disable" or "twosided" => ShaderCull.None,
                        "back" or "backside" or "backsided" => ShaderCull.Back,
                        _ => ShaderCull.Front,
                    };
                    break;
                case "polygonoffset":
                    def.PolygonOffset = true;
                    break;
                case "nomipmaps":
                    def.NoMipmaps = true;
                    break;
                case "nopicmip":
                    def.NoPicmip = true;
                    break;
                case "radialnormals":
                    def.RadialNormals = true;
                    break;
                case "sort" when p.Length > 1:
                    def.Sort = p[1].ToLowerInvariant();
                    break;
                case "deformvertexes" when p.Length > 1:
                    def.Deforms.Add(p.Skip(1).Select(x => x.ToLowerInvariant()).ToArray());
                    break;
            }
        }
        return i;
    }

    // Etapa a partir de la línea siguiente a su "{". Devuelve la línea siguiente a su "}".
    private static int ParseStage(List<string[]> lines, int i, ShaderStage stage)
    {
        var bundle = stage.Main;
        for (; i < lines.Count; i++)
        {
            var p = lines[i];
            if (p[0] == "}") return i + 1;
            if (p[0] == "{") { i = SkipBlock(lines, i) - 1; continue; }
            var key = p[0].ToLowerInvariant();
            switch (key)
            {
                case "nextbundle":
                    bundle = new TextureBundle();
                    stage.Bundles.Add(bundle);
                    break;
                case "map" or "clampmap" when p.Length > 1:
                    ParseMap(p, bundle, key == "clampmap");
                    break;
                case "animmap" when p.Length > 2:
                    bundle.AnimFrequency = F(p[1]);
                    foreach (var f in p.Skip(2)) bundle.AnimFrames.Add(NormPath(f));
                    break;
                case "blendfunc" when p.Length > 1:
                    // Tras nextbundle describe la combinación con el bundle anterior (p. ej. el lightmap
                    // con "filter"), no la mezcla de la etapa: terrain.shader a_grass1b lleva "blend"
                    // en la etapa y "filter" en el bundle del lightmap.
                    if (bundle == stage.Main) ParseBlend(p, stage);
                    else
                    {
                        var tmp = new ShaderStage();
                        ParseBlend(p, tmp);
                        bundle.CombineSrc = tmp.BlendSrc;
                        bundle.CombineDst = tmp.BlendDst;
                    }
                    break;
                case "alphafunc" when p.Length > 1:
                    stage.AlphaFunc = p[1].ToUpperInvariant() switch
                    {
                        "GT0" => ShaderAlphaFunc.Gt0,
                        "LT128" => ShaderAlphaFunc.Lt128,
                        "GE128" => ShaderAlphaFunc.Ge128,
                        _ => ShaderAlphaFunc.None,
                    };
                    break;
                case "rgbgen" when p.Length > 1:
                    ParseRgbGen(p, stage);
                    break;
                case "alphagen" when p.Length > 1:
                    ParseAlphaGen(p, stage);
                    break;
                case "tcmod" when p.Length > 1:
                    var mod = ParseTcMod(p);
                    if (mod is not null) bundle.TcMods.Add(mod);
                    break;
                case "tcgen" or "texgen" when p.Length > 1:
                    bundle.TcGen = string.Join(' ', p.Skip(1)).ToLowerInvariant();
                    break;
                case "depthwrite":
                    stage.DepthWrite = true;
                    break;
                case "depthfunc" when p.Length > 1:
                    stage.DepthFunc = p[1].ToLowerInvariant();
                    break;
            }
        }
        return i;
    }

    // "map [modificadores] <ruta>": modificadores de CoD1 antes de la ruta (cadenas del ejecutable:
    // clamp, clampx/clamps, clampy/clampt, nomipmaps, mipmaps, picmip, nopicmip, noopt, only16bit,
    // only32bit, heightToNormal). La ruta es el último argumento.
    private static void ParseMap(string[] p, TextureBundle b, bool clampMap)
    {
        if (clampMap) b.Clamp = TextureClamp.Both;
        for (var k = 1; k < p.Length - 1; k++)
        {
            switch (p[k].ToLowerInvariant())
            {
                case "clamp": b.Clamp = TextureClamp.Both; break;
                case "clampx" or "clamps": b.Clamp = b.Clamp == TextureClamp.Y ? TextureClamp.Both : TextureClamp.X; break;
                case "clampy" or "clampt": b.Clamp = b.Clamp == TextureClamp.X ? TextureClamp.Both : TextureClamp.Y; break;
                case "nomipmaps": b.NoMipmaps = true; break;
            }
        }
        var last = p[^1];
        b.Map = last.StartsWith('$') || last.StartsWith('*') ? last.ToLowerInvariant() : NormPath(last);
    }

    private static void ParseBlend(string[] p, ShaderStage s)
    {
        if (p.Length == 2)
        {
            switch (p[1].ToLowerInvariant())
            {
                case "blend": s.BlendSrc = BlendFactor.SrcAlpha; s.BlendDst = BlendFactor.OneMinusSrcAlpha; return;
                case "add": s.BlendSrc = BlendFactor.One; s.BlendDst = BlendFactor.One; return;
                case "filter" or "multiply": s.BlendSrc = BlendFactor.DstColor; s.BlendDst = BlendFactor.Zero; return;
                default: return; // valores no válidos que aparecen en los datos (p. ej. "blendfunc GE128")
            }
        }
        var src = Factor(p[1]);
        var dst = Factor(p[2]);
        if (src is null || dst is null) return;
        s.BlendSrc = src;
        s.BlendDst = dst;
    }

    private static BlendFactor? Factor(string s) => s.ToUpperInvariant() switch
    {
        "GL_ZERO" => BlendFactor.Zero,
        "GL_ONE" => BlendFactor.One,
        "GL_SRC_COLOR" => BlendFactor.SrcColor,
        "GL_ONE_MINUS_SRC_COLOR" => BlendFactor.OneMinusSrcColor,
        "GL_DST_COLOR" => BlendFactor.DstColor,
        "GL_ONE_MINUS_DST_COLOR" => BlendFactor.OneMinusDstColor,
        "GL_SRC_ALPHA" => BlendFactor.SrcAlpha,
        "GL_ONE_MINUS_SRC_ALPHA" => BlendFactor.OneMinusSrcAlpha,
        "GL_DST_ALPHA" => BlendFactor.DstAlpha,
        "GL_ONE_MINUS_DST_ALPHA" => BlendFactor.OneMinusDstAlpha,
        "GL_SRC_ALPHA_SATURATE" => BlendFactor.SrcAlphaSaturate,
        _ => null,
    };

    private static void ParseRgbGen(string[] p, ShaderStage s)
    {
        switch (p[1].ToLowerInvariant())
        {
            case "identity": s.RgbGen = RgbGen.Identity; break;
            case "identitylighting": s.RgbGen = RgbGen.IdentityLighting; break;
            case "vertex": s.RgbGen = RgbGen.Vertex; break;
            case "exactvertex": s.RgbGen = RgbGen.ExactVertex; break;
            case "lightingdiffuse": s.RgbGen = RgbGen.LightingDiffuse; break;
            case "lightingprecalc": s.RgbGen = RgbGen.LightingPrecalc; break;
            case "const" or "constant":
                s.RgbGen = RgbGen.Const;
                s.RgbConst = Vector(p, 2);
                break;
            case "constlighting":
                s.RgbGen = RgbGen.ConstLighting;
                s.RgbConst = Vector(p, 2);
                break;
            case "wave":
                s.RgbGen = RgbGen.Wave;
                s.RgbWave = Wave(p, 2);
                break;
            default: s.RgbGen = RgbGen.Other; break;
        }
    }

    private static void ParseAlphaGen(string[] p, ShaderStage s)
    {
        switch (p[1].ToLowerInvariant())
        {
            case "identity": s.AlphaGen = AlphaGen.Identity; break;
            case "vertex": s.AlphaGen = AlphaGen.Vertex; break;
            case "const" or "constant":
                s.AlphaGen = AlphaGen.Const;
                s.AlphaConst = p.Length > 2 ? F(p[2]) : 1;
                break;
            case "wave":
                s.AlphaGen = AlphaGen.Wave;
                s.AlphaWave = Wave(p, 2);
                break;
            default: s.AlphaGen = AlphaGen.Other; break;
        }
    }

    private static TcMod? ParseTcMod(string[] p)
    {
        float A(int k) => k < p.Length ? F(p[k]) : 0;
        return p[1].ToLowerInvariant() switch
        {
            "scroll" => new TcMod(TcModKind.Scroll, new[] { A(2), A(3) }),
            "scale" => new TcMod(TcModKind.Scale, new[] { A(2), A(3) }),
            "rotate" => new TcMod(TcModKind.Rotate, new[] { A(2) }),
            "transform" => new TcMod(TcModKind.Transform, new[] { A(2), A(3), A(4), A(5), A(6), A(7) }),
            // turb [función] <base> <amplitud> <fase> <frecuencia> (la función, si aparece, se ignora: siempre seno)
            "turb" => Turb(p),
            "stretch" => new TcMod(TcModKind.Stretch, Array.Empty<float>(), Wave(p, 2)),
            _ => null,
        };
    }

    private static TcMod Turb(string[] p)
    {
        var k = p.Length > 2 && !IsNumber(p[2]) ? 3 : 2;
        float A(int j) => j < p.Length ? F(p[j]) : 0;
        return new TcMod(TcModKind.Turb, Array.Empty<float>(), new ShaderWave(WaveFunc.Sin, A(k), A(k + 1), A(k + 2), A(k + 3)));
    }

    private static ShaderWave Wave(string[] p, int k)
    {
        var func = (k < p.Length ? p[k].ToLowerInvariant() : "sin") switch
        {
            "triangle" => WaveFunc.Triangle,
            "square" => WaveFunc.Square,
            "sawtooth" => WaveFunc.Sawtooth,
            "inversesawtooth" => WaveFunc.InverseSawtooth,
            "noise" => WaveFunc.Noise,
            _ => WaveFunc.Sin,
        };
        float A(int j) => j < p.Length ? F(p[j]) : 0;
        return new ShaderWave(func, A(k + 1), A(k + 2), A(k + 3), A(k + 4));
    }

    // "( r g b )" o "r g b"
    private static float[] Vector(string[] p, int k)
    {
        var nums = p.Skip(k).Where(IsNumber).Take(3).Select(F).ToArray();
        return nums.Length == 3 ? nums : new float[] { 1, 1, 1 };
    }

    private static bool IsNumber(string s) =>
        float.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out _);

    private static float F(string s) =>
        float.TryParse(s.TrimEnd('f', 'F'), NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : 0;

    private static string NormPath(string s) => s.Replace('\\', '/').ToLowerInvariant();
}
