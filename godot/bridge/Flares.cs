using System;
using System.Collections.Generic;
using Foley.Formats.Materials;
using Foley.Sim.Collision;
using Foley.Sim.Math;
using Godot;

/// <summary>
/// Destellos en pantalla (solo presentación): coronas del mapa (entidades <c>corona</c>) y destello,
/// brillo y oscurecimiento del sol o la luna (<c>scripts/&lt;sunfile&gt;.sun</c>). Se ocultan si la
/// colisión del mapa tapa la línea de visión. Ver docs/formats/shader.md ("Coronas y destellos").
/// </summary>
public partial class Flares : Control
{
	public sealed class Corona
	{
		public Vector3 SimOrigin;
		public Color Color = Colors.White;
		public float Scale = 1f;
		public float? ZCutoff, ZFadeOut;
		public float Alpha; // desvanecido actual
	}

	// Quake 3 (tr_flares.c / tr_init.c): r_flareSize 40, r_flareCoeff 150 (FLARE_STDCOEFF), r_flareFade 7.
	// TODO(presentación): valores de CoD1 sin verificar.
	private const float FlareSize = 40f;
	private const float FlareCoeff = 150f;
	private const float FlareFade = 7f;

	private readonly List<Corona> _coronas = new();
	private Texture2D? _coronaTex;
	private ClipMap? _map;

	// Sol o luna
	private Vector3? _sunDir;   // ejes de Godot, hacia el astro
	private Texture2D? _sunFlareTex;
	private SunFile? _sun;
	private float _sunAlpha;    // desvanecido actual del destello
	private float _glare, _blind;

	public void Setup(ClipMap map, IEnumerable<Corona> coronas, Texture2D? coronaTex,
		Vector3? sunDir, SunFile? sun, Texture2D? sunFlareTex)
	{
		_map = map;
		_coronas.AddRange(coronas);
		_coronaTex = coronaTex;
		_sunDir = sunDir;
		_sun = sun;
		_sunFlareTex = sunFlareTex;
		SetAnchorsPreset(LayoutPreset.FullRect);
		MouseFilter = MouseFilterEnum.Ignore;
		Material = new CanvasItemMaterial { BlendMode = CanvasItemMaterial.BlendModeEnum.Add };
	}

	public override void _Process(double delta)
	{
		var cam = GetViewport().GetCamera3D();
		if (cam is null || _map is null) return;
		var dt = (float)delta;
		var camSim = ToSim(cam.GlobalPosition);

		foreach (var c in _coronas)
		{
			var target = VisibleFrom(camSim, c.SimOrigin) ? 1f : 0f;
			c.Alpha = Mathf.MoveToward(c.Alpha, target, dt * FlareFade);
		}

		if (_sunDir is { } sd && _sun is not null)
		{
			var forward = -cam.GlobalTransform.Basis.Z;
			var angle = Mathf.RadToDeg(forward.AngleTo(sd));
			var sky = SkyVisible(camSim, sd);

			var flare = sky ? Ramp(angle, _sun.GetFloat("r_sunflare_min_angle", 45), _sun.GetFloat("r_sunflare_max_angle", 2)) : 0f;
			// fadein/fadeout en segundos (supuesto): tiempo de 0 a 1 y de 1 a 0.
			var rate = flare > _sunAlpha ? 1f / Mathf.Max(0.01f, _sun.GetFloat("r_sunflare_fadein", 1)) : 1f / Mathf.Max(0.01f, _sun.GetFloat("r_sunflare_fadeout", 0.2f));
			_sunAlpha = Mathf.MoveToward(_sunAlpha, flare, dt * rate);

			var glare = sky ? Ramp(angle, _sun.GetFloat("r_sunglare_min_angle", 30), _sun.GetFloat("r_sunglare_max_angle", 5)) : 0f;
			_glare = Mathf.MoveToward(_glare, glare, dt / Mathf.Max(0.01f, _sun.GetFloat(glare > _glare ? "r_sunglare_fadein" : "r_sunglare_fadeout", 0.1f)));
			var blind = sky ? Ramp(angle, _sun.GetFloat("r_sunblind_max_angle", 5), _sun.GetFloat("r_sunblind_min_angle", 1)) : 0f;
			_blind = Mathf.MoveToward(_blind, blind, dt / Mathf.Max(0.01f, _sun.GetFloat(blind > _blind ? "r_sunblind_fadein" : "r_sunblind_fadeout", 1f)));
		}
		QueueRedraw();
	}

	public override void _Draw()
	{
		var cam = GetViewport().GetCamera3D();
		if (cam is null) return;
		var width = GetViewportRect().Size.X;
		var camPos = cam.GlobalPosition;

		if (_coronaTex is not null)
		{
			foreach (var c in _coronas)
			{
				if (c.Alpha <= 0) continue;
				var world = MapViewer.SimToGodot(c.SimOrigin.X, c.SimOrigin.Y, c.SimOrigin.Z);
				if (cam.IsPositionBehind(world)) continue;
				var distance = camPos.DistanceTo(world) / 0.03f; // en unidades del juego
				var eye = ToSim(camPos);
				var dz = (new Vector3(eye.X, eye.Y, eye.Z) - c.SimOrigin).Normalized().Z;
				var zFade = 1f;
				if (c.ZCutoff is { } cut && c.ZFadeOut is { } fo && !Mathf.IsEqualApprox(cut, fo))
					zFade = Mathf.Clamp((cut - dz) / (cut - fo), 0f, 1f);
				if (zFade <= 0) continue;

				// RB_RenderFlare de Quake 3
				var size = width * (FlareSize / 640f + 8f / Mathf.Max(distance, 1f)) * c.Scale;
				var factor = distance + size * Mathf.Sqrt(FlareCoeff);
				var intensity = Mathf.Min(1f, FlareCoeff * size * size / (factor * factor));
				var col = c.Color * (c.Alpha * intensity * zFade);
				col.A = 1f;
				var p = cam.UnprojectPosition(world);
				DrawTextureRect(_coronaTex, new Rect2(p - new Vector2(size, size) * 0.5f, new Vector2(size, size)), false, col);
			}
		}

		if (_sunDir is { } sd && _sun is not null)
		{
			var screen = GetViewportRect().Size;
			if (_glare > 0)
				DrawRect(new Rect2(Vector2.Zero, screen), new Color(1, 1, 1, 1) * (_glare * _sun.GetFloat("r_sunglare_max_lighten", 0)));
			if (_sunFlareTex is not null && _sunAlpha > 0 && !cam.IsPositionBehind(camPos + sd))
			{
				var t = _sunAlpha;
				var size = Mathf.Lerp(_sun.GetFloat("r_sunflare_min_size", 0), _sun.GetFloat("r_sunflare_max_size", 120), t) * width / 640f;
				var p = cam.UnprojectPosition(camPos + sd * 100f);
				var col = Colors.White * (t * _sun.GetFloat("r_sunflare_max_alpha", 1));
				col.A = 1f;
				DrawTextureRect(_sunFlareTex, new Rect2(p - new Vector2(size, size) * 0.5f, new Vector2(size, size)), false, col);
			}
		}
	}

	/// <summary>0 fuera de <paramref name="from"/> grados, 1 a <paramref name="to"/> grados o menos.</summary>
	private static float Ramp(float angle, float from, float to) =>
		Mathf.IsEqualApprox(from, to) ? (angle <= to ? 1f : 0f) : Mathf.Clamp((from - angle) / (from - to), 0f, 1f);

	// El rayo se detiene 16 unidades antes del halo para que no lo tape la propia lámpara que lo rodea.
	private bool VisibleFrom(Vec3 eye, Vector3 target)
	{
		var t = new Vec3(target.X, target.Y, target.Z);
		var d = t - eye;
		var len = QMath.Length(d);
		if (len <= 16f) return true;
		var end = Vec3.MA(eye, (len - 16f) / len, d);
		var tr = _map!.Trace(eye, end, Vec3.Zero, Vec3.Zero, ClipMap.ContentsSolid);
		return tr.Fraction >= 1f;
	}

	// El rayo hacia el astro debe acabar en una superficie de cielo (SURF_SKY = 0x4, como Quake 3).
	private bool SkyVisible(Vec3 eye, Vector3 dirGodot)
	{
		var d = new Vec3(dirGodot.X, -dirGodot.Z, dirGodot.Y);
		var end = new Vec3(eye.X + d.X * 65536f, eye.Y + d.Y * 65536f, eye.Z + d.Z * 65536f);
		var tr = _map!.Trace(eye, end, Vec3.Zero, Vec3.Zero, ClipMap.ContentsSolid);
		return tr.Fraction >= 1f || (tr.SurfaceFlags & 0x4) != 0;
	}

	private static Vec3 ToSim(Vector3 g) => new(g.X / 0.03f, -g.Z / 0.03f, g.Y / 0.03f);
}
