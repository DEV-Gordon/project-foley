using Godot;

/// <summary>
/// Ajuste de una luz concreta del BSP en un rig de mapa (<see cref="MapLightRig"/>): apagarla o
/// multiplicar su energía. Se aplica en la vista previa del editor y en el juego. La luz se identifica
/// por su <c>targetname</c> o, si no tiene, por su <c>origin</c> tal como lo escribe el BSP
/// ("x y z"); el sol o la luna es <c>sol</c>. Ver docs/decisions/0003-iluminacion.md.
/// </summary>
[Tool] // el rig los lee en el editor; sin [Tool] serían instancias de marcador de posición
[GlobalClass]
public partial class MapLightOverride : Resource
{
	private string _id = "";
	private bool _enabled = true;
	private float _energyScale = 1f;

	/// <summary><c>targetname</c> de la luz, su <c>origin</c> ("x y z") o <c>sol</c>.</summary>
	[Export] public string Id { get => _id; set { _id = value ?? ""; EmitChanged(); } }
	/// <summary>Desactivada: la luz no se crea.</summary>
	[Export] public bool Enabled { get => _enabled; set { _enabled = value; EmitChanged(); } }
	/// <summary>Multiplica la energía que le dan los ajustes del mapa.</summary>
	[Export(PropertyHint.Range, "0,10,0.05,or_greater")]
	public float EnergyScale { get => _energyScale; set { _energyScale = value; EmitChanged(); } }
}
