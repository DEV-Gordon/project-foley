using System;
using System.Collections.Generic;
using System.Globalization;
using Foley.Formats.Bsp;
using Godot;

/// <summary>
/// Visor del mapa en el juego: construye el mapa con <see cref="MapBuilder"/> y añade lo propio del
/// juego (evento <see cref="MapLoaded"/>, HUD, teclas de inspección L y B, coronas y destellos).
/// Solo presentación: no genera colisión ni toca la simulación.
/// </summary>
public partial class MapViewer : Node3D
{
	/// <summary>Ruta lógica del mapa dentro de los pk3.</summary>
	[Export] public string MapPath { get; set; } = "maps/burnville.bsp";

	/// <summary>Se emite al terminar de construir el mapa, con el BSP leído (lo usa el controlador del jugador).</summary>
	public event Action<BspFile>? MapLoaded;

	// Ayudas de inspección (solo presentación). L cambia la vista; B cambia el brillo ambiental.
	private static readonly float[] AmbientLevels = { 1.0f, 2.0f, 4.0f, 0.3f };
	private Godot.Environment? _environment;
	private Label? _hud;
	private int _viewMode;   // 0 normal, 1 sin sombreado (fullbright), 2 alambre
	private int _ambientIndex;

	public override void _Ready()
	{
		var args = ParseUserArgs();
		if (args.TryGetValue("map", out var mapArg)) MapPath = mapArg;

		// Debe activarse antes de crear las mallas para que el modo alambre tenga datos.
		RenderingServer.SetDebugGenerateWireframes(true);

		if (!MapBuilder.TryFindPk3Directory(out var pk3Dir, out var error))
		{
			GD.PushError(error);
			return;
		}

		var (settings, overrides) = LoadLighting();
		var map = MapBuilder.Build(MapPath, pk3Dir, settings, overrides, this);
		_environment = map.Environment;
		BuildFlares(map);

		PlaceCamera(map.Bsp, args);
		AddHud();
		MapLoaded?.Invoke(map.Bsp);

		if (args.TryGetValue("shot", out var shotPath)) _ = TakeScreenshotAndQuit(shotPath, args.TryGetValue("wait", out var w) ? Parse(w) : 0f);
	}

	/// <summary>Posición de la simulación (unidades y ejes originales) a posición de Godot (metros, Y arriba).</summary>
	public static Vector3 SimToGodot(float x, float y, float z) => MapBuilder.SimToGodot(x, y, z);

	/// <summary>
	/// Iluminación del mapa: si existe el rig <c>res://render/maps/&lt;mapa&gt;.tscn</c>, se instancia en el
	/// origen (sus luces manuales se suman a las del BSP) y valen sus ajustes y sus ajustes por luz; si
	/// no, <c>default.tres</c>; si tampoco, los valores del código.
	/// </summary>
	private (MapLighting settings, Godot.Collections.Array<MapLightOverride>? overrides) LoadLighting()
	{
		var rigPath = $"res://render/maps/{System.IO.Path.GetFileNameWithoutExtension(MapPath)}.tscn";
		if (ResourceLoader.Exists(rigPath) && ResourceLoader.Load(rigPath) is PackedScene scene)
		{
			var node = scene.Instantiate();
			if (node is MapLightRig rig)
			{
				rig.Transform = Transform3D.Identity;
				AddChild(rig);
				GD.Print($"[MapViewer] iluminación: {rigPath} ({rig.AjustesPorLuz.Count} ajustes por luz, {CountLights(rig)} luces manuales)");
				return (rig.EffectiveLighting(), rig.AjustesPorLuz);
			}
			node.Free();
			GD.PushWarning($"[MapViewer] {rigPath} no tiene un MapLightRig como raíz; se ignora.");
		}
		if (MapLightRig.LoadDefaultLighting() is { } d)
		{
			GD.Print($"[MapViewer] iluminación: {MapLightRig.DefaultLightingPath}");
			return (d, null);
		}
		GD.Print("[MapViewer] iluminación: valores por defecto del código");
		return (new MapLighting(), null);
	}

	private static int CountLights(Node n)
	{
		var count = 0;
		foreach (var c in n.GetChildren()) count += (c is Light3D ? 1 : 0) + CountLights(c);
		return count;
	}

	private void PlaceCamera(BspFile bsp, Dictionary<string, string> args)
	{
		var cam = GetNodeOrNull<Camera3D>("Camera3D");
		if (cam is null) return;

		var pos = new Vector3(0, 10, 0);
		foreach (var e in bsp.Entities)
		{
			if (e.ClassName != "info_player_start") continue;
			var o = e.Get("origin");
			if (o is null) break;
			var p = o.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			if (p.Length == 3)
			{
				var q = new Vector3(Parse(p[0]), Parse(p[1]), Parse(p[2]));
				pos = MapBuilder.SimToGodot(q.X, q.Y, q.Z) + new Vector3(0, 1.0f, 0);
			}
			break;
		}

		if (args.TryGetValue("pos", out var posArg))
		{
			var p = posArg.Split(',');
			if (p.Length == 3) pos = new Vector3(Parse(p[0]), Parse(p[1]), Parse(p[2]));
		}
		cam.Position = pos;
		var yaw = args.TryGetValue("yaw", out var y) ? Parse(y) : 0f;
		var pitch = args.TryGetValue("pitch", out var pi) ? Parse(pi) : 0f;
		cam.RotationDegrees = new Vector3(pitch, yaw, 0);
	}

	private void AddHud()
	{
		_hud = new Label { Position = new Vector2(12, 8) };
		_hud.AddThemeColorOverride("font_shadow_color", Colors.Black);
		_hud.AddThemeConstantOverride("shadow_offset_x", 1);
		_hud.AddThemeConstantOverride("shadow_offset_y", 1);
		var layer = new CanvasLayer();
		layer.AddChild(_hud);
		AddChild(layer);
		UpdateHud();
	}

	public override void _UnhandledKeyInput(InputEvent @event)
	{
		if (@event is not InputEventKey { Pressed: true, Echo: false } key) return;
		switch (key.Keycode)
		{
			case Key.L:
				_viewMode = (_viewMode + 1) % 3;
				GetViewport().DebugDraw = _viewMode switch
				{
					1 => Viewport.DebugDrawEnum.Unshaded,
					2 => Viewport.DebugDrawEnum.Wireframe,
					_ => Viewport.DebugDrawEnum.Disabled,
				};
				break;
			case Key.B:
				_ambientIndex = (_ambientIndex + 1) % AmbientLevels.Length;
				if (_environment is not null) _environment.AmbientLightEnergy = AmbientLevels[_ambientIndex];
				break;
			default:
				return;
		}
		UpdateHud();
	}

	private void UpdateHud()
	{
		if (_hud is null) return;
		var mode = _viewMode switch { 1 => "sin sombreado (fullbright)", 2 => "alambre", _ => "normal" };
		_hud.Text = $"Vista: {mode}  [L]    Brillo ambiental: x{AmbientLevels[_ambientIndex]:0.##}  [B]" + (char)10 +
					"WASD mover | Q/E bajar/subir | Shift rapido | clic derecho: girar";
	}

	/// <summary>Captura tras 5 cuadros y, si se pide, <paramref name="waitSeconds"/> más (para los desvanecidos).</summary>
	private async System.Threading.Tasks.Task TakeScreenshotAndQuit(string path, float waitSeconds)
	{
		for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
		if (waitSeconds > 0) await ToSignal(GetTree().CreateTimer(waitSeconds), SceneTreeTimer.SignalName.Timeout);
		var image = GetViewport().GetTexture().GetImage();
		var err = image.SavePng(path);
		GD.Print($"[MapViewer] captura -> {path}: {err}");
		GetTree().Quit();
	}

	private static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);

	private static Dictionary<string, string> ParseUserArgs()
	{
		var result = new Dictionary<string, string>();
		foreach (var a in OS.GetCmdlineUserArgs())
		{
			if (!a.StartsWith("--")) continue;
			var eq = a.IndexOf('=');
			if (eq < 0) result[a[2..]] = "";
			else result[a[2..eq]] = a[(eq + 1)..];
		}
		return result;
	}
}
