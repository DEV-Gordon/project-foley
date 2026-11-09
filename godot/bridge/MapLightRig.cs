using System;
using System.Linq;
using Godot;
using Godot.Collections;

/// <summary>
/// Rig de iluminación de un mapa: raíz de <c>res://render/maps/&lt;mapa&gt;.tscn</c>. Guarda los ajustes
/// del mapa (<see cref="Iluminacion"/>), los ajustes por luz del BSP (<see cref="AjustesPorLuz"/>) y,
/// como hijos normales, las luces puestas a mano, que son trabajo artístico propio del proyecto.
///
/// En el editor, "Vista previa del mapa" construye el mapa con <see cref="MapBuilder"/> (geometría,
/// modelos, cielo, luna, entorno y luces del BSP) como hijos internos <b>sin Owner</b>: no aparecen en
/// el árbol de la escena y nunca se guardan, así que la escena no contiene nada del juego (CLAUDE.md,
/// regla dura 1). En el juego, <see cref="MapViewer"/> instancia la escena en el origen y aplica sus
/// ajustes. Ver docs/decisions/0003-iluminacion.md y docs/flujo-iluminacion.md.
/// </summary>
[Tool]
[GlobalClass]
public partial class MapLightRig : Node3D
{
	public const string DefaultLightingPath = "res://render/maps/default.tres";
	// Escena de juego de la que la vista previa toma el entorno base (SSAO, SSR, niebla volumétrica).
	private const string GameScenePath = "res://jugar.tscn";
	private const string PreviewName = "VistaPreviaDelMapa";

	private string _mapa = "";
	private MapLighting? _iluminacion;
	private Array<MapLightOverride> _ajustesPorLuz = new();
	private readonly System.Collections.Generic.List<Resource> _subscribed = new();
	private bool _vistaPrevia;
	private bool _applyPending;
	private MapBuilder? _builder;

	/// <summary>Mapa del rig: "maps/burnville.bsp" o "burnville". Vacío = el nombre de la escena.</summary>
	[Export] public string Mapa { get => _mapa; set => _mapa = value ?? ""; }

	/// <summary>Ajustes del mapa. Un rig nuevo empieza con una copia de <c>default.tres</c>.</summary>
	[Export]
	public MapLighting? Iluminacion
	{
		get => _iluminacion;
		set { _iluminacion = value; Resubscribe(); QueueApply(); }
	}

	/// <summary>Luces del BSP apagadas o con la energía multiplicada (por <c>targetname</c>, <c>origin</c> o <c>sol</c>).</summary>
	[Export]
	public Array<MapLightOverride> AjustesPorLuz
	{
		get => _ajustesPorLuz;
		set { _ajustesPorLuz = value ?? new Array<MapLightOverride>(); Resubscribe(); QueueApply(); }
	}

	[ExportGroup("Editor")]
	/// <summary>Solo editor y nunca se guarda: construye el mapa sin Owner para iluminarlo a mano.</summary>
	[Export]
	public bool VistaPreviaDelMapa
	{
		get => _vistaPrevia;
		set
		{
			if (_vistaPrevia == value) return;
			_vistaPrevia = value;
			if (!Engine.IsEditorHint() || !IsInsideTree()) return;
			if (value) BuildPreview();
			else ClearPreview();
		}
	}

	[ExportToolButton("Refrescar")]
	public Callable RefrescarBoton => Callable.From(Refrescar);

	[ExportToolButton("Añadir ajuste de la luz del BSP más cercana a la cámara")]
	public Callable AjustarLuzCercanaBoton => Callable.From(AjustarLuzCercana);

	[ExportToolButton("Listar las luces del BSP en Salida")]
	public Callable ListarLucesBoton => Callable.From(ListarLuces);

	/// <summary>Los ajustes que valen: los del rig o, si no tiene, <c>default.tres</c>.</summary>
	public MapLighting EffectiveLighting() =>
		_iluminacion ?? LoadDefaultLighting() ?? new MapLighting();

	public static MapLighting? LoadDefaultLighting() =>
		ResourceLoader.Exists(DefaultLightingPath) ? ResourceLoader.Load<MapLighting>(DefaultLightingPath) : null;

	public override void _ValidateProperty(Dictionary property)
	{
		// La casilla se ve en el inspector pero no se guarda en la escena.
		if (property["name"].AsStringName() == PropertyName.VistaPreviaDelMapa)
			property["usage"] = (int)PropertyUsageFlags.Editor;
	}

	public override void _Ready()
	{
		if (!Engine.IsEditorHint()) return;
		// Un rig nuevo copia default.tres: lo que se ve en el inspector es exactamente lo que se guarda.
		if (_iluminacion is null && GetTree().EditedSceneRoot == this && LoadDefaultLighting() is { } d)
			Iluminacion = (MapLighting)d.Duplicate(true);
		if (_vistaPrevia && _builder is null) BuildPreview();
	}

	// ---------------------------------------------------------------- vista previa (solo editor)

	private void Refrescar()
	{
		if (!Engine.IsEditorHint()) return;
		if (_vistaPrevia) BuildPreview();
		else GD.Print("[MapLightRig] La vista previa está apagada: actívala con \"Vista previa del mapa\".");
	}

	private void BuildPreview()
	{
		if (!Engine.IsEditorHint()) return;
		ClearPreview();

		var mapPath = ResolveMapPath();
		if (mapPath is null)
		{
			GD.PushError("[MapLightRig] Falta el mapa: rellena 'Mapa' (p. ej. maps/burnville.bsp) o guarda la escena como res://render/maps/<mapa>.tscn.");
			TurnPreviewOff();
			return;
		}
		if (!MapBuilder.TryFindPk3Directory(out var pk3Dir, out var error))
		{
			GD.PushError($"[MapLightRig] {error} No se genera la vista previa.");
			TurnPreviewOff();
			return;
		}

		var preview = new Node3D { Name = PreviewName };
		// Hijo interno y sin Owner: ni se ve en el árbol de la escena ni se guarda.
		AddChild(preview, false, InternalMode.Back);
		try
		{
			_builder = MapBuilder.Build(mapPath, pk3Dir, EffectiveLighting(), _ajustesPorLuz, preview, GameEnvironment());
			GD.Print($"[MapLightRig] vista previa de {mapPath}: {_builder.Lights.Count} luces del BSP. No se guarda con la escena.");
		}
		catch (Exception e)
		{
			GD.PushError($"[MapLightRig] No se pudo construir {mapPath} (¿está el mapa en los pk3?): {e.GetType().Name}: {e.Message}");
			ClearPreview();
			TurnPreviewOff();
		}
	}

	private void ClearPreview()
	{
		_builder = null;
		foreach (var child in GetChildren(true))
		{
			if (child.Name.ToString() != PreviewName) continue;
			RemoveChild(child);
			child.QueueFree();
		}
	}

	private void TurnPreviewOff()
	{
		_vistaPrevia = false;
		NotifyPropertyListChanged();
	}

	/// <summary>Ruta lógica del mapa: <see cref="Mapa"/> o el nombre del archivo de la escena.</summary>
	public string? ResolveMapPath()
	{
		var name = _mapa.Trim();
		if (name.Length == 0 && !string.IsNullOrEmpty(SceneFilePath))
			name = System.IO.Path.GetFileNameWithoutExtension(SceneFilePath);
		if (name.Length == 0) return null;
		if (!name.Contains('/')) name = "maps/" + name;
		if (!name.EndsWith(".bsp", StringComparison.OrdinalIgnoreCase)) name += ".bsp";
		return name;
	}

	/// <summary>Entorno de la escena de juego (SSAO, SSR, niebla), para que la vista previa se le parezca.</summary>
	private static Godot.Environment? GameEnvironment()
	{
		if (!ResourceLoader.Exists(GameScenePath) || ResourceLoader.Load<PackedScene>(GameScenePath) is not { } scene) return null;
		var state = scene.GetState();
		for (var n = 0; n < state.GetNodeCount(); n++)
		{
			if (state.GetNodeType(n) != "WorldEnvironment") continue;
			for (var p = 0; p < state.GetNodePropertyCount(n); p++)
				if (state.GetNodePropertyName(n, p) == "environment" && state.GetNodePropertyValue(n, p).Obj is Godot.Environment env)
					return env;
		}
		return null;
	}

	// ---------------------------------------------------------------- refresco al cambiar un ajuste

	private void Resubscribe()
	{
		foreach (var r in _subscribed)
			if (IsInstanceValid(r)) r.Changed -= QueueApply;
		_subscribed.Clear();
		if (!Engine.IsEditorHint()) return;
		if (_iluminacion is not null) _subscribed.Add(_iluminacion);
		foreach (var o in _ajustesPorLuz)
			if (o is not null) _subscribed.Add(o);
		foreach (var r in _subscribed) r.Changed += QueueApply;
	}

	/// <summary>Junta varios cambios seguidos (p. ej. al arrastrar un deslizador) en una sola aplicación.</summary>
	private void QueueApply()
	{
		if (!Engine.IsEditorHint() || !_vistaPrevia || _applyPending || !IsInsideTree()) return;
		_applyPending = true;
		Callable.From(ApplyPending).CallDeferred();
	}

	private void ApplyPending()
	{
		_applyPending = false;
		if (!_vistaPrevia) return;
		if (_builder is null) { BuildPreview(); return; }
		_builder.ApplyLighting(EffectiveLighting(), _ajustesPorLuz);
	}

	public override void _Notification(int what)
	{
		if (what == NotificationPredelete)
			foreach (var r in _subscribed)
				if (IsInstanceValid(r)) r.Changed -= QueueApply;
	}

	// ---------------------------------------------------------------- ayudas para los ajustes por luz

	private void AjustarLuzCercana()
	{
#if TOOLS
		if (!Engine.IsEditorHint()) return;
		if (_builder is null)
		{
			GD.PushWarning("[MapLightRig] Activa primero la vista previa del mapa.");
			return;
		}
		var cam = EditorInterface.Singleton.GetEditorViewport3D(0)?.GetCamera3D();
		if (cam is null) return;
		var p = cam.GlobalPosition;
		var candidates = _builder.Lights.Where(l => l.Id != MapBuilder.SunId).ToList();
		if (candidates.Count == 0)
		{
			GD.Print("[MapLightRig] El mapa no tiene luces puntuales ni focos activos.");
			return;
		}
		var best = candidates.OrderBy(l => l.Position.DistanceSquaredTo(p)).First();
		var dist = best.Position.DistanceTo(p);
		if (_ajustesPorLuz.Any(o => o is not null && string.Equals(o.Id, best.Id, StringComparison.OrdinalIgnoreCase)))
		{
			GD.Print($"[MapLightRig] La luz {best.Kind} '{best.Id}' ({dist:0.0} m) ya tiene ajuste.");
			return;
		}
		var list = new Array<MapLightOverride>(_ajustesPorLuz) { new MapLightOverride { Id = best.Id } };
		AjustesPorLuz = list;
		NotifyPropertyListChanged();
		EditorInterface.Singleton.MarkSceneAsUnsaved();
		GD.Print($"[MapLightRig] Ajuste añadido para la luz {best.Kind} '{best.Id}' a {dist:0.0} m de la cámara. Edítalo en 'Ajustes por luz'.");
#endif
	}

	private void ListarLuces()
	{
		if (!Engine.IsEditorHint()) return;
		if (_builder is null)
		{
			GD.PushWarning("[MapLightRig] Activa primero la vista previa del mapa.");
			return;
		}
		foreach (var l in _builder.Lights)
			GD.Print($"[MapLightRig] {l.Kind,-8} id '{l.Id}'  posición Godot {l.Position.X:0.0}, {l.Position.Y:0.0}, {l.Position.Z:0.0}");
		GD.Print($"[MapLightRig] {_builder.Lights.Count} luces activas.");
	}
}
