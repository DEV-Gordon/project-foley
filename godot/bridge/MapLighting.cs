using System.Collections.Generic;
using Godot;

/// <summary>
/// Ajustes de la iluminación nueva de un mapa: <c>res://render/maps/default.tres</c> para todos y, si el
/// mapa tiene rig (<c>res://render/maps/&lt;mapa&gt;.tscn</c>, <see cref="MapLightRig"/>), los del rig.
/// Solo valores artísticos: las luces en sí salen al cargar de las entidades del BSP (<c>light</c>,
/// worldspawn), así que este recurso no contiene datos del juego. Cada cambio emite <c>changed</c> para
/// que la vista previa del editor se refresque. Ver docs/decisions/0003-iluminacion.md.
/// </summary>
[Tool] // el rig los lee en el editor; sin [Tool] serían instancias de marcador de posición
[GlobalClass]
public partial class MapLighting : Resource
{
	private float _pointEnergy = 2.0f, _rangeScale = 1.0f, _spotEnergy = 3.0f;
	private float _pointIndirect = 1.0f, _spotIndirect = 1.0f, _shadowMinRange;
	private bool _pointShadows = true, _spotShadows = true;
	private bool _distanceFade = true;
	private float _fadeBegin = 80f, _fadeShadow = 40f, _fadeLength = 10f;
	private float _sunEnergyScale = 1.0f, _sunIndirect = 1.0f, _sunAngular;
	private Color _sunTint = Colors.White;
	private bool _sunShadows = true;
	private float _ambientScale = 1.0f;
	private bool _ambientFloorEnabled;
	private Color _ambientFloor = new(0.02f, 0.02f, 0.03f);
	private bool _sdfgi = true, _ssil = true;
	private float _sdfgiEnergy = 1.0f, _sdfgiBounce = 0.5f;
	private Godot.Environment.ToneMapper _tonemap = Godot.Environment.ToneMapper.Agx;
	private float _exposure = 1.0f;
	private bool _glow = true;
	private float _glowIntensity = 0.8f;

	[ExportGroup("Luces del mapa (entidades light)")]
	/// <summary>Energía de las luces puntuales.</summary>
	[Export] public float PointEnergy { get => _pointEnergy; set => Change(ref _pointEnergy, value); }
	/// <summary>Multiplicador de la luz indirecta (SDFGI) de las puntuales.</summary>
	[Export] public float PointIndirectEnergy { get => _pointIndirect; set => Change(ref _pointIndirect, value); }
	/// <summary>Alcance = valor <c>light</c> de la entidad (unidades) × este factor.</summary>
	[Export] public float RangeScale { get => _rangeScale; set => Change(ref _rangeScale, value); }
	/// <summary>Energía de los focos (entidades light con <c>target</c>).</summary>
	[Export] public float SpotEnergy { get => _spotEnergy; set => Change(ref _spotEnergy, value); }
	/// <summary>Multiplicador de la luz indirecta (SDFGI) de los focos.</summary>
	[Export] public float SpotIndirectEnergy { get => _spotIndirect; set => Change(ref _spotIndirect, value); }
	[Export] public bool PointShadows { get => _pointShadows; set => Change(ref _pointShadows, value); }
	[Export] public bool SpotShadows { get => _spotShadows; set => Change(ref _spotShadows, value); }
	/// <summary>Las luces con alcance menor (metros) no dan sombra (0 = todas la dan).</summary>
	[Export(PropertyHint.Range, "0,30,0.1,or_greater,suffix:m")]
	public float ShadowMinRange { get => _shadowMinRange; set => Change(ref _shadowMinRange, value); }

	[ExportSubgroup("Desvanecido por distancia")]
	/// <summary>Las luces lejanas a la cámara se apagan (y antes pierden la sombra).</summary>
	[Export] public bool DistanceFade { get => _distanceFade; set => Change(ref _distanceFade, value); }
	/// <summary>Distancia (metros) a la que empieza a apagarse la luz.</summary>
	[Export(PropertyHint.Range, "0,500,1,or_greater,suffix:m")]
	public float DistanceFadeBegin { get => _fadeBegin; set => Change(ref _fadeBegin, value); }
	/// <summary>Distancia (metros) a la que la luz deja de dar sombra.</summary>
	[Export(PropertyHint.Range, "0,500,1,or_greater,suffix:m")]
	public float DistanceFadeShadow { get => _fadeShadow; set => Change(ref _fadeShadow, value); }
	/// <summary>Tramo (metros) en que la luz pasa de entera a apagada.</summary>
	[Export(PropertyHint.Range, "0,100,1,or_greater,suffix:m")]
	public float DistanceFadeLength { get => _fadeLength; set => Change(ref _fadeLength, value); }

	[ExportGroup("Sol o luna (worldspawn)")]
	/// <summary>Energía = <c>sunlight</c> × este factor.</summary>
	[Export] public float SunEnergyScale { get => _sunEnergyScale; set => Change(ref _sunEnergyScale, value); }
	/// <summary>Multiplicador de la luz indirecta (SDFGI) del astro.</summary>
	[Export] public float SunIndirectEnergy { get => _sunIndirect; set => Change(ref _sunIndirect, value); }
	/// <summary>Multiplica el color del sol o la luna (<c>suncolor</c>); blanco = el del mapa.</summary>
	[Export] public Color SunTint { get => _sunTint; set => Change(ref _sunTint, value); }
	[Export] public bool SunShadows { get => _sunShadows; set => Change(ref _sunShadows, value); }
	/// <summary>Tamaño aparente del astro en grados: más grande, sombras más suaves (0 = duras).</summary>
	[Export(PropertyHint.Range, "0,10,0.1")]
	public float SunAngularDistance { get => _sunAngular; set => Change(ref _sunAngular, value); }

	[ExportGroup("Ambiente e iluminación global")]
	/// <summary>Multiplica la luz ambiente calculada del worldspawn (ambient, _color, sundiffusecolor).</summary>
	[Export] public float AmbientScale { get => _ambientScale; set => Change(ref _ambientScale, value); }
	/// <summary>Si está activo, el color ambiente no baja de <see cref="AmbientFloor"/> (por componente).</summary>
	[Export] public bool AmbientFloorEnabled { get => _ambientFloorEnabled; set => Change(ref _ambientFloorEnabled, value); }
	/// <summary>Relleno ambiental mínimo (antes de multiplicar por <see cref="AmbientScale"/>).</summary>
	[Export(PropertyHint.ColorNoAlpha)] public Color AmbientFloor { get => _ambientFloor; set => Change(ref _ambientFloor, value); }
	[Export] public bool Sdfgi { get => _sdfgi; set => Change(ref _sdfgi, value); }
	[Export] public float SdfgiEnergy { get => _sdfgiEnergy; set => Change(ref _sdfgiEnergy, value); }
	/// <summary>Rebotes de luz del SDFGI (0 = uno solo; más alto, más luz rebotada).</summary>
	[Export(PropertyHint.Range, "0,1.99,0.01")]
	public float SdfgiBounceFeedback { get => _sdfgiBounce; set => Change(ref _sdfgiBounce, value); }
	/// <summary>Iluminación indirecta en espacio de pantalla.</summary>
	[Export] public bool Ssil { get => _ssil; set => Change(ref _ssil, value); }

	[ExportGroup("Cámara")]
	[Export] public Godot.Environment.ToneMapper Tonemap { get => _tonemap; set => Change(ref _tonemap, value); }
	/// <summary>Exposición del tonemap.</summary>
	[Export] public float Exposure { get => _exposure; set => Change(ref _exposure, value); }
	[Export] public bool Glow { get => _glow; set => Change(ref _glow, value); }
	[Export(PropertyHint.Range, "0,8,0.01")]
	public float GlowIntensity { get => _glowIntensity; set => Change(ref _glowIntensity, value); }

	private void Change<T>(ref T field, T value)
	{
		if (EqualityComparer<T>.Default.Equals(field, value)) return;
		field = value;
		EmitChanged();
	}
}
