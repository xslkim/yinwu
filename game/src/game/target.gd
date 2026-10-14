class_name Target
extends Node3D

## 水平往复移动的标靶：球形靶心 + 圆柱躯干拼装，卡通纯色
## 命中判定用靶心（bullseye）的世界坐标做屏幕空间距离判定

signal destroyed

@export var speed := 3.0      ## 移动速度（米/秒）
@export var move_range := 5.0 ## 以出生点为中心的往返幅度

var _origin := Vector3.ZERO
var _direction := 1.0
var _bullseye: Node3D
var _body: StaticBody3D


func _ready() -> void:
	_origin = position
	_build()


func _process(delta: float) -> void:
	position.x += _direction * speed * delta
	if absf(position.x - _origin.x) >= move_range:
		_direction *= -1.0  # 撞墙回头


## 靶心世界坐标（命中判定目标点）
func bullseye_position() -> Vector3:
	return _bullseye.global_position


func _build() -> void:
	# 躯干（卡通黄）
	var torso := MeshInstance3D.new()
	var torso_mesh := CylinderMesh.new()
	torso_mesh.top_radius = 0.35
	torso_mesh.bottom_radius = 0.45
	torso_mesh.height = 1.2
	torso.mesh = torso_mesh
	torso.position.y = -0.7
	torso.material_override = _flat(Color(1.0, 0.8, 0.2))
	add_child(torso)

	# 头 / 靶心（红白相间，一眼可读）
	_bullseye = Node3D.new()
	_bullseye.name = "Bullseye"
	add_child(_bullseye)

	var head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.45
	head_mesh.height = 0.9
	head_mesh.radial_segments = 12  # 低多边形感
	head_mesh.rings = 6
	head.mesh = head_mesh
	head.material_override = _flat(Color(1.0, 0.25, 0.2))
	_bullseye.add_child(head)

	var dot := MeshInstance3D.new()
	var dot_mesh := SphereMesh.new()
	dot_mesh.radius = 0.18
	dot_mesh.height = 0.36
	dot.mesh = dot_mesh
	dot.position.z = 0.35
	dot.material_override = _flat(Color.WHITE)
	_bullseye.add_child(dot)

	# 物理体：raycast 验遮挡用（形状略大于靶心，贴合整颗头）
	_body = StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = 0.5
	shape.shape = sphere
	_body.add_child(shape)
	_bullseye.add_child(_body)


func _flat(color: Color) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.roughness = 1.0  # 哑光卡通感
	return mat
