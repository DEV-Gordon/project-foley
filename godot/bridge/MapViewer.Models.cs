using System;
using System.Collections.Generic;
using System.Globalization;
using Foley.Formats.Bsp;
using Foley.Formats.Pk3;
using Foley.Formats.XModel;
using Godot;

/// <summary>
/// Modelos del mapa (xmodels de las entidades: misc_model, script_model, ítems, ametralladoras).
/// Solo presentación: la colisión de los misc_model ya está en los brushes del BSP
/// (docs/re/modelos.md). Los actores (actor_*) los creará la IA, no se dibujan aquí.
/// </summary>
public partial class MapViewer
{
	private void BuildModels(BspFile bsp, Pk3Collection pk3)
	{
		var instances = new Dictionary<string, List<Transform3D>>(StringComparer.OrdinalIgnoreCase);
		foreach (var e in bsp.Entities)
		{
			var model = e.Get("model");
			if (model is null || model.StartsWith('*')) continue;
			if (e.ClassName.StartsWith("actor_", StringComparison.Ordinal)) continue;
			if (!TryParseVector(e.Get("origin"), out var origin)) continue;

			var angles = Vector3.Zero; // pitch, yaw, roll en grados
			if (TryParseVector(e.Get("angles"), out var a)) angles = a;
			else if (e.Get("angle") is { } yaw) angles.Y = Parse(yaw);
			var scale = e.Get("modelscale") is { } ms ? Parse(ms) : 1f;

			if (!instances.TryGetValue(model, out var list)) instances[model] = list = new List<Transform3D>();
			list.Add(EntityTransform(origin, angles, scale));
		}

		int drawn = 0, failed = 0;
		foreach (var (path, transforms) in instances)
		{
			ArrayMesh? mesh;
			try
			{
				mesh = BuildModelMesh(XModelMesh.Load(pk3, path), pk3);
			}
			catch (Exception ex)
			{
				GD.PushWarning($"[MapViewer] no se pudo cargar {path}: {ex.Message}");
				failed++;
				continue;
			}
			var multi = new MultiMesh
			{
				TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
				Mesh = mesh,
				InstanceCount = transforms.Count,
			};
			for (var i = 0; i < transforms.Count; i++) multi.SetInstanceTransform(i, transforms[i]);
			AddChild(new MultiMeshInstance3D { Multimesh = multi, Name = path.Replace('/', '_') });
			drawn += transforms.Count;
		}
		GD.Print($"[MapViewer] modelos: {instances.Count} distintos, {drawn} instancias, {failed} sin cargar");
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

	private ArrayMesh BuildModelMesh(XModelMesh model, Pk3Collection pk3)
	{
		var mesh = new ArrayMesh();
		foreach (var s in model.Surfaces)
		{
			var n = s.Positions.Length / 3;
			var positions = new Vector3[n];
			var normals = new Vector3[n];
			var uv = new Vector2[n];
			for (var v = 0; v < n; v++)
			{
				positions[v] = ToGodot(s.Positions[v * 3], s.Positions[v * 3 + 1], s.Positions[v * 3 + 2]) * MetersPerUnit;
				normals[v] = ToGodot(s.Normals[v * 3], s.Normals[v * 3 + 1], s.Normals[v * 3 + 2]);
				uv[v] = new Vector2(s.UVs[v * 2], s.UVs[v * 2 + 1]);
			}
			var arrays = new Godot.Collections.Array();
			arrays.Resize((int)Mesh.ArrayType.Max);
			arrays[(int)Mesh.ArrayType.Vertex] = positions;
			arrays[(int)Mesh.ArrayType.Normal] = normals;
			arrays[(int)Mesh.ArrayType.TexUV] = uv;
			// Mismo giro que el BSP (docs/formats/xmodel.md).
			arrays[(int)Mesh.ArrayType.Index] = s.Triangles;
			mesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
			mesh.SurfaceSetMaterial(mesh.GetSurfaceCount() - 1, MakeSkinMaterial(pk3, s.Material));
		}
		return mesh;
	}

	private readonly Dictionary<string, Material> _skinMaterials = new(StringComparer.OrdinalIgnoreCase);

	private Material MakeSkinMaterial(Pk3Collection pk3, string skin)
	{
		if (_skinMaterials.TryGetValue(skin, out var cached)) return cached;
		var m = new StandardMaterial3D
		{
			Roughness = 1f,
			TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmapsAnisotropic,
		};
		var path = "skins/" + skin;
		var tex = pk3.Contains(path) ? LoadTexture(pk3, path) : null;
		if (tex is null)
		{
			m.AlbedoColor = new Color(1f, 0f, 1f);
		}
		else
		{
			m.AlbedoTexture = tex;
			if (_alphaTextures.Contains(path))
			{
				m.Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor;
				m.AlphaScissorThreshold = 0.5f;
			}
		}
		// Las tarjetas de follaje se ven por las dos caras (supuesto de presentación).
		if (skin.Contains("_masked@", StringComparison.OrdinalIgnoreCase))
			m.CullMode = BaseMaterial3D.CullModeEnum.Disabled;
		_skinMaterials[skin] = m;
		return m;
	}

	private static bool TryParseVector(string? s, out Vector3 v)
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
