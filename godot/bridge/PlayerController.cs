using System;
using System.Globalization;
using System.Linq;
using Foley.Formats.Bsp;
using Foley.Sim.Collision;
using Foley.Sim.Math;
using Foley.Sim.Movement;
using Godot;

/// <summary>
/// Puente jugador (hito 3): traduce teclado y ratón a comandos de usuario, avanza el pmove de
/// la simulación un paso de 8 ms por tick de física (125 Hz) con un reloj entero propio, y
/// coloca la cámara en la posición simulada. La presentación solo lee el estado.
/// </summary>
public partial class PlayerController : Node
{
	[Export] public NodePath MapViewerPath { get; set; } = "..";
	[Export] public NodePath CameraPath { get; set; } = "../Camera3D";
	[Export] public float MouseSensitivity { get; set; } = 0.15f; // grados por píxel (solo entrada)

	private Pmove? _pmove;
	private ClipMap? _map;
	private PlayerState _ps = new();
	private Vec3 _spawnOrigin;
	private float _spawnYaw;
	private int _simTime; // reloj de simulación en ms, entero

	// Ángulos de vista que pide el jugador (como cl.viewangles en el cliente de Q3), en grados.
	private float _viewPitch, _viewYaw;

	private Camera3D? _camera;
	private Label? _hud;

	public override void _Ready()
	{
		_camera = GetNodeOrNull<Camera3D>(CameraPath);
		var viewer = GetNodeOrNull<MapViewer>(MapViewerPath);
		if (viewer is null)
		{
			GD.PushError("[PlayerController] no se encontró el MapViewer.");
			return;
		}
		viewer.MapLoaded += OnMapLoaded;

		_hud = new Label { Position = new Vector2(12, 64) };
		_hud.AddThemeColorOverride("font_shadow_color", Colors.Black);
		var layer = new CanvasLayer();
		layer.AddChild(_hud);
		AddChild(layer);
	}

	private void OnMapLoaded(BspFile bsp)
	{
		_map = ClipMap.FromBsp(bsp);
		_pmove = new Pmove(new PmoveConfig(), _map);

		var start = bsp.Entities.FirstOrDefault(e => e.ClassName == "info_player_start");
		if (start?.Get("origin") is { } o)
		{
			var p = o.Split(' ', StringSplitOptions.RemoveEmptyEntries);
			_spawnOrigin = new Vec3(Parse(p[0]), Parse(p[1]), Parse(p[2]));
			_spawnYaw = Parse(start.Get("angle") ?? "0");
		}
		Respawn();
		GD.Print($"[PlayerController] {_map.Brushes.Length} brushes de colisión; inicio en {_spawnOrigin}");
	}

	private void Respawn()
	{
		if (_pmove is null) return;
		// info_player_start marca los pies; el origen de la caja (convención Q3) va por encima de mins.z.
		// TODO(fidelidad): convención de origen y caja de CoD1 por confirmar (docs/re/pmove.md).
		_ps = new PlayerState
		{
			CommandTime = _simTime,
			Origin = _spawnOrigin with { Z = _spawnOrigin.Z - _pmove.Config.Mins.Z + 1 },
			ViewAngles = new Vec3(0, _spawnYaw, 0),
			ViewHeight = _pmove.Config.ViewHeight,
			JumpTime = _simTime - _pmove.Config.JumpCooldown, // permite saltar desde el primer paso
		};
		_viewPitch = 0;
		_viewYaw = _spawnYaw;
		UpdateCamera();
		_camera?.ResetPhysicsInterpolation();
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		switch (@event)
		{
			case InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }:
				Input.MouseMode = Input.MouseModeEnum.Captured;
				break;
			case InputEventKey { Pressed: true, Keycode: Key.Escape }:
				Input.MouseMode = Input.MouseModeEnum.Visible;
				break;
			case InputEventKey { Pressed: true, Echo: false, Keycode: Key.R }:
				Respawn();
				break;
			case InputEventMouseMotion motion when Input.MouseMode == Input.MouseModeEnum.Captured:
				_viewYaw -= motion.Relative.X * MouseSensitivity;
				_viewPitch = Mathf.Clamp(_viewPitch + motion.Relative.Y * MouseSensitivity, -89f, 89f);
				break;
		}
	}

	public override void _PhysicsProcess(double delta)
	{
		// El delta de Godot no se usa: cada tick de física (125 Hz) es un paso fijo de 8 ms.
		if (_pmove is null) return;
		_simTime += _pmove.Config.MsecPerStep;

		sbyte forward = 0, right = 0, up = 0;
		if (Input.IsKeyPressed(Key.W)) forward += 127;
		if (Input.IsKeyPressed(Key.S)) forward -= 127;
		if (Input.IsKeyPressed(Key.D)) right += 127;
		if (Input.IsKeyPressed(Key.A)) right -= 127;
		if (Input.IsKeyPressed(Key.Space)) up = 127;

		var cmd = new UserCmd(_simTime,
			QMath.AngleToShort(_viewPitch), QMath.AngleToShort(_viewYaw), 0,
			forward, right, up);
		_pmove.Move(_ps, cmd);

		UpdateCamera();
	}

	private void UpdateCamera()
	{
		if (_camera is null) return;
		var eye = _ps.Origin with { Z = _ps.Origin.Z + _ps.ViewHeight };
		_camera.Position = MapViewer.SimToGodot(eye.X, eye.Y, eye.Z);
		// Q3: yaw gira de +X hacia +Y y pitch positivo mira abajo. Godot mira a -Z con Y arriba.
		_camera.RotationDegrees = new Vector3(-_ps.ViewAngles.X, _ps.ViewAngles.Y - 90f, 0);

		if (_hud is not null)
		{
			var v = _ps.Velocity;
			var speed = MathF.Sqrt(v.X * v.X + v.Y * v.Y);
			_hud.Text = $"Jugador: clic = capturar ratón, Esc = soltar, R = reiniciar | WASD + Espacio\n" +
						$"pos ({_ps.Origin.X:0.0}, {_ps.Origin.Y:0.0}, {_ps.Origin.Z:0.0})  vel {speed:0} u/s  " +
						$"{(_ps.OnGround ? "en suelo" : "en el aire")}  t={_simTime} ms";
		}
	}

	private static float Parse(string s) => float.Parse(s, CultureInfo.InvariantCulture);
}
