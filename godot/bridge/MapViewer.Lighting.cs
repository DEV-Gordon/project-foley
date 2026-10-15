using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Foley.Formats.Bsp;
using Godot;

/// <summary>
/// Iluminación nueva del mapa (CLAUDE.md, "Iluminación"): sin lightmaps; luces de Godot guiadas por las
/// entidades del BSP y GI en tiempo real. Ajustes artísticos en <c>res://render/maps/&lt;mapa&gt;.tres</c>.
/// Ver docs/decisions/0003-iluminacion.md.
/// </summary>
public partial class MapViewer
{
	private void AddLighting(BspFile bsp)
	{
		var settings = LoadLightingSettings();

		// Entorno: el de la escena si existe (conserva sus efectos de postproceso), si no uno nuevo.
		WorldEnvironment? sceneEnvironment = null;
		var sceneHasSun = false;
		foreach (var child in GetChildren())
		{
			if (child is WorldEnvironment we) sceneEnvironment ??= we;
			else if (child is DirectionalLight3D) sceneHasSun = true;
		}
		if (sceneEnvironment is null)
		{
			sceneEnvironment = new WorldEnvironment();
			AddChild(sceneEnvironment);
		}
		var env = sceneEnvironment.Environment ??= new Godot.Environment();
		_environment = env;

		var world = bsp.Entities.Count > 0 ? bsp.Entities[0] : null;

		// Cielo original.
		if (_sky is not null)
		{
			env.BackgroundMode = Godot.Environment.BGMode.Sky;
			env.Sky = _sky;
		}

		// Ambiente del worldspawn: ambient · _color + luz difusa del cielo (sundiffusecolor · sunlight · diffusefraction).
		// El renderer de CoDSP.exe avisa "ambient too big, assuming it uses the old 0-255 scale instead of
		// the proper 0-1 scale": un ambient mayor que 1 se divide por 255.
		var ambient = Num(world?.Get("ambient"), 0f);
		if (ambient > 1f) ambient /= 255f;
		var ambientColor = LightColor(world?.Get("_color"), Vector3.One) * ambient
			+ LightColor(world?.Get("sundiffusecolor"), Vector3.Zero) * Num(world?.Get("sunlight"), 0f) * Num(world?.Get("diffusefraction"), 0f);
		env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		env.AmbientLightColor = new Color(ambientColor.X, ambientColor.Y, ambientColor.Z);
		env.AmbientLightEnergy = settings.AmbientScale;
		env.TonemapExposure = settings.Exposure;
		env.SdfgiEnabled = settings.Sdfgi;
		env.SdfgiEnergy = settings.SdfgiEnergy;
		env.SdfgiUseOcclusion = true;

		// Sol o luna: sundirection, suncolor, sunlight.
		if (!sceneHasSun && _sunDirection is { } toSun && world is not null)
		{
			var c = LightColor(world.Get("suncolor"), Vector3.One);
			var sun = new DirectionalLight3D
			{
				Name = "Sol",
				LightColor = new Color(c.X, c.Y, c.Z) * settings.SunTint,
				LightEnergy = Num(world.Get("sunlight"), 1f) * settings.SunEnergyScale,
				ShadowEnabled = settings.SunShadows,
				LightAngularDistance = settings.SunAngularDistance,
			};
			AddChild(sun);
			sun.LookAt(sun.GlobalPosition - toSun, Mathf.Abs(toSun.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
		}

		// Entidades light: puntuales y focos (con target → info_null).
		var targets = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
		foreach (var e in bsp.Entities)
			if (e.Get("targetname") is { } tn && TryParseVector(e.Get("origin"), out var to)) targets[tn] = to;

		int points = 0, spots = 0;
		foreach (var e in bsp.Entities.Where(e => e.ClassName == "light"))
		{
			if (!TryParseVector(e.Get("origin"), out var o)) continue;
			var intensity = Num(e.Get("light"), 300f); // valor por defecto de q3map: 300
			var c = LightColor(e.Get("_color"), Vector3.One);
			var color = new Color(c.X, c.Y, c.Z);
			var pos = ToGodot(o.X, o.Y, o.Z) * MetersPerUnit;

			if (e.Get("target") is { } target && targets.TryGetValue(target, out var t))
			{
				// Foco: cono con radio "radius" (64 por defecto en q3map) a la distancia del destino.
				var dir = t - o;
				var dist = dir.Length();
				var radius = Num(e.Get("radius"), 64f);
				var spot = new SpotLight3D
				{
					Name = "Foco",
					LightColor = color,
					LightEnergy = settings.SpotEnergy,
					SpotRange = Mathf.Max(intensity, dist * 1.5f) * MetersPerUnit * settings.RangeScale,
					SpotAngle = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(radius, Mathf.Max(dist, 1f))), 1f, 89f),
					ShadowEnabled = settings.SpotShadows,
					Position = pos,
				};
				AddChild(spot);
				var g = ToGodot(dir.X, dir.Y, dir.Z).Normalized();
				spot.LookAt(spot.GlobalPosition + g, Mathf.Abs(g.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
				spots++;
			}
			else
			{
				AddChild(new OmniLight3D
				{
					Name = "Luz",
					LightColor = color,
					LightEnergy = settings.PointEnergy,
					OmniRange = intensity * MetersPerUnit * settings.RangeScale,
					ShadowEnabled = settings.PointShadows,
					Position = pos,
				});
				points++;
			}
		}
		GD.Print($"[MapViewer] luces: {points} puntuales, {spots} focos, sol {(sceneHasSun ? "de la escena" : _sunDirection is null ? "no" : "del mapa")}, SDFGI {(env.SdfgiEnabled ? "sí" : "no")}");
	}

	/// <summary>El .tres del mapa; si no hay, <c>default.tres</c>; si tampoco, los valores del código.</summary>
	private MapLighting LoadLightingSettings()
	{
		var name = System.IO.Path.GetFileNameWithoutExtension(MapPath);
		foreach (var path in new[] { $"res://render/maps/{name}.tres", "res://render/maps/default.tres" })
		{
			if (ResourceLoader.Exists(path) && ResourceLoader.Load(path) is MapLighting m)
			{
				GD.Print($"[MapViewer] iluminación: {path}");
				return m;
			}
		}
		GD.Print("[MapViewer] iluminación: valores por defecto del código");
		return new MapLighting();
	}

	/// <summary>
	/// Color de luz de una entidad, normalizado como ColorNormalize de q3map (divide por la componente
	/// mayor): los mapas lo escriben en escala 0-1 o 0-255 (pavlov: "100 100 105") y la intensidad la
	/// pone solo sunlight / light.
	/// </summary>
	private static Vector3 LightColor(string? s, Vector3 fallback)
	{
		var v = Vec(s, fallback);
		var max = Mathf.Max(v.X, Mathf.Max(v.Y, v.Z));
		return max > 0 ? v / max : v;
	}

	private static float Num(string? s, float fallback) =>
		s is not null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

	private static Vector3 Vec(string? s, Vector3 fallback) => TryParseVector(s, out var v) ? v : fallback;
}
