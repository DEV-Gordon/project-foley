using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Foley.Formats.Bsp;
using Foley.Formats.Materials;
using Foley.Formats.Pk3;
using Foley.Formats.XModel;
using Godot;

/// <summary>
/// Construye la presentación de un mapa a partir del BSP y los pk3 de la instalación del jugador y la
/// cuelga de un nodo padre: mallas por material (<see cref="StageMaterials"/>), modelos del mapa con
/// LODs, marcadores FX, cielo con el sprite de la luna, entorno y luces de las entidades del BSP.
/// Lo usan el juego (<see cref="MapViewer"/>) y la vista previa del editor (<see cref="MapLightRig"/>).
/// No sabe nada del jugador, del HUD ni de las ayudas de inspección. Solo presentación: no genera
/// colisión ni toca la simulación. Nada de lo que crea se guarda en el proyecto (CLAUDE.md, regla dura 1).
///
/// Propiedades del <see cref="Godot.Environment"/> que pisa el código (las demás, como SSAO, SSR y la
/// niebla volumétrica, son las de la escena; ver docs/decisions/0003-iluminacion.md):
/// <c>background_mode</c> y <c>sky</c> (si el mapa tiene cielo), <c>ambient_light_source</c>,
/// <c>ambient_light_color</c>, <c>ambient_light_energy</c>, <c>tonemap_mode</c>,
/// <c>tonemap_exposure</c>, <c>glow_enabled</c>, <c>glow_intensity</c>, <c>sdfgi_enabled</c>,
/// <c>sdfgi_energy</c>, <c>sdfgi_use_occlusion</c>, <c>sdfgi_bounce_feedback</c> y <c>ssil_enabled</c>.
/// </summary>
public sealed class MapBuilder
{
	// Escala solo visual (unidades del original -> metros). La simulación conserva las unidades originales.
	// TODO(fidelidad): valor provisional de presentación; no sale de ninguna fuente del juego.
	public const float MetersPerUnit = 0.03f;

	/// <summary>Identificador del sol o la luna en los ajustes por luz.</summary>
	public const string SunId = "sol";

	/// <summary>Una luz creada desde el BSP: su identificador para los ajustes por luz y su tipo.</summary>
	public readonly record struct BspLight(string Id, string Kind, Vector3 Position);

	public BspFile Bsp { get; }
	public Godot.Environment Environment { get; private set; } = null!;
	/// <summary>Dirección hacia el sol o la luna en ejes de Godot (unitaria), o null si el mapa no la define.</summary>
	public Vector3? SunDirection { get; private set; }
	public SunFile? SunFile { get; private set; }
	/// <summary>Textura del destello del astro (<c>r_sunflare_shader</c>).</summary>
	public Texture2D? SunFlareTexture { get; private set; }
	/// <summary>Textura de las coronas (<c>flareShader</c>).</summary>
	public Texture2D? CoronaTexture { get; private set; }
	/// <summary>Luces del BSP creadas en la última aplicación de los ajustes (sin las desactivadas).</summary>
	public IReadOnlyList<BspLight> Lights => _lights;

	private readonly Node3D _parent;
	private readonly string _mapPath;
	private readonly Dictionary<string, Texture2D?> _textures = new();
	private readonly Dictionary<string, Material> _skinMaterials = new(StringComparer.OrdinalIgnoreCase);
	private readonly List<BspLight> _lights = new();
	private StageMaterials? _stages;
	private Sky? _sky;
	private Node3D? _lightsNode;
	private bool _parentHasSun;

	private MapBuilder(BspFile bsp, Node3D parent, string mapPath)
	{
		Bsp = bsp;
		_parent = parent;
		_mapPath = mapPath;
	}

	/// <summary>
	/// Lee el mapa y construye todo bajo <paramref name="parent"/>. Si el padre ya tiene un
	/// <see cref="WorldEnvironment"/> se reutiliza (conserva sus efectos de postproceso); si no, se crea
	/// uno, a partir de <paramref name="baseEnvironment"/> si se da. Si el padre tiene un
	/// <see cref="DirectionalLight3D"/>, no se crea el astro del mapa.
	/// </summary>
	public static MapBuilder Build(string mapPath, string pk3Dir, MapLighting settings,
		IEnumerable<MapLightOverride>? overrides, Node3D parent, Godot.Environment? baseEnvironment = null)
	{
		var sw = System.Diagnostics.Stopwatch.StartNew();
		MapBuilder builder;
		using (var pk3 = new Pk3Collection(pk3Dir))
		{
			builder = new MapBuilder(BspFile.Read(pk3.ReadAllBytes(mapPath)), parent, mapPath);
			var resolver = new MaterialResolver(pk3);
			builder._stages = new StageMaterials(path =>
				resolver.FindTexture(path) is { } file ? builder.LoadTexture(pk3, file) : null);
			builder.BuildMap(resolver);
			builder.BuildModels(pk3, resolver);
			builder.BuildSky(pk3, resolver);
			if (resolver.FindShader("flareShader")?.FirstMap is { } map && resolver.FindTexture(map) is { } file)
				builder.CoronaTexture = builder.LoadTexture(pk3, file);
			builder._stages = null;
		}
		GD.Print($"[MapBuilder] {mapPath}: {builder.Bsp.Surfaces.Length} superficies, {builder.Bsp.Vertices.Length} vértices, {sw.ElapsedMilliseconds} ms");

		builder.SetupEnvironment(baseEnvironment);
		builder.ApplyLighting(settings, overrides);
		return builder;
	}

	// ---------------------------------------------------------------- instalación del juego

	/// <summary>COD1_PATH, o 'game_path' en user://foley.cfg. Nunca una ruta fija.</summary>
	public static string? ResolveGameRoot()
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

	/// <summary>Carpeta de los pk3 de la instalación, o un mensaje de error claro.</summary>
	public static bool TryFindPk3Directory(out string pk3Dir, out string error)
	{
		pk3Dir = "";
		var root = ResolveGameRoot();
		if (root is null)
		{
			error = "No se encontró la instalación del juego. Define COD1_PATH o 'game_path' en user://foley.cfg.";
			return false;
		}
		var dir = Pk3Locator.FindPk3Directory(root);
		if (dir is null)
		{
			error = $"No hay archivos .pk3 en '{root}'.";
			return false;
		}
		pk3Dir = dir;
		error = "";
		return true;
	}

	// ---------------------------------------------------------------- ejes y unidades

	/// <summary>Ejes del original (Z arriba, diestro) a Godot (Y arriba, -Z adelante): rotación propia.</summary>
	public static Vector3 ToGodot(float x, float y, float z) => new(x, z, -y);

	/// <summary>Posición de la simulación (unidades y ejes originales) a posición de Godot (metros, Y arriba).</summary>
	public static Vector3 SimToGodot(float x, float y, float z) => ToGodot(x, y, z) * MetersPerUnit;

	// ---------------------------------------------------------------- superficies del BSP

	private void BuildMap(MaterialResolver resolver)
	{
		var bsp = Bsp;
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

			var mesh = BuildMesh(bsp, surfaces, StageMaterials.Deform(resolved.Shader));
			var material = _stages!.Build(resolved.Shader);
			if (material is null)
			{
				untextured++;
				material = new StandardMaterial3D { AlbedoColor = new Color(1f, 0f, 1f) };
			}
			mesh.SurfaceSetMaterial(0, material);
			_parent.AddChild(new MeshInstance3D { Mesh = mesh, Name = resolved.Name.Replace('/', '_') });
			drawnSurfaces += surfaces.Count;
		}
		GD.Print($"[MapBuilder] dibujadas {drawnSurfaces} superficies; omitidas: cielo {skippedSky}, nodraw {skippedNoDraw}; materiales sin textura: {untextured}");
	}

	private static ArrayMesh BuildMesh(BspFile bsp, List<BspSurface> surfaces, StageMaterials.SpriteDeform deform)
	{
		var vertexTotal = 0;
		var indexTotal = 0;
		foreach (var s in surfaces) { vertexTotal += s.VertexCount; indexTotal += s.IndexCount; }

		var positions = new Vector3[vertexTotal];
		var normals = new Vector3[vertexTotal];
		var uv = new Vector2[vertexTotal];
		var uv2 = new Vector2[vertexTotal];
		var colors = new Color[vertexTotal];
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
				// Color de vértice tal cual (rgbGen/alphaGen vertex lo usan; el shader lo linealiza).
				colors[v] = new Color(src.R / 255f, src.G / 255f, src.B / 255f, src.A / 255f);
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
		arrays[(int)Mesh.ArrayType.Color] = colors;
		arrays[(int)Mesh.ArrayType.Index] = indices;

		var mesh = new ArrayMesh();
		// deformVertexes autosprite/autosprite2: datos por vértice en CUSTOM0/CUSTOM1.
		StageMaterials.AddSurface(mesh, arrays, positions, deform);
		return mesh;
	}

	// ---------------------------------------------------------------- texturas

	/// <summary>Carga una textura del pk3 (DDS, TGA o JPG) con mipmaps; cacheada por ruta.</summary>
	private Texture2D? LoadTexture(Pk3Collection pk3, string path)
	{
		if (_textures.TryGetValue(path, out var cached)) return cached;

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
				GD.PushWarning($"[MapBuilder] no se pudo decodificar {path}: {err}");
			}
			else
			{
				if (!TryCustomMipmaps(pk3, path, ref image) && !image.IsCompressed() && !image.HasMipmaps())
					image.GenerateMipmaps();
				result = ImageTexture.CreateFromImage(image);
			}
		}
		catch (Exception e)
		{
			GD.PushWarning($"[MapBuilder] error cargando {path}: {e.Message}");
		}
		_textures[path] = result;
		return result;
	}

	/// <summary>
	/// Cadena de mips hecha a mano: las texturas <c>nombre#0</c> traen sus niveles siguientes como
	/// <c>nombre#1</c>, <c>nombre#2</c>... (56 en la instalación; p. ej. los cables, para que no
	/// desaparezcan de lejos). Se usan esos niveles y los más pequeños que falten se generan
	/// reduciendo el último. Supuesto sin verificar en el ejecutable (REA): solo hay evidencia de los datos.
	/// </summary>
	private static bool TryCustomMipmaps(Pk3Collection pk3, string path, ref Image level0)
	{
		var dot = path.LastIndexOf('.');
		if (dot < 2 || path[dot - 2] != '#' || path[dot - 1] != '0') return false;
		var stem = path[..(dot - 1)];
		if (level0.IsCompressed()) level0.Decompress();
		if (level0.HasMipmaps()) level0.ClearMipmaps();
		level0.Convert(Image.Format.Rgba8);

		var levels = new List<Image> { level0 };
		int w = level0.GetWidth(), h = level0.GetHeight();
		for (var n = 1; w > 1 || h > 1; n++)
		{
			w = Math.Max(1, w / 2);
			h = Math.Max(1, h / 2);
			Image? next = null;
			foreach (var ext in new[] { ".dds", ".tga", ".jpg" })
			{
				var file = stem + n + ext;
				if (!pk3.Contains(file)) continue;
				var img = new Image();
				var bytes = pk3.ReadAllBytes(file);
				var e = ext == ".dds" ? img.LoadDdsFromBuffer(bytes) : ext == ".tga" ? img.LoadTgaFromBuffer(bytes) : img.LoadJpgFromBuffer(bytes);
				if (e != Error.Ok) break;
				if (img.HasMipmaps()) img.ClearMipmaps();
				if (img.IsCompressed()) img.Decompress();
				img.Convert(Image.Format.Rgba8);
				if (img.GetWidth() == w && img.GetHeight() == h) next = img;
				break;
			}
			if (next is null)
			{
				next = (Image)levels[^1].Duplicate();
				next.Resize(w, h, Image.Interpolation.Bilinear);
			}
			levels.Add(next);
		}
		if (levels.Count < 2) return false;
		var data = new List<byte>();
		foreach (var l in levels) data.AddRange(l.GetData());
		level0 = Image.CreateFromData(level0.GetWidth(), level0.GetHeight(), true, Image.Format.Rgba8, data.ToArray());
		return true;
	}

	// ---------------------------------------------------------------- modelos del mapa

	/// <summary>
	/// Modelos del mapa (xmodels de las entidades: misc_model, script_model, ítems, ametralladoras).
	/// La colisión de los misc_model ya está en los brushes del BSP (docs/re/modelos.md). Los actores
	/// (actor_*) los creará la IA, no se dibujan aquí.
	/// </summary>
	private void BuildModels(Pk3Collection pk3, MaterialResolver resolver)
	{
		var instances = new Dictionary<string, List<Transform3D>>(StringComparer.OrdinalIgnoreCase);
		foreach (var e in Bsp.Entities)
		{
			var model = e.Get("model");
			if (model is null || model.StartsWith('*')) continue;
			if (e.ClassName.StartsWith("actor_", StringComparison.Ordinal)) continue;
			// Marcadores de efectos del editor: maps/_load.gsc los oculta al cargar el nivel, salvo los
			// que se llaman "exploderchunk" (trozos que salen volando en una explosión).
			if (string.Equals(model, "xmodel/fx", StringComparison.OrdinalIgnoreCase) && e.Get("targetname") != "exploderchunk") continue;
			if (!TryParseVector(e.Get("origin"), out var origin)) continue;

			var angles = Vector3.Zero; // pitch, yaw, roll en grados
			if (TryParseVector(e.Get("angles"), out var a)) angles = a;
			else if (e.Get("angle") is { } yaw) angles.Y = Parse(yaw);
			var scale = e.Get("modelscale") is { } ms ? Parse(ms) : 1f;

			if (!instances.TryGetValue(model, out var list)) instances[model] = list = new List<Transform3D>();
			list.Add(EntityTransform(origin, angles, scale));
		}

		int drawn = 0, failed = 0, withLods = 0;
		foreach (var (path, transforms) in instances)
		{
			// Un LOD por cada nombre del xmodel; cada uno se ve hasta su distancia (0 = sin límite).
			var lods = new List<(ArrayMesh mesh, float maxDist)>();
			try
			{
				var file = XModelFile.Read(pk3.ReadAllBytes(path));
				for (var l = 0; l < file.Lods.Length; l++)
					lods.Add((BuildModelMesh(XModelMesh.Load(pk3, path, l), resolver), file.Lods[l].Distance));
			}
			catch (Exception ex)
			{
				GD.PushWarning($"[MapBuilder] no se pudo cargar {path}: {ex.Message}");
				failed++;
				continue;
			}

			if (lods.Count == 1)
			{
				var multi = new MultiMesh
				{
					TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
					Mesh = lods[0].mesh,
					InstanceCount = transforms.Count,
				};
				for (var i = 0; i < transforms.Count; i++) multi.SetInstanceTransform(i, transforms[i]);
				_parent.AddChild(new MultiMeshInstance3D { Multimesh = multi, Name = path.Replace('/', '_') });
			}
			else
			{
				// El rango de visibilidad de Godot es por nodo, así que cada instancia va suelta.
				// TODO(presentación): distancia en unidades del juego tal cual, sin r_lodScale ni campo de visión.
				withLods++;
				foreach (var t in transforms)
				{
					var begin = 0f;
					for (var l = 0; l < lods.Count; l++)
					{
						var end = lods[l].maxDist > 0 ? lods[l].maxDist * MetersPerUnit : 0f;
						_parent.AddChild(new MeshInstance3D
						{
							Mesh = lods[l].mesh,
							Transform = t,
							VisibilityRangeBegin = begin,
							VisibilityRangeEnd = end,
							Name = path.Replace('/', '_') + "_lod" + l,
						});
						if (end <= 0) break;
						begin = end;
					}
				}
			}
			drawn += transforms.Count;
		}
		GD.Print($"[MapBuilder] modelos: {instances.Count} distintos ({withLods} con LODs), {drawn} instancias, {failed} sin cargar");
	}

	/// <summary>
	/// Orientación de una entidad como AnglesToAxis de id Tech 3 (ioquake3 q_math.c, AngleVectors):
	/// adelante, izquierda y arriba en ejes originales, llevada a ejes de Godot.
	/// </summary>
	private static Transform3D EntityTransform(Vector3 origin, Vector3 anglesDeg, float scale)
	{
		var p = Mathf.DegToRad(anglesDeg.X);
		var y = Mathf.DegToRad(anglesDeg.Y);
		var r = Mathf.DegToRad(anglesDeg.Z);
		float sp = Mathf.Sin(p), cp = Mathf.Cos(p), sy = Mathf.Sin(y), cy = Mathf.Cos(y), sr = Mathf.Sin(r), cr = Mathf.Cos(r);
		var forward = new Vector3(cp * cy, cp * sy, -sp);
		var right = new Vector3(-sr * sp * cy + cr * sy, -sr * sp * sy - cr * cy, -sr * cp);
		var up = new Vector3(cr * sp * cy + sr * sy, cr * sp * sy - sr * cy, cr * cp);

		// Los vértices del modelo ya están en ejes de Godot: x -> adelante, y (arriba) -> arriba, z (= -y original) -> derecha.
		var basis = new Basis(ToGodot(forward.X, forward.Y, forward.Z),
							  ToGodot(up.X, up.Y, up.Z),
							  ToGodot(right.X, right.Y, right.Z)) * Basis.FromScale(Vector3.One * scale);
		return new Transform3D(basis, ToGodot(origin.X, origin.Y, origin.Z) * MetersPerUnit);
	}

	private ArrayMesh BuildModelMesh(XModelMesh model, MaterialResolver resolver)
	{
		var mesh = new ArrayMesh();
		foreach (var s in model.Surfaces)
		{
			// Shader de la skin: shader propio o plantilla shadertypes/model/<tipo>.stype.
			var resolved = resolver.Resolve("skins/" + s.Material, MaterialKind.Model);
			var radial = resolved.Shader.RadialNormals;

			var n = s.Positions.Length / 3;
			var positions = new Vector3[n];
			var normals = new Vector3[n];
			var uv = new Vector2[n];
			for (var v = 0; v < n; v++)
			{
				var px = s.Positions[v * 3];
				var py = s.Positions[v * 3 + 1];
				var pz = s.Positions[v * 3 + 2];
				positions[v] = ToGodot(px, py, pz) * MetersPerUnit;
				// radialNormals: normales que salen del origen del modelo (follaje), en vez de las de la malla.
				normals[v] = radial
					? ToGodot(px, py, pz).Normalized()
					: ToGodot(s.Normals[v * 3], s.Normals[v * 3 + 1], s.Normals[v * 3 + 2]);
				uv[v] = new Vector2(s.UVs[v * 2], s.UVs[v * 2 + 1]);
			}
			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = positions;
			arrays[(int)Mesh.ArrayType.Normal] = normals;
			arrays[(int)Mesh.ArrayType.TexUV] = uv;
			// Mismo giro que el BSP (docs/formats/xmodel.md).
			arrays[(int)Mesh.ArrayType.Index] = s.Triangles;
			StageMaterials.AddSurface(mesh, arrays, positions, StageMaterials.Deform(resolved.Shader));
			mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, SkinMaterial(resolved));
		}
		return mesh;
	}

	private Material SkinMaterial(ResolvedMaterial resolved)
	{
		if (_skinMaterials.TryGetValue(resolved.Name, out var cached)) return cached;
		var m = _stages!.Build(resolved.Shader) ?? new StandardMaterial3D { AlbedoColor = new Color(1f, 0f, 1f) };
		_skinMaterials[resolved.Name] = m;
		return m;
	}

	// ---------------------------------------------------------------- cielo

	/// <summary>
	/// Cielo original: la caja de <c>skyParms</c> del shader de cielo del mapa (6 caras en <c>env/</c>) y el
	/// sprite de la luna o el sol de su <c>sunfile</c>, en la dirección <c>sundirection</c> del worldspawn.
	/// Ver docs/formats/shader.md ("Cielo").
	/// </summary>
	private void BuildSky(Pk3Collection pk3, MaterialResolver resolver)
	{
		SunDirection = ComputeSunDirection(Bsp);

		ShaderDef? skyShader = null;
		foreach (var s in Bsp.Surfaces)
		{
			var r = resolver.Resolve(Bsp.Materials[s.MaterialIndex].Name);
			if (r.IsSky && r.Shader.SkyEnv is not null) { skyShader = r.Shader; break; }
		}
		if (skyShader is null) return;

		// Orden de id Tech 3 (tr_sky.c: suf[] y sky_texorder[]): +X rt, -X lf, +Y bk, -Y ft, +Z up, -Z dn.
		var mat = new ShaderMaterial { Shader = new Shader { Code = SkyCode } };
		foreach (var face in new[] { "rt", "lf", "bk", "ft", "up", "dn" })
		{
			var file = resolver.FindTexture(skyShader.SkyEnv + "_" + face);
			var tex = file is null ? null : LoadTexture(pk3, file);
			if (tex is null) { GD.PushWarning($"[MapBuilder] falta la cara {face} del cielo {skyShader.SkyEnv}"); return; }
			mat.SetShaderParameter(face, tex);
		}

		// Sprite del sol o la luna (sunfile → r_sunsprite_shader).
		if (skyShader.SunFile is { } sunName && pk3.Contains($"scripts/{sunName}.sun") && SunDirection is { } dir)
		{
			var sun = SunFile.Parse(pk3.ReadAllText($"scripts/{sunName}.sun"));
			SunFile = sun;
			// Destello (r_sunflare_shader): lo dibuja quien use el constructor (capa Flares del juego).
			if (sun.Get("r_sunflare_shader") is { } flareName && resolver.FindShader(flareName)?.FirstMap is { } flareMap
				&& resolver.FindTexture(flareMap) is { } flareFile)
				SunFlareTexture = LoadTexture(pk3, flareFile);
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
	private static Vector3? ComputeSunDirection(BspFile bsp)
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

	// ---------------------------------------------------------------- iluminación nueva

	/// <summary>Busca o crea el WorldEnvironment del padre y pone el cielo original.</summary>
	private void SetupEnvironment(Godot.Environment? baseEnvironment)
	{
		WorldEnvironment? worldEnv = null;
		foreach (var child in _parent.GetChildren())
		{
			if (child is WorldEnvironment we) worldEnv ??= we;
			else if (child is DirectionalLight3D) _parentHasSun = true;
		}
		if (worldEnv is null)
		{
			worldEnv = new WorldEnvironment { Name = "Entorno" };
			_parent.AddChild(worldEnv);
		}
		worldEnv.Environment ??= (Godot.Environment?)baseEnvironment?.Duplicate() ?? new Godot.Environment();
		Environment = worldEnv.Environment;

		if (_sky is not null)
		{
			Environment.BackgroundMode = Godot.Environment.BGMode.Sky;
			Environment.Sky = _sky;
		}
	}

	/// <summary>
	/// Aplica los ajustes al entorno y vuelve a crear las luces del BSP (sol o luna, puntuales y focos).
	/// Se puede llamar otra vez al cambiar un ajuste: no relee el mapa.
	/// Iluminación nueva (CLAUDE.md, "Iluminación"): sin lightmaps; luces de Godot guiadas por las
	/// entidades del BSP y GI en tiempo real. Ver docs/decisions/0003-iluminacion.md.
	/// </summary>
	public void ApplyLighting(MapLighting settings, IEnumerable<MapLightOverride>? overrides)
	{
		var byId = new Dictionary<string, MapLightOverride>(StringComparer.OrdinalIgnoreCase);
		if (overrides is not null)
			foreach (var o in overrides)
				if (o is not null && !string.IsNullOrWhiteSpace(o.Id)) byId[NormalizeId(o.Id)] = o;

		var env = Environment;
		var world = Bsp.Entities.Count > 0 ? Bsp.Entities[0] : null;

		// Ambiente del worldspawn: ambient · _color + luz difusa del cielo (sundiffusecolor · sunlight · diffusefraction).
		// El renderer de CoDSP.exe avisa "ambient too big, assuming it uses the old 0-255 scale instead of
		// the proper 0-1 scale": un ambient mayor que 1 se divide por 255.
		var ambient = Num(world?.Get("ambient"), 0f);
		if (ambient > 1f) ambient /= 255f;
		var ambientColor = LightColor(world?.Get("_color"), Vector3.One) * ambient
			+ LightColor(world?.Get("sundiffusecolor"), Vector3.Zero) * Num(world?.Get("sunlight"), 0f) * Num(world?.Get("diffusefraction"), 0f);
		if (settings.AmbientFloorEnabled)
		{
			var f = settings.AmbientFloor;
			ambientColor = new Vector3(Mathf.Max(ambientColor.X, f.R), Mathf.Max(ambientColor.Y, f.G), Mathf.Max(ambientColor.Z, f.B));
		}
		env.AmbientLightSource = Godot.Environment.AmbientSource.Color;
		env.AmbientLightColor = new Color(ambientColor.X, ambientColor.Y, ambientColor.Z);
		env.AmbientLightEnergy = settings.AmbientScale;
		env.TonemapMode = settings.Tonemap;
		env.TonemapExposure = settings.Exposure;
		env.GlowEnabled = settings.Glow;
		env.GlowIntensity = settings.GlowIntensity;
		env.SdfgiEnabled = settings.Sdfgi;
		env.SdfgiEnergy = settings.SdfgiEnergy;
		env.SdfgiUseOcclusion = true;
		env.SdfgiBounceFeedback = settings.SdfgiBounceFeedback;
		env.SsilEnabled = settings.Ssil;

		// Las luces se vuelven a crear enteras: son pocas (burnville: 85) y así no queda estado viejo.
		if (_lightsNode is not null && GodotObject.IsInstanceValid(_lightsNode))
		{
			_parent.RemoveChild(_lightsNode);
			_lightsNode.Free();
		}
		_lightsNode = new Node3D { Name = "LucesBsp" };
		_parent.AddChild(_lightsNode);
		_lights.Clear();
		int off = 0;

		// Sol o luna: sundirection, suncolor, sunlight.
		if (!_parentHasSun && SunDirection is { } toSun && world is not null)
		{
			var scale = Override(byId, SunId, ref off);
			if (scale > 0f)
			{
				var c = LightColor(world.Get("suncolor"), Vector3.One);
				var sun = new DirectionalLight3D
				{
					Name = "Sol",
					LightColor = new Color(c.X, c.Y, c.Z) * settings.SunTint,
					LightEnergy = Num(world.Get("sunlight"), 1f) * settings.SunEnergyScale * scale,
					LightIndirectEnergy = settings.SunIndirectEnergy,
					ShadowEnabled = settings.SunShadows,
					LightAngularDistance = settings.SunAngularDistance,
				};
				_lightsNode.AddChild(sun);
				sun.LookAt(sun.GlobalPosition - toSun, Mathf.Abs(toSun.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
				_lights.Add(new BspLight(SunId, "sol", sun.GlobalPosition));
			}
		}

		// Entidades light: puntuales y focos (con target → info_null).
		var targets = new Dictionary<string, Vector3>(StringComparer.OrdinalIgnoreCase);
		foreach (var e in Bsp.Entities)
			if (e.Get("targetname") is { } tn && TryParseVector(e.Get("origin"), out var to)) targets[tn] = to;

		int points = 0, spots = 0;
		foreach (var e in Bsp.Entities.Where(e => e.ClassName == "light"))
		{
			if (!TryParseVector(e.Get("origin"), out var o)) continue;
			var id = LightId(e.Get("targetname"), e.Get("origin")!);
			var scale = Override(byId, id, ref off);
			if (scale <= 0f) continue;

			var intensity = Num(e.Get("light"), 300f); // valor por defecto de q3map: 300
			var c = LightColor(e.Get("_color"), Vector3.One);
			var color = new Color(c.X, c.Y, c.Z);
			var pos = ToGodot(o.X, o.Y, o.Z) * MetersPerUnit;

			Light3D light;
			float range;
			if (e.Get("target") is { } target && targets.TryGetValue(target, out var t))
			{
				// Foco: cono con radio "radius" (64 por defecto en q3map) a la distancia del destino.
				var dir = t - o;
				var dist = dir.Length();
				var radius = Num(e.Get("radius"), 64f);
				range = Mathf.Max(intensity, dist * 1.5f) * MetersPerUnit * settings.RangeScale;
				var spot = new SpotLight3D
				{
					Name = "Foco",
					LightColor = color,
					LightEnergy = settings.SpotEnergy * scale,
					LightIndirectEnergy = settings.SpotIndirectEnergy,
					SpotRange = range,
					SpotAngle = Mathf.Clamp(Mathf.RadToDeg(Mathf.Atan2(radius, Mathf.Max(dist, 1f))), 1f, 89f),
					ShadowEnabled = settings.SpotShadows && range >= settings.ShadowMinRange,
					Position = pos,
				};
				_lightsNode.AddChild(spot);
				var g = ToGodot(dir.X, dir.Y, dir.Z).Normalized();
				spot.LookAt(spot.GlobalPosition + g, Mathf.Abs(g.Y) > 0.99f ? Vector3.Forward : Vector3.Up);
				light = spot;
				spots++;
				_lights.Add(new BspLight(id, "foco", spot.GlobalPosition));
			}
			else
			{
				range = intensity * MetersPerUnit * settings.RangeScale;
				light = new OmniLight3D
				{
					Name = "Luz",
					LightColor = color,
					LightEnergy = settings.PointEnergy * scale,
					LightIndirectEnergy = settings.PointIndirectEnergy,
					OmniRange = range,
					ShadowEnabled = settings.PointShadows && range >= settings.ShadowMinRange,
					Position = pos,
				};
				_lightsNode.AddChild(light);
				points++;
				_lights.Add(new BspLight(id, "puntual", light.GlobalPosition));
			}
			light.DistanceFadeEnabled = settings.DistanceFade;
			light.DistanceFadeBegin = settings.DistanceFadeBegin;
			light.DistanceFadeShadow = settings.DistanceFadeShadow;
			light.DistanceFadeLength = settings.DistanceFadeLength;
		}
		GD.Print($"[MapBuilder] luces: {points} puntuales, {spots} focos, sol {(_parentHasSun ? "de la escena" : SunDirection is null ? "no" : "del mapa")}, " +
				 $"{off} desactivadas por ajuste, SDFGI {(env.SdfgiEnabled ? "sí" : "no")}");
	}

	/// <summary>Multiplicador de energía del ajuste de una luz: 0 si está desactivada, 1 si no tiene ajuste.</summary>
	private static float Override(Dictionary<string, MapLightOverride> byId, string id, ref int off)
	{
		if (!byId.TryGetValue(id, out var o)) return 1f;
		if (!o.Enabled) { off++; return 0f; }
		return Mathf.Max(o.EnergyScale, 0f);
	}

	/// <summary>
	/// Identificador de una luz del BSP para los ajustes por luz: su <c>targetname</c> o, si no tiene, su
	/// <c>origin</c> tal como lo escribe el BSP (espacios normalizados).
	/// </summary>
	public static string LightId(string? targetname, string origin) =>
		!string.IsNullOrWhiteSpace(targetname) ? targetname.Trim() : NormalizeId(origin);

	private static string NormalizeId(string id) =>
		string.Join(' ', id.Split(' ', StringSplitOptions.RemoveEmptyEntries));

	// ---------------------------------------------------------------- utilidades de entidades

	/// <summary>
	/// Color de luz de una entidad, normalizado como ColorNormalize de q3map (divide por la componente
	/// mayor): los mapas lo escriben en escala 0-1 o 0-255 (pavlov: "100 100 105") y la intensidad la
	/// pone solo sunlight / light.
	/// </summary>
	public static Vector3 LightColor(string? s, Vector3 fallback)
	{
		var v = TryParseVector(s, out var p) ? p : fallback;
		var max = Mathf.Max(v.X, Mathf.Max(v.Y, v.Z));
		return max > 0 ? v / max : v;
	}

	public static float Num(string? s, float fallback) =>
		s is not null && float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) ? v : fallback;

	public static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);

	public static bool TryParseVector(string? s, out Vector3 v)
	{
		v = Vector3.Zero;
		if (s is null) return false;
		var p = s.Split(' ', StringSplitOptions.RemoveEmptyEntries);
		if (p.Length != 3) return false;
		if (!float.TryParse(p[0], NumberStyles.Float, CultureInfo.InvariantCulture, out var x) ||
			!float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var y) ||
			!float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) return false;
		v = new Vector3(x, y, z);
		return true;
	}
}
