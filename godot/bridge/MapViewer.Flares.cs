using System.Collections.Generic;
using Foley.Sim.Collision;
using Godot;

/// <summary>
/// Coronas del mapa y destello del sol o la luna. Claves de la entidad <c>corona</c> según las cadenas
/// del renderer de CoDSP.exe (junto a "corona scale must be > 0"): <c>scale</c>, <c>zcutoff</c>,
/// <c>zfadeout</c>, color (<c>_color</c>; <c>dl_color</c> si existe), dibujada con <c>flareShader</c>.
/// </summary>
public partial class MapViewer
{
	private void BuildFlares(MapBuilder map)
	{
		var coronas = new List<Flares.Corona>();
		foreach (var e in map.Bsp.Entities)
		{
			if (e.ClassName != "corona" || !MapBuilder.TryParseVector(e.Get("origin"), out var o)) continue;
			var c = MapBuilder.LightColor(e.Get("dl_color") ?? e.Get("_color"), Vector3.One);
			var corona = new Flares.Corona
			{
				SimOrigin = o,
				Color = new Color(c.X, c.Y, c.Z),
				Scale = MapBuilder.Num(e.Get("scale"), 1f),
			};
			if (e.Get("zcutoff") is { } zc && e.Get("zfadeout") is { } zf)
			{
				corona.ZCutoff = MapBuilder.Num(zc, 0);
				corona.ZFadeOut = MapBuilder.Num(zf, 0);
			}
			coronas.Add(corona);
		}
		if (coronas.Count == 0 && map.SunFlareTexture is null) return;

		var flares = new Flares { Name = "Destellos" };
		flares.Setup(ClipMap.FromBsp(map.Bsp), coronas, map.CoronaTexture, map.SunDirection, map.SunFile, map.SunFlareTexture);
		var layer = new CanvasLayer { Layer = -1 };
		layer.AddChild(flares);
		AddChild(layer);
		GD.Print($"[MapViewer] destellos: {coronas.Count} coronas, destello del astro {(map.SunFlareTexture is null ? "no" : "sí")}");
	}
}
