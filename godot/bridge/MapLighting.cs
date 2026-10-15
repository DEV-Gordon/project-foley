using Godot;

/// <summary>
/// Ajustes de la iluminación nueva de un mapa (<c>res://render/maps/&lt;mapa&gt;.tres</c>, o
/// <c>res://render/maps/default.tres</c> para los mapas sin archivo propio). Solo valores
/// artísticos: las luces en sí salen al cargar de las entidades del BSP (<c>light</c>, worldspawn), así
/// que este recurso no contiene datos del juego. Ver docs/decisions/0003-iluminacion.md.
/// </summary>
[GlobalClass]
public partial class MapLighting : Resource
{
	[ExportGroup("Luces del mapa (entidades light)")]
	/// <summary>Energía de las luces puntuales.</summary>
	[Export] public float PointEnergy { get; set; } = 2.0f;
	/// <summary>Alcance = valor <c>light</c> de la entidad (unidades) × este factor.</summary>
	[Export] public float RangeScale { get; set; } = 1.0f;
	/// <summary>Energía de los focos (entidades light con <c>target</c>).</summary>
	[Export] public float SpotEnergy { get; set; } = 3.0f;
	[Export] public bool PointShadows { get; set; } = true;
	[Export] public bool SpotShadows { get; set; } = true;

	[ExportGroup("Sol o luna (worldspawn)")]
	/// <summary>Energía = <c>sunlight</c> × este factor.</summary>
	[Export] public float SunEnergyScale { get; set; } = 1.0f;
	/// <summary>Multiplica el color del sol o la luna (<c>suncolor</c>); blanco = el del mapa.</summary>
	[Export] public Color SunTint { get; set; } = Colors.White;
	[Export] public bool SunShadows { get; set; } = true;
	/// <summary>Tamaño aparente del astro en grados: más grande, sombras más suaves (0 = duras).</summary>
	[Export(PropertyHint.Range, "0,10,0.1")] public float SunAngularDistance { get; set; } = 0.0f;

	[ExportGroup("Ambiente e iluminación global")]
	/// <summary>Multiplica la luz ambiente calculada del worldspawn (ambient, _color, sundiffusecolor).</summary>
	[Export] public float AmbientScale { get; set; } = 1.0f;
	[Export] public bool Sdfgi { get; set; } = true;
	[Export] public float SdfgiEnergy { get; set; } = 1.0f;
	/// <summary>Exposición del tonemap.</summary>
	[Export] public float Exposure { get; set; } = 1.0f;
}
