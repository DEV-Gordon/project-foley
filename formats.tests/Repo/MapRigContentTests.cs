using System.Text.RegularExpressions;

namespace Foley.Formats.Tests.Repo;

/// <summary>
/// Los rigs y ajustes de iluminación (<c>godot/render/maps/*.tscn</c> y <c>*.tres</c>) solo pueden
/// guardar ajustes y luces puestas a mano: nunca geometría ni texturas, que serían derivados del juego
/// (CLAUDE.md, regla dura 1). La vista previa del mapa en el editor no tiene Owner y no debe acabar en
/// el archivo; este test lo comprueba leyendo el texto de las escenas, sin Godot.
/// </summary>
public class MapRigContentTests
{
	private static readonly Regex Header = new(@"^\[(gd_scene|gd_resource|sub_resource|ext_resource)\b([^\]]*)\]", RegexOptions.Multiline);
	private static readonly Regex TypeAttr = new(@"\btype=""([^""]+)""");
	private static readonly Regex PathAttr = new(@"\bpath=""([^""]+)""");

	// Tipos de recurso con geometría o imágenes.
	private static readonly HashSet<string> ForbiddenTypes = new(StringComparer.Ordinal)
	{
		"ArrayMesh", "ImmediateMesh", "MultiMesh", "Mesh", "MeshLibrary", "ArrayOccluder3D", "OccluderPolygon2D",
		"Image", "ImageTexture", "ImageTexture3D", "ImageTextureLayered", "Texture2DArray", "Cubemap", "CubemapArray",
		"PortableCompressedTexture2D", "CompressedTexture2D", "CompressedTexture3D", "CompressedTexture2DArray",
		"ConcavePolygonShape3D", "ConvexPolygonShape3D", "HeightMapShape3D",
		"LightmapGIData", "VoxelGIData", "OccluderInstance3D",
	};

	// Datos en bruto típicos de mallas e imágenes.
	private static readonly string[] ForbiddenTokens =
	{
		"PackedByteArray(", "PackedVector2Array(", "PackedVector3Array(", "PackedColorArray(", "PackedFloat32Array(",
	};

	/// <summary>Problemas de un archivo .tscn/.tres; vacío si está limpio.</summary>
	public static List<string> Check(string text)
	{
		var problems = new List<string>();
		foreach (Match m in Header.Matches(text))
		{
			var attrs = m.Groups[2].Value;
			var type = TypeAttr.Match(attrs) is { Success: true } t ? t.Groups[1].Value : null;
			if (type is not null && IsForbiddenType(type))
				problems.Add($"recurso de tipo {type}");
			if (m.Groups[1].Value == "ext_resource" && PathAttr.Match(attrs) is { Success: true } p
				&& !p.Groups[1].Value.StartsWith("res://", StringComparison.Ordinal))
				problems.Add($"recurso externo fuera del proyecto: {p.Groups[1].Value}");
		}
		foreach (var token in ForbiddenTokens)
			if (text.Contains(token, StringComparison.Ordinal))
				problems.Add($"datos en bruto {token.TrimEnd('(')}");
		return problems;
	}

	private static bool IsForbiddenType(string type) =>
		ForbiddenTypes.Contains(type) || type.EndsWith("Mesh", StringComparison.Ordinal) || type.Contains("Texture", StringComparison.Ordinal);

	public static string MapsDirectory()
	{
		for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
			if (File.Exists(Path.Combine(dir.FullName, "godot", "project.godot")))
				return Path.Combine(dir.FullName, "godot", "render", "maps");
		throw new DirectoryNotFoundException("No se encontró godot/project.godot subiendo desde " + AppContext.BaseDirectory);
	}

	[Fact]
	public void RigsYAjustesNoContienenGeometriaNiTexturas()
	{
		var dir = MapsDirectory();
		var files = Directory.EnumerateFiles(dir, "*.*", SearchOption.AllDirectories)
			.Where(f => f.EndsWith(".tscn", StringComparison.OrdinalIgnoreCase) || f.EndsWith(".tres", StringComparison.OrdinalIgnoreCase))
			.ToList();
		Assert.NotEmpty(files); // al menos default.tres

		var failures = files
			.SelectMany(f => Check(File.ReadAllText(f)).Select(p => $"{Path.GetRelativePath(dir, f)}: {p}"))
			.ToList();
		Assert.True(failures.Count == 0, "Contenido prohibido en godot/render/maps:\n" + string.Join("\n", failures));
	}

	[Theory]
	[InlineData("[sub_resource type=\"ArrayMesh\" id=\"ArrayMesh_1\"]")]
	[InlineData("[sub_resource type=\"ImageTexture\" id=\"t\"]")]
	[InlineData("[sub_resource type=\"Image\" id=\"i\"]\ndata = {\"data\": PackedByteArray(0, 1, 2)}")]
	[InlineData("[sub_resource type=\"BoxMesh\" id=\"b\"]")]
	[InlineData("[ext_resource type=\"Texture2D\" path=\"res://x.png\" id=\"1\"]")]
	[InlineData("[sub_resource type=\"Shader\" id=\"s\"]\nfoo = PackedVector3Array(0, 0, 0)")]
	[InlineData("[ext_resource type=\"Script\" path=\"D:/juego/main/x.cs\" id=\"1\"]")]
	public void DetectaContenidoProhibido(string text) => Assert.NotEmpty(Check(text));

	[Fact]
	public void AceptaUnRigConLucesYAjustes()
	{
		const string rig = """
			[gd_scene format=3]

			[ext_resource type="Script" path="res://bridge/MapLightRig.cs" id="1_rig"]
			[ext_resource type="Script" path="res://bridge/MapLighting.cs" id="2_l"]

			[sub_resource type="Resource" id="Resource_1"]
			script = ExtResource("2_l")
			Exposure = 2.0

			[node name="burnville" type="Node3D"]
			script = ExtResource("1_rig")
			Iluminacion = SubResource("Resource_1")

			[node name="Farol" type="OmniLight3D" parent="."]
			transform = Transform3D(1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 2, 3)
			""";
		Assert.Empty(Check(rig));
	}
}
