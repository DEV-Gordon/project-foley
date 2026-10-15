using System;
using System.Globalization;
using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;
using Godot;

/// <summary>
/// Cielo original: la caja de <c>skyParms</c> del shader de cielo del mapa (6 caras en <c>env/</c>) y el
/// sprite de la luna o el sol de su <c>sunfile</c>, en la dirección <c>sundirection</c> del worldspawn.
/// Solo presentación. Ver docs/formats/shader.md ("Cielo").
/// </summary>
public partial class MapViewer
{
	private Sky? _sky;
	private SunFile? _sunFile;
	private Texture2D? _sunFlareTexture;
	/// <summary>Dirección hacia el sol o la luna en ejes de Godot (unitaria), o null si el mapa no la define.</summary>
	private Vector3? _sunDirection;

	private void BuildSky(BspFile bsp, Pk3Collection pk3, MaterialResolver resolver)
	{
		_sunDirection = SunDirection(bsp);

		ShaderDef? skyShader = null;
		foreach (var s in bsp.Surfaces)
		{
			var r = resolver.Resolve(bsp.Materials[s.MaterialIndex].Name);
			if (r.IsSky && r.Shader.SkyEnv is not null) { skyShader = r.Shader; break; }
		}
		if (skyShader is null) return;

		// Orden de id Tech 3 (tr_sky.c: suf[] y sky_texorder[]): +X rt, -X lf, +Y bk, -Y ft, +Z up, -Z dn.
		var mat = new ShaderMaterial { Shader = new Shader { Code = SkyCode } };
		foreach (var face in new[] { "rt", "lf", "bk", "ft", "up", "dn" })
		{
			var file = resolver.FindTexture(skyShader.SkyEnv + "_" + face);
			var tex = file is null ? null : LoadTexture(pk3, file);
			if (tex is null) { GD.PushWarning($"[MapViewer] falta la cara {face} del cielo {skyShader.SkyEnv}"); return; }
			mat.SetShaderParameter(face, tex);
		}

		// Sprite del sol o la luna (sunfile → r_sunsprite_shader).
		if (skyShader.SunFile is { } sunName && pk3.Contains($"scripts/{sunName}.sun") && _sunDirection is { } dir)
		{
			var sun = SunFile.Parse(pk3.ReadAllText($"scripts/{sunName}.sun"));
			_sunFile = sun;
			// Destello (r_sunflare_shader): lo dibuja la capa Flares.
			if (sun.Get("r_sunflare_shader") is { } flareName && resolver.FindShader(flareName)?.FirstMap is { } flareMap
				&& resolver.FindTexture(flareMap) is { } flareFile)
				_sunFlareTexture = LoadTexture(pk3, flareFile);
			var spriteShader = sun.Get("r_sunsprite_shader");
			var sprite = spriteShader is null ? null : resolver.FindShader(spriteShader);
			var file = sprite?.FirstMap is { } m ? resolver.FindTexture(m) : null;
			var tex = file is null ? null : LoadTexture(pk3, file);
			if (tex is not null)
			{
				// TODO(presentación): r_sunsprite_size (32 en burnville) se interpreta como píxeles de una
				// pantalla de 640 con 80° de campo horizontal (0.125° por píxel). Supuesto sin verificar.
				var diameterDeg = sun.GetFloat("r_sunsprite_size", 32) * 0.125f;
				mat.SetShaderParameter("sprite", tex);
				mat.SetShaderParameter("has_sprite", true);
				mat.SetShaderParameter("sprite_dir", dir);
				mat.SetShaderParameter("sprite_radius", Mathf.DegToRad(diameterDeg * 0.5f));
			}
		}

		_sky = new Sky { SkyMaterial = mat, ProcessMode = Sky.ProcessModeEnum.Realtime };
	}

	/// <summary>
	/// <c>sundirection</c> del worldspawn (pitch yaw roll) como vector hacia el astro, con AngleVectors:
	/// pitch negativo = sobre el horizonte.
	/// </summary>
	private static Vector3? SunDirection(BspFile bsp)
	{
		if (bsp.Entities.Count == 0 || !TryParseVector(bsp.Entities[0].Get("sundirection"), out var a)) return null;
		var p = Mathf.DegToRad(a.X);
		var y = Mathf.DegToRad(a.Y);
		var forward = new Vector3(Mathf.Cos(p) * Mathf.Cos(y), Mathf.Cos(p) * Mathf.Sin(y), -Mathf.Sin(p));
		return ToGodot(forward.X, forward.Y, forward.Z).Normalized();
	}

	private const string SkyCode = @"
shader_type sky;
uniform sampler2D rt : source_color, filter_linear, repeat_disable;
uniform sampler2D lf : source_color, filter_linear, repeat_disable;
uniform sampler2D bk : source_color, filter_linear, repeat_disable;
uniform sampler2D ft : source_color, filter_linear, repeat_disable;
uniform sampler2D up : source_color, filter_linear, repeat_disable;
uniform sampler2D dn : source_color, filter_linear, repeat_disable;
uniform sampler2D sprite : source_color, filter_linear, repeat_disable;
uniform bool has_sprite = false;
uniform vec3 sprite_dir = vec3(0.0, 1.0, 0.0);
uniform float sprite_radius = 0.035;

// Coordenadas (s, t) de id Tech 3 en [-1, 1] → UV de la imagen (t hacia arriba = v hacia 0).
vec2 st_uv(vec2 st) { return vec2((st.x + 1.0) * 0.5, 1.0 - (st.y + 1.0) * 0.5); }

void sky() {
	// Dirección en ejes del juego: Godot (x, y, z) = juego (x, z, -y).
	vec3 d = vec3(EYEDIR.x, -EYEDIR.z, EYEDIR.y);
	vec3 a = abs(d);
	vec3 c;
	// tr_sky.c st_to_vec: +X (dist, -s, t), -X (-dist, s, t), +Y (s, dist, t), -Y (-s, -dist, t),
	// +Z (-t, -s, dist), -Z (t, -s, -dist).
	if (a.x >= a.y && a.x >= a.z) {
		if (d.x > 0.0) c = texture(rt, st_uv(vec2(-d.y, d.z) / a.x)).rgb;
		else c = texture(lf, st_uv(vec2(d.y, d.z) / a.x)).rgb;
	} else if (a.y >= a.z) {
		if (d.y > 0.0) c = texture(bk, st_uv(vec2(d.x, d.z) / a.y)).rgb;
		else c = texture(ft, st_uv(vec2(-d.x, d.z) / a.y)).rgb;
	} else {
		if (d.z > 0.0) c = texture(up, st_uv(vec2(-d.y, -d.x) / a.z)).rgb;
		else c = texture(dn, st_uv(vec2(-d.y, d.x) / a.z)).rgb;
	}

	// Sprite del astro: cuadrado orientado hacia la cámara en sprite_dir, mezcla aditiva (blendFunc add).
	if (has_sprite) {
		vec3 n = normalize(sprite_dir);
		float cosang = dot(EYEDIR, n);
		if (cosang > 0.0) {
			vec3 right = normalize(cross(n, abs(n.y) < 0.99 ? vec3(0.0, 1.0, 0.0) : vec3(1.0, 0.0, 0.0)));
			vec3 upv = cross(right, n);
			vec3 p = EYEDIR / cosang - n;
			vec2 uv = vec2(dot(p, right), -dot(p, upv)) / tan(sprite_radius) * 0.5 + 0.5;
			if (uv.x >= 0.0 && uv.x <= 1.0 && uv.y >= 0.0 && uv.y <= 1.0)
				c += texture(sprite, uv).rgb;
		}
	}
	COLOR = c;
}
";
}
