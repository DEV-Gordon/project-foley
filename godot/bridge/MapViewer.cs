using System;
using System.Collections.Generic;
using System.Globalization;
using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;
using Godot;

/// <summary>
/// Visor del mapa (hito 2): lee el BSP y los pk3 de la instalación del jugador y construye la
/// malla visible. Solo presentación: no genera colisión ni toca la simulación.
/// </summary>
public partial class MapViewer : Node3D
{
	/// <summary>Ruta lógica del mapa dentro de los pk3.</summary>
	[Export] public string MapPath { get; set; } = "maps/burnville.bsp";

	// Escala solo visual (unidades del original -> metros). La simulación conserva las unidades originales.
	// TODO(fidelidad): valor provisional de presentación; no sale de ninguna fuente del juego.
	private const float MetersPerUnit = 0.03f;

	private readonly Dictionary<string, Texture2D?> _textures = new();

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

		var root = ResolveGameRoot();
		if (root is null)
		{
			GD.PushError("No se encontró la instalación del juego. Define COD1_PATH o 'game_path' en user://foley.cfg.");
			return;
		}
		var pk3Dir = Pk3Locator.FindPk3Directory(root);
		if (pk3Dir is null)
		{
			GD.PushError($"No hay archivos .pk3 en '{root}'.");
			return;
		}

		var sw = System.Diagnostics.Stopwatch.StartNew();
		BspFile bsp;
		using (var pk3 = new Pk3Collection(pk3Dir))
		{
			bsp = BspFile.Read(pk3.ReadAllBytes(MapPath));
			var resolver = new MaterialResolver(pk3);
			BuildMap(bsp, pk3, resolver);
			BuildModels(bsp, pk3);
		}
		GD.Print($"[MapViewer] {MapPath}: {bsp.Surfaces.Length} superficies, {bsp.Vertices.Length} vértices, {sw.ElapsedMilliseconds} ms");

		PlaceCamera(bsp, args);
		AddLighting();
		AddHud();
		MapLoaded?.Invoke(bsp);

		if (args.TryGetValue("shot", out var shotPath)) _ = TakeScreenshotAndQuit(shotPath);
	}

	private void BuildMap(BspFile bsp, Pk3Collection pk3, MaterialResolver resolver)
	{
		var bySurfaceMaterial = new Dictionary<int, List<BspSurface>>();
		foreach (var s in bsp.Surfaces)
		{
			if (!bySurfaceMaterial.TryGetValue(s.MaterialIndex, out var list))
				bySurfaceMaterial[s.MaterialIndex] = list = new List<BspSurface>();
			list.Add(s);
		}

		int skippedSky = 0, skippedNoDraw = 0, untextured = 0, drawnSurfaces = 0;
		foreach (var (materialIndex, surfaces) in bySurfaceMaterial)
		{
			var resolved = resolver.Resolve(bsp.Materials[materialIndex].Name);
			if (resolved.IsSky) { skippedSky += surfaces.Count; continue; }
			if (resolved.NoDraw) { skippedNoDraw += surfaces.Count; continue; }

			var mesh = BuildMesh(bsp, surfaces);
			var material = MakeMaterial(pk3, resolved, ref untextured);
			mesh.SurfaceSetMaterial(0, material);
			AddChild(new MeshInstance3D { Mesh = mesh, Name = resolved.Name.Replace('/', '_') });
			drawnSurfaces += surfaces.Count;
		}
		GD.Print($"[MapViewer] dibujadas {drawnSurfaces} superficies; omitidas: cielo {skippedSky}, nodraw {skippedNoDraw}; materiales sin textura: {untextured}");
	}

	private static ArrayMesh BuildMesh(BspFile bsp, List<BspSurface> surfaces)
	{
		var vertexTotal = 0;
		var indexTotal = 0;
		foreach (var s in surfaces) { vertexTotal += s.VertexCount; indexTotal += s.IndexCount; }

		var positions = new Vector3[vertexTotal];
		var normals = new Vector3[vertexTotal];
		var uv = new Vector2[vertexTotal];
		var uv2 = new Vector2[vertexTotal];
		var indices = new int[indexTotal];

		var v = 0;
		var i = 0;
		foreach (var s in surfaces)
		{
			var baseVertex = v;
			for (var k = 0; k < s.VertexCount; k++, v++)
			{
				var src = bsp.Vertices[(int)s.FirstVertex + k];
				positions[v] = ToGodot(src.X, src.Y, src.Z) * MetersPerUnit;
				normals[v] = ToGodot(src.Nx, src.Ny, src.Nz);
				uv[v] = new Vector2(src.U, src.V);
				uv2[v] = new Vector2(src.LmU, src.LmV);
			}
			// El giro horario del original coincide con el frontal de Godot al ser una rotación propia.
			for (var k = 0; k < s.IndexCount; k++, i++)
				indices[i] = baseVertex + bsp.Indices[(int)s.FirstIndex + k];
		}

		var arrays = new Godot.Collections.Array();
		arrays.Resize((int)Mesh.ArrayType.Max);
		arrays[(int)Mesh.ArrayType.Vertex] = positions;
		arrays[(int)Mesh.ArrayType.Normal] = normals;
		arrays[(int)Mesh.ArrayType.TexUV] = uv;
		arrays[(int)Mesh.ArrayType.TexUV2] = uv2;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		var mesh = new ArrayMesh();
		mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
		return mesh;
	}

	/// <summary>Ejes del original (Z arriba, diestro) a Godot (Y arriba, -Z adelante): rotación propia.</summary>
	private static Vector3 ToGodot(float x, float y, float z) => new(x, z, -y);

	/// <summary>Posición de la simulación (unidades y ejes originales) a posición de Godot (metros, Y arriba).</summary>
	public static Vector3 SimToGodot(float x, float y, float z) => ToGodot(x, y, z) * MetersPerUnit;

	private StandardMaterial3D MakeMaterial(Pk3Collection pk3, ResolvedMaterial resolved, ref int untextured)
	{
		var m = new StandardMaterial3D
		{
			Roughness = 1f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
		};
		var tex = resolved.TexturePath is null ? null : LoadTexture(pk3, resolved.TexturePath);
		if (tex is null)
		{
			untextured++;
			m.AlbedoColor = new Color(1f, 0f, 1f);
			return m;
		}
		m.AlbedoTexture = tex;
		if (_alphaTextures.Contains(resolved.TexturePath!))
		{
			m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
			m.AlphaScissorThreshold = 0.5f;
		}
		return m;
	}

	private readonly HashSet<string> _alphaTextures = new();

	/// <param name="invertAlpha">
	/// Invierte el canal alfa (skins de follaje de los modelos, que guardan la forma con alfa 0;
	/// ver docs/formats/xmodel.md). Se cachea aparte con la clave <c>ruta|inv</c>.
	/// </param>
	private Texture2D? LoadTexture(Pk3Collection pk3, string path, bool invertAlpha = false)
	{
		var key = invertAlpha ? path + "|inv" : path;
		if (_textures.TryGetValue(key, out var cached)) return cached;

		Texture2D? result = null;
		try
		{
			var bytes = pk3.ReadAllBytes(path);
			var image = new Image();
			var err = path switch
			{
				_ when path.EndsWith(".dds") => image.LoadDdsFromBuffer(bytes),
				_ when path.EndsWith(".tga") => image.LoadTgaFromBuffer(bytes),
				_ when path.EndsWith(".jpg") => image.LoadJpgFromBuffer(bytes),
				_ => Error.FileUnrecognized,
			};
			if (err != Error.Ok)
			{
				GD.PushWarning($"[MapViewer] no se pudo decodificar {path}: {err}");
			}
			else
			{
				if (invertAlpha) InvertAlpha(image);
				if (!image.IsCompressed() && !image.HasMipmaps()) image.GenerateMipmaps();
				if (image.DetectAlpha() != Image.AlphaMode.None) _alphaTextures.Add(key);
				result = ImageTexture.CreateFromImage(image);
			}
		}
		catch (Exception e)
		{
			GD.PushWarning($"[MapViewer] error cargando {path}: {e.Message}");
		}
		_textures[key] = result;
		return result;
	}

	/// <summary>Invierte el alfa de todos los niveles de la imagen (descomprime DDS a RGBA8).</summary>
	private static void InvertAlpha(Image image)
	{
		if (image.IsCompressed()) image.Decompress();
		if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
		var data = image.GetData();
		for (var i = 3; i < data.Length; i += 4) data[i] = (byte)(255 - data[i]);
		image.SetData(image.GetWidth(), image.GetHeight(), image.HasMipmaps(), Image.Format.Rgba8, data);
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
				pos = ToGodot(q.X, q.Y, q.Z) * MetersPerUnit + new Vector3(0, 1.0f, 0);
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

	private void AddLighting()
	{
		// Solo para poder inspeccionar el mapa; la iluminación real es del hito 8.
		// Si la escena ya trae su entorno o su sol (p. ej. luztest.tscn) se respetan y no se añade otro.
		WorldEnvironment? sceneEnvironment = null;
		var sceneHasSun = false;
		foreach (var child in GetChildren())
		{
			if (child is WorldEnvironment we) sceneEnvironment ??= we;
			else if (child is DirectionalLight3D) sceneHasSun = true;
		}

		if (sceneEnvironment is not null)
		{
			sceneEnvironment.Environment ??= CreateInspectionEnvironment();
			_environment = sceneEnvironment.Environment;
		}
		else
		{
			_environment = CreateInspectionEnvironment();
			AddChild(new WorldEnvironment { Environment = _environment });
		}

		if (!sceneHasSun)
			AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-50, 30, 0), LightEnergy = 0.6f });
	}

	private static Godot.Environment CreateInspectionEnvironment() => new()
	{
		AmbientLightSource = Godot.Environment.AmbientSource.Color,
		AmbientLightColor = new Color(0.8f, 0.82f, 0.9f),
		AmbientLightEnergy = 1.0f,
		BackgroundMode = Godot.Environment.BGMode.Color,
		BackgroundColor = new Color(0.05f, 0.06f, 0.1f),
	};

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

	private async System.Threading.Tasks.Task TakeScreenshotAndQuit(string path)
	{
		for (var i = 0; i < 5; i++) await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
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

	/// <summary>COD1_PATH, o 'game_path' en user://foley.cfg. Nunca una ruta fija.</summary>
	private static string? ResolveGameRoot()
	{
		var env = System.Environment.GetEnvironmentVariable("COD1_PATH");
		if (!string.IsNullOrWhiteSpace(env)) return env;
		var cfg = new ConfigFile();
		if (cfg.Load("user://foley.cfg") == Error.Ok)
		{
			var p = (string)cfg.GetValue("game", "game_path", "");
			if (!string.IsNullOrWhiteSpace(p)) return p;
		}
		return null;
	}
}
