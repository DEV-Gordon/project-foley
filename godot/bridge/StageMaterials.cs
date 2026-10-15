using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Foley.Formats.Materials;
using Godot;

/// <summary>
/// Traduce un shader del juego (etapas de id Tech 3 / CoD1) a materiales de Godot.
/// Solo presentación. Ver docs/formats/shader.md, sección "Traducción a Godot".
/// - Las etapas y bundles de <c>$lightmap</c> se descartan: la luz es la de Godot (CLAUDE.md, Iluminación).
/// - Primera etapa = material; las siguientes, <see cref="Material.NextPass"/> en orden.
/// - Mezclas, alphaFunc, rgbGen/alphaGen, tcMod, animMap, clamp por eje, cull, polygonOffset.
/// - deformVertexes autosprite y autosprite2 (la malla debe traer CUSTOM0/CUSTOM1, ver
///   <see cref="AutospriteData"/> y <see cref="Autosprite2Data"/>).
/// - sort → prioridad de dibujado de las superficies mezcladas (<see cref="SortValue"/>).
/// - Sin traducir todavía: tcGen (environment, vector), el resto de deformVertexes, depthFunc.
/// </summary>
public sealed class StageMaterials
{
	private const int MaxTcMods = 4;
	private const int MaxAnimFrames = 8;

	private readonly Func<string, Texture2D?> _loadTexture;
	private readonly Dictionary<string, Shader> _shaders = new();

	public StageMaterials(Func<string, Texture2D?> loadTexture) => _loadTexture = loadTexture;

	/// <summary>Material de Godot del shader, o null si no tiene ninguna etapa dibujable.</summary>
	public Material? Build(ShaderDef def, bool lit = true)
	{
		Material? first = null;
		Material? last = null;
		var priority = 0;
		foreach (var stage in def.Stages)
		{
			if (stage.IsLightmapOnly) continue;
			var m = BuildStage(def, stage, lit, (SortValue(def.Sort) - 9) * 8 + priority++);
			if (m is null) continue;
			if (first is null) first = m;
			else last!.NextPass = m;
			last = m;
		}
		return first;
	}

	private ShaderMaterial? BuildStage(ShaderDef def, ShaderStage stage, bool lit, int priority)
	{
		var b = stage.Main;
		var frames = b.IsAnimated ? b.AnimFrames : (b.Map is null ? new List<string>() : new List<string> { b.Map });
		var textures = new List<Texture2D>();
		foreach (var f in frames.Take(MaxAnimFrames))
		{
			var tex = f.StartsWith('$') || f.StartsWith('*') ? null : _loadTexture(f);
			if (tex is not null) textures.Add(tex);
		}
		var white = frames.Count == 0 || frames[0] is "$whiteimage" or "*white";
		if (textures.Count == 0 && !white) return null;

		var blend = Blend(stage);
		var noMip = def.NoMipmaps || b.NoMipmaps;
		var unshaded = !lit || blend is BlendKind.Add or BlendKind.Mul;
		var deform = Deform(def);
		var key = $"{blend}|{def.Cull}|{stage.DepthWrite}|{noMip}|{unshaded}|{textures.Count}|{deform}";
		if (!_shaders.TryGetValue(key, out var shader))
		{
			// Los sprites se reconstruyen mirando a la cámara: se dibujan por las dos caras.
			var cull = deform == SpriteDeform.None ? def.Cull : ShaderCull.None;
			shader = new Shader { Code = Code(blend, cull, stage.DepthWrite, noMip, unshaded, Math.Max(1, textures.Count), deform) };
			_shaders[key] = shader;
		}

		var m = new ShaderMaterial { Shader = shader, RenderPriority = priority };
		for (var i = 0; i < textures.Count; i++) m.SetShaderParameter(i == 0 ? "tex" : $"frame{i}", textures[i]);
		m.SetShaderParameter("use_white", white);
		m.SetShaderParameter("frame_count", Math.Max(1, textures.Count));
		m.SetShaderParameter("anim_freq", b.AnimFrequency);
		m.SetShaderParameter("clamp_uv", new Vector2I(
			b.Clamp is TextureClamp.Both or TextureClamp.X ? 1 : 0,
			b.Clamp is TextureClamp.Both or TextureClamp.Y ? 1 : 0));
		m.SetShaderParameter("alpha_func", (int)stage.AlphaFunc);
		m.SetShaderParameter("polygon_offset", def.PolygonOffset ? 1.0f : 0.0f);
		m.SetShaderParameter("invert_blend_alpha", blend == BlendKind.MixInverse);

		// rgbGen
		var rgb = stage.RgbGen switch
		{
			RgbGen.Vertex or RgbGen.ExactVertex => 1,
			RgbGen.Const or RgbGen.ConstLighting => 2,
			RgbGen.Wave => 3,
			_ => 0,
		};
		m.SetShaderParameter("rgb_gen", rgb);
		m.SetShaderParameter("rgb_const", new Vector3(stage.RgbConst[0], stage.RgbConst[1], stage.RgbConst[2]));
		SetWave(m, "rgb_wave", stage.RgbWave);

		// alphaGen
		var alpha = stage.AlphaGen switch
		{
			AlphaGen.Vertex => 1,
			AlphaGen.Const => 2,
			AlphaGen.Wave => 3,
			_ => 0,
		};
		m.SetShaderParameter("alpha_gen", alpha);
		m.SetShaderParameter("alpha_const", stage.AlphaConst);
		SetWave(m, "alpha_wave", stage.AlphaWave);

		// tcMod, en orden
		var kinds = new int[MaxTcMods];
		var pa = new Vector4[MaxTcMods];
		var pb = new Vector4[MaxTcMods];
		var n = Math.Min(MaxTcMods, b.TcMods.Count);
		for (var i = 0; i < n; i++)
		{
			var t = b.TcMods[i];
			var a = t.Args;
			float A(int k) => k < a.Length ? a[k] : 0;
			kinds[i] = (int)t.Kind + 1;
			if (t.Wave is { } w)
			{
				pa[i] = new Vector4(w.Base, w.Amplitude, w.Phase, w.Frequency);
				pb[i] = new Vector4((int)w.Func, 0, 0, 0);
			}
			else
			{
				pa[i] = new Vector4(A(0), A(1), A(2), A(3));
				pb[i] = new Vector4(A(4), A(5), 0, 0);
			}
		}
		m.SetShaderParameter("tc_count", n);
		m.SetShaderParameter("tc_kind", kinds);
		m.SetShaderParameter("tc_a", pa);
		m.SetShaderParameter("tc_b", pb);
		return m;
	}

	private static void SetWave(ShaderMaterial m, string name, ShaderWave? wave)
	{
		var w = wave ?? new ShaderWave(WaveFunc.Sin, 1, 0, 0, 0);
		m.SetShaderParameter(name, new Vector4(w.Base, w.Amplitude, w.Phase, w.Frequency));
		m.SetShaderParameter(name + "_func", (int)w.Func);
	}

	private enum BlendKind { Opaque, Mix, MixInverse, Add, AddAlpha, Mul, Premul }

	/// <summary>blendFunc de OpenGL → modo de mezcla de Godot (los que no tienen equivalente, al más parecido).</summary>
	private static BlendKind Blend(ShaderStage s)
	{
		if (!s.IsBlended) return BlendKind.Opaque;
		return (s.BlendSrc, s.BlendDst) switch
		{
			(BlendFactor.SrcAlpha, BlendFactor.OneMinusSrcAlpha) => BlendKind.Mix,
			(BlendFactor.OneMinusSrcAlpha, BlendFactor.SrcAlpha) => BlendKind.MixInverse,
			(BlendFactor.One, BlendFactor.One) => BlendKind.Add,
			(BlendFactor.SrcAlpha, BlendFactor.One) => BlendKind.AddAlpha,
			(BlendFactor.DstColor, BlendFactor.One) => BlendKind.Add,
			(BlendFactor.DstColor, BlendFactor.Zero) or (BlendFactor.Zero, BlendFactor.SrcColor) => BlendKind.Mul,
			(BlendFactor.One, BlendFactor.OneMinusSrcAlpha) => BlendKind.Premul,
			(BlendFactor.Zero, BlendFactor.OneMinusSrcColor) or (BlendFactor.Zero, BlendFactor.OneMinusSrcAlpha) => BlendKind.Mul,
			_ => BlendKind.Mix,
		};
	}

	public enum SpriteDeform { None, Autosprite, Autosprite2 }

	public static SpriteDeform Deform(ShaderDef def)
	{
		foreach (var d in def.Deforms)
		{
			if (d.Length == 0) continue;
			if (d[0] == "autosprite2") return SpriteDeform.Autosprite2;
			if (d[0] == "autosprite") return SpriteDeform.Autosprite;
		}
		return SpriteDeform.None;
	}

	/// <summary>
	/// Orden de dibujado de <c>sort</c>, con los valores de Quake 3 (tr_local.h, shaderSort_t) para las
	/// palabras que comparten y un hueco razonable para las de CoD1 (innerblend antes que outerblend,
	/// ambas antes que las aditivas). Sin <c>sort</c>: 9 (SS_BLEND0, el de las superficies mezcladas).
	/// TODO(presentación): valores de CoD1 sin verificar en el ejecutable.
	/// </summary>
	public static int SortValue(string? sort) => sort switch
	{
		"portal" => 1,
		"sky" => 2,
		"opaque" => 3,
		"decal" => 4,
		"seethrough" => 5,
		"banner" => 6,
		"underwater" or "water" or "ocean" or "boathull" => 8,
		"innerblend" => 9,
		"outerblend" => 10,
		"additive" => 11,
		"corona" => 15,
		"nearest" => 16,
		_ when sort is not null && int.TryParse(sort, out var n) => n,
		_ => 9,
	};

	/// <summary>
	/// Datos por vértice de autosprite (como AutospriteDeform de Quake 3): cada grupo de 4 vértices se
	/// sustituye por un cuadrado que mira a la cámara, centrado en su punto medio y de radio
	/// |v0 - medio| * 0.707. CUSTOM0 = punto medio (xyz) y radio (w); CUSTOM1.x = esquina 0..3.
	/// </summary>
	public static (float[] custom0, float[] custom1) AutospriteData(Vector3[] positions)
	{
		var c0 = new float[positions.Length * 4];
		var c1 = new float[positions.Length * 4];
		for (var q = 0; q + 3 < positions.Length; q += 4)
		{
			var mid = (positions[q] + positions[q + 1] + positions[q + 2] + positions[q + 3]) * 0.25f;
			var radius = (positions[q] - mid).Length() * 0.707f;
			for (var k = 0; k < 4; k++)
			{
				var v = q + k;
				c0[v * 4] = mid.X; c0[v * 4 + 1] = mid.Y; c0[v * 4 + 2] = mid.Z; c0[v * 4 + 3] = radius;
				c1[v * 4] = k;
			}
		}
		return (c0, c1);
	}

	/// <summary>Añade la superficie a la malla con los datos de la deformación de sprites del shader, si tiene.</summary>
	public static void AddSurface(ArrayMesh mesh, Godot.Collections.Array arrays, Vector3[] positions, SpriteDeform deform)
	{
		if (deform == SpriteDeform.None)
		{
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			return;
		}
		var (c0, c1) = deform == SpriteDeform.Autosprite ? AutospriteData(positions) : Autosprite2Data(positions);
		arrays[(int)Mesh.ArrayType.Custom0] = c0;
		arrays[(int)Mesh.ArrayType.Custom1] = c1;
		var rgbaFloat = (int)Mesh.ArrayCustomFormat.RgbaFloat;
		var flags = (Mesh.ArrayFormat)((rgbaFloat << 13) | (rgbaFloat << 16)); // ARRAY_FORMAT_CUSTOM0_SHIFT y CUSTOM1_SHIFT
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays, null, null, flags);
	}

	/// <summary>
	/// Datos por vértice de autosprite2 (como Autosprite2Deform de Quake 3): la superficie va en grupos
	/// de 4 vértices; los dos lados más cortos de cada grupo definen el eje largo. CUSTOM0 = punto medio
	/// del lado corto del vértice (xyz) y su media longitud con signo (w); CUSTOM1 = eje largo (xyz).
	/// </summary>
	public static (float[] custom0, float[] custom1) Autosprite2Data(Vector3[] positions)
	{
		var c0 = new float[positions.Length * 4];
		var c1 = new float[positions.Length * 4];
		for (var q = 0; q + 3 < positions.Length; q += 4)
		{
			// Las 6 parejas de vértices del grupo; las dos más cortas son los lados cortos.
			var pairs = new List<(int a, int b, float len)>();
			for (var i = 0; i < 4; i++)
				for (var j = i + 1; j < 4; j++)
					pairs.Add((q + i, q + j, positions[q + i].DistanceTo(positions[q + j])));
			pairs.Sort((x, y) => x.len.CompareTo(y.len));
			var e1 = pairs[0];
			var e2 = pairs.Skip(1).First(p => p.a != e1.a && p.a != e1.b && p.b != e1.a && p.b != e1.b);
			var m1 = (positions[e1.a] + positions[e1.b]) * 0.5f;
			var m2 = (positions[e2.a] + positions[e2.b]) * 0.5f;
			var major = (m2 - m1).Normalized();
			var normal = (positions[q + 1] - positions[q]).Cross(positions[q + 2] - positions[q]).Normalized();
			var side = major.Cross(normal);
			foreach (var (a, b) in new[] { (e1.a, e1.b), (e2.a, e2.b) })
			{
				var mid = (positions[a] + positions[b]) * 0.5f;
				foreach (var v in new[] { a, b })
				{
					var d = positions[v] - mid;
					var w = d.Length() * (d.Dot(side) >= 0 ? 1f : -1f);
					c0[v * 4] = mid.X; c0[v * 4 + 1] = mid.Y; c0[v * 4 + 2] = mid.Z; c0[v * 4 + 3] = w;
					c1[v * 4] = major.X; c1[v * 4 + 1] = major.Y; c1[v * 4 + 2] = major.Z;
				}
			}
		}
		return (c0, c1);
	}

	private static string Code(BlendKind blend, ShaderCull cull, bool depthWrite, bool noMip, bool unshaded, int frames, SpriteDeform deform)
	{
		var modes = new List<string>
		{
			blend switch
			{
				BlendKind.Add or BlendKind.AddAlpha => "blend_add",
				BlendKind.Mul => "blend_mul",
				BlendKind.Premul => "blend_premul_alpha",
				_ => "blend_mix",
			},
			cull switch { ShaderCull.None => "cull_disabled", ShaderCull.Back => "cull_front", _ => "cull_back" },
		};
		if (blend != BlendKind.Opaque) modes.Add(depthWrite ? "depth_draw_always" : "depth_draw_never");
		if (unshaded) modes.Add("unshaded");

		var hint = noMip ? "filter_linear" : "filter_linear_mipmap_anisotropic";
		var sb = new StringBuilder();
		sb.AppendLine("shader_type spatial;");
		if (deform == SpriteDeform.Autosprite2) sb.AppendLine("#define AUTOSPRITE2");
		if (deform == SpriteDeform.Autosprite) sb.AppendLine("#define AUTOSPRITE");
		sb.AppendLine("render_mode " + string.Join(", ", modes) + ";");
		sb.AppendLine($"uniform sampler2D tex : source_color, {hint}, repeat_enable;");
		for (var i = 1; i < frames; i++) sb.AppendLine($"uniform sampler2D frame{i} : source_color, {hint}, repeat_enable;");
		sb.Append(Body);
		sb.AppendLine("vec4 sample_tex(vec2 uv) {");
		sb.AppendLine("\tint f = frame_count > 1 ? int(mod(floor(TIME * anim_freq), float(frame_count))) : 0;");
		for (var i = 1; i < frames; i++) sb.AppendLine($"\tif (f == {i}) return texture(frame{i}, uv);");
		sb.AppendLine("\treturn texture(tex, uv);");
		sb.AppendLine("}");
		sb.Append(Fragment(blend));
		return sb.ToString();
	}

	// Uniformes y funciones comunes. Coordenadas del juego: Godot (x, y, z) = juego (x, z, -y) · 0.03.
	private const string Body = @"
uniform bool use_white = false;
uniform int frame_count = 1;
uniform float anim_freq = 0.0;
uniform ivec2 clamp_uv = ivec2(0);
uniform int alpha_func = 0;      // 0 ninguno, 1 GT0, 2 LT128, 3 GE128
uniform float polygon_offset = 0.0;
uniform bool invert_blend_alpha = false;
uniform int rgb_gen = 0;         // 0 identidad, 1 vértice, 2 constante, 3 onda
uniform vec3 rgb_const = vec3(1.0);
uniform vec4 rgb_wave = vec4(1.0, 0.0, 0.0, 0.0);
uniform int rgb_wave_func = 0;
uniform int alpha_gen = 0;       // 0 identidad, 1 vértice, 2 constante, 3 onda
uniform float alpha_const = 1.0;
uniform vec4 alpha_wave = vec4(1.0, 0.0, 0.0, 0.0);
uniform int alpha_wave_func = 0;
uniform int tc_count = 0;
uniform int tc_kind[4];          // 1 scroll, 2 scale, 3 rotate, 4 turb, 5 stretch, 6 transform
uniform vec4 tc_a[4];
uniform vec4 tc_b[4];

varying vec3 game_pos;

// Onda de id Tech 3 (base, amplitud, fase, frecuencia): sin, triangle, square, sawtooth, inversesawtooth.
float wave(vec4 w, int func) {
	float x = fract(w.z + TIME * w.w);
	float v;
	if (func == 1) v = x < 0.5 ? 4.0 * x - 1.0 : 3.0 - 4.0 * x;
	else if (func == 2) v = x < 0.5 ? 1.0 : -1.0;
	else if (func == 3) v = x;
	else if (func == 4) v = 1.0 - x;
	else v = sin(x * 6.28318530718);
	return w.x + w.y * v;
}

vec2 tcmods(vec2 st) {
	for (int i = 0; i < 4; i++) {
		if (i >= tc_count) break;
		int k = tc_kind[i];
		vec4 a = tc_a[i];
		if (k == 1) {
			st += a.xy * TIME;
		} else if (k == 2) {
			st *= a.xy;
		} else if (k == 3) {
			float r = radians(-a.x * TIME);
			vec2 c = st - 0.5;
			st = vec2(c.x * cos(r) - c.y * sin(r), c.x * sin(r) + c.y * cos(r)) + 0.5;
		} else if (k == 4) {
			// RB_CalcTurbulentTexCoords: desplazamiento según la posición y el tiempo
			float now = a.z + TIME * a.w;
			st.x += sin(((game_pos.x + game_pos.z) * (1.0 / 128.0) * 0.125 + now) * 6.28318530718) * a.y;
			st.y += sin((game_pos.y * (1.0 / 128.0) * 0.125 + now) * 6.28318530718) * a.y;
		} else if (k == 5) {
			float p = 1.0 / max(0.0001, wave(a, int(tc_b[i].x)));
			st = (st - 0.5) * p + 0.5;
		} else if (k == 6) {
			st = vec2(st.x * a.x + st.y * a.z + tc_b[i].x, st.x * a.y + st.y * a.w + tc_b[i].y);
		}
	}
	return st;
}

void vertex() {
#ifdef AUTOSPRITE
	// Cuadrado que mira a la cámara (RB_AddQuadStamp): esquinas izquierda+arriba, derecha+arriba,
	// derecha+abajo, izquierda+abajo, con UV (0,0) (1,0) (1,1) (0,1).
	mat3 to_model = mat3(inverse(MODEL_MATRIX));
	vec3 left = normalize(to_model * -INV_VIEW_MATRIX[0].xyz) * CUSTOM0.w;
	vec3 up = normalize(to_model * INV_VIEW_MATRIX[1].xyz) * CUSTOM0.w;
	int k = int(CUSTOM1.x + 0.5);
	vec2 sgn = k == 0 ? vec2(1.0, 1.0) : k == 1 ? vec2(-1.0, 1.0) : k == 2 ? vec2(-1.0, -1.0) : vec2(1.0, -1.0);
	VERTEX = CUSTOM0.xyz + left * sgn.x + up * sgn.y;
	UV = k == 0 ? vec2(0.0, 0.0) : k == 1 ? vec2(1.0, 0.0) : k == 2 ? vec2(1.0, 1.0) : vec2(0.0, 1.0);
#endif
#ifdef AUTOSPRITE2
	// La tira gira sobre su eje largo para mirar a la cámara.
	vec3 cam = (inverse(MODEL_MATRIX) * vec4(CAMERA_POSITION_WORLD, 1.0)).xyz;
	vec3 minor = normalize(cross(CUSTOM1.xyz, cam - CUSTOM0.xyz));
	VERTEX = CUSTOM0.xyz + minor * CUSTOM0.w;
#endif
	vec3 w = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz / 0.03;
	game_pos = vec3(w.x, -w.z, w.y);
	// polygonOffset: acerca la superficie a la cámara una fracción mínima para que no parpadee
	vec4 v = MODELVIEW_MATRIX * vec4(VERTEX, 1.0);
	POSITION = PROJECTION_MATRIX * vec4(v.xyz * (polygon_offset > 0.0 ? 0.9995 : 1.0), 1.0);
}
";

	private static string Fragment(BlendKind blend)
	{
		var sb = new StringBuilder();
		sb.AppendLine(@"
void fragment() {
	vec2 uv = tcmods(UV);
	if (clamp_uv.x == 1) uv.x = clamp(uv.x, 0.0, 1.0);
	if (clamp_uv.y == 1) uv.y = clamp(uv.y, 0.0, 1.0);
	vec4 c = use_white ? vec4(1.0) : sample_tex(uv);

	// El color de vértice del juego multiplica en espacio gamma; linealizarlo equivale a multiplicar
	// y luego linealizar el resultado.
	vec3 rgb = vec3(1.0);
	if (rgb_gen == 1) rgb = pow(COLOR.rgb, vec3(2.2));
	else if (rgb_gen == 2) rgb = pow(rgb_const, vec3(2.2));
	else if (rgb_gen == 3) rgb = vec3(pow(clamp(wave(rgb_wave, rgb_wave_func), 0.0, 1.0), 2.2));
	float a = c.a;
	if (alpha_gen == 1) a *= COLOR.a;
	else if (alpha_gen == 2) a = alpha_const;
	else if (alpha_gen == 3) a = clamp(wave(alpha_wave, alpha_wave_func), 0.0, 1.0);

	if (alpha_func == 1 && a <= 0.0) discard;
	if (alpha_func == 2 && a >= 0.5) discard;
	if (alpha_func == 3 && a < 0.5) discard;

	ALBEDO = c.rgb * rgb;
	// Las superficies de id Tech 3 solo tienen luz difusa: sin brillo especular.
	SPECULAR = 0.0;
	ROUGHNESS = 1.0;");
		switch (blend)
		{
			case BlendKind.Mix:
			case BlendKind.MixInverse:
				sb.AppendLine("\tALPHA = invert_blend_alpha ? 1.0 - a : a;");
				break;
			case BlendKind.AddAlpha:
				sb.AppendLine("\tALPHA = a;");
				break;
			case BlendKind.Add:
			case BlendKind.Mul:
				sb.AppendLine("\tALPHA = 1.0;");
				break;
			case BlendKind.Premul:
				sb.AppendLine("\tALPHA = a;");
				break;
		}
		sb.AppendLine("}");
		return sb.ToString();
	}
}
