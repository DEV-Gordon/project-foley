extends Camera3D
## Cámara libre de inspección (solo presentación).
## WASD mueve, Q/E baja/sube, Shift acelera, botón derecho + ratón gira.

@export var speed: float = 12.0
@export var fast_multiplier: float = 4.0
@export var mouse_sensitivity: float = 0.1

var _looking := false


func _ready() -> void:
	far = 3000.0
	near = 0.05


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseButton and event.button_index == MOUSE_BUTTON_RIGHT:
		_looking = event.pressed
		Input.mouse_mode = Input.MOUSE_MODE_CAPTURED if _looking else Input.MOUSE_MODE_VISIBLE
	elif event is InputEventMouseMotion and _looking:
		rotation_degrees.y -= event.relative.x * mouse_sensitivity
		rotation_degrees.x = clampf(rotation_degrees.x - event.relative.y * mouse_sensitivity, -89.0, 89.0)


func _process(delta: float) -> void:
	var dir := Vector3.ZERO
	if Input.is_key_pressed(KEY_W): dir -= global_transform.basis.z
	if Input.is_key_pressed(KEY_S): dir += global_transform.basis.z
	if Input.is_key_pressed(KEY_A): dir -= global_transform.basis.x
	if Input.is_key_pressed(KEY_D): dir += global_transform.basis.x
	if Input.is_key_pressed(KEY_E): dir += Vector3.UP
	if Input.is_key_pressed(KEY_Q): dir -= Vector3.UP
	if dir == Vector3.ZERO:
		return
	var s := speed * (fast_multiplier if Input.is_key_pressed(KEY_SHIFT) else 1.0)
	global_position += dir.normalized() * s * delta
