using System.Collections.Generic;
using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;
using Foley.Sim.Collision;
using Godot;

/// <summary>
/// Coronas del mapa y destello del sol o la luna. Claves de la entidad <c>corona</c> según las cadenas
/// del renderer de CoDSP.exe (junto a "corona scale must be > 0"): <c>scale</c>, <c>zcutoff</c>,
/// <c>zfadeout</c>, color (<c>_color</c>; <c>dl_color</c> si existe), dibujada con <c>flareShader</c>.
/// </summary>
public partial class MapViewer
{
	private void BuildFlares(BspFile bsp, Pk3Collection pk3, MaterialResolver resolver)
	{
		var coronas = new List<Flares.Corona>();
		foreach (var e in bsp.Entities)
		{
			if (e.ClassName != "corona" || !TryParseVector(e.Get("origin"), out var o)) continue;
			var c = LightColor(e.Get("dl_color") ?? e.Get("_color"), Vector3.One);
			var corona = new Flares.Corona
			{
				SimOrigin = o,
				Color = new Color(c.X, c.Y, c.Z),
				Scale = Num(e.Get("scale"), 1f),
			};
			if (e.Get("zcutoff") is { } zc && e.Get("zfadeout") is { } zf)
			{
				corona.ZCutoff = Num(zc, 0);
				corona.ZFadeOut = Num(zf, 0);
			}
			coronas.Add(corona);
		}
		if (coronas.Count == 0 && _sunFlareTexture is null) return;

		Texture2D? coronaTex = null;
		if (resolver.FindShader("flareShader")?.FirstMap is { } map && resolver.FindTexture(map) is { } file)
			coronaTex = LoadTexture(pk3, file);

		var flares = new Flares { Name = "Destellos" };
		flares.Setup(ClipMap.FromBsp(bsp), coronas, coronaTex, _sunDirection, _sunFile, _sunFlareTexture);
		var layer = new CanvasLayer { Layer = -1 };
		layer.AddChild(flares);
		AddChild(layer);
		GD.Print($"[MapViewer] destellos: {coronas.Count} coronas, destello del astro {(_sunFlareTexture is null ? "no" : "sí")}");
	}
}
