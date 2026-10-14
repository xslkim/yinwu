class_name Target
extends Node3D

## 标靶：三种靶型（普通/人质/金靶），弹出登场 + 到时缩回
## 命中判定由 main 做屏幕空间距离判定，本类只负责表现与生命周期
##
## 视觉区分（零素材程序化拼装）：
##   normal  红头黄身 + 白色靶心点 —— 加分
##   hostage 绿身白头 + 两点眼睛（"人脸"感）—— 误伤扣分
##   gold    整体 0.65 倍、纯金色、移动快 —— 高分限时

signal expired(target: Target)  ## 停留超时缩回完毕

enum Type { NORMAL, HOSTAGE, GOLD }

const POP_TIME := 0.25      ## 弹出动画时长
const RETRACT_TIME := 0.2   ## 缩回动画时长

var target_type := Type.NORMAL
var speed := 0.0            ## 水平移动速度（0 = 固定靶）
var dwell := 4.0            ## 停留秒数
var base_points := ScoreKeeper.BASE_POINTS

var _origin := Vector3.ZERO
var _direction := 1.0
var _bullseye: Node3D
var _alive := false         ## 弹出完成后才可被打
var _dying := false
var _dwell_left := 0.0


## 由生成器配置好后调用，播弹出动画
func spawn_at(pos: Vector3) -> void:
	position = pos
	_origin = pos
	scale = Vector3.ZERO
	_build()
	var tween := create_tween()
	tween.tween_property(self, "scale", Vector3.ONE, POP_TIME) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_OUT)
	tween.tween_callback(func(): _alive = true)
	_dwell_left = dwell


func _process(delta: float) -> void:
	if _dying or not _alive:
		return
	# 水平往复（在出生点 ±2.5m 内）
	if speed != 0.0:
		position.x += _direction * absf(speed) * delta
		if absf(position.x - _origin.x) >= 2.5:
			_direction *= -1.0
	# 到时缩回
	_dwell_left -= delta
	if _dwell_left <= 0.0:
		retract()


## 靶心世界坐标（命中判定目标点）
func bullseye_position() -> Vector3:
	return _bullseye.global_position


## 是否可被命中（弹出动画途中不算）
func is_hittable() -> bool:
	return _alive and not _dying


## 被击中：小幅后仰 + 缩回消失（粒子/跳分由 main 负责）
func destroy_with_hit() -> void:
	if _dying:
		return
	_dying = true
	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(self, "rotation_degrees:x", -25.0, 0.15)
	tween.tween_property(self, "scale", Vector3.ZERO, RETRACT_TIME) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
	tween.chain().tween_callback(queue_free)


## 到时缩回（未被击中）
func retract() -> void:
	if _dying:
		return
	_dying = true
	var tween := create_tween()
	tween.tween_property(self, "scale", Vector3.ZERO, RETRACT_TIME) \
		.set_trans(Tween.TRANS_BACK).set_ease(Tween.EASE_IN)
	tween.tween_callback(func(): expired.emit(self))
	tween.tween_callback(queue_free)


# ---------- 搭建 ----------

func _build() -> void:
	match target_type:
		Type.NORMAL:
			_build_body(Color(1.0, 0.8, 0.2), Color(1.0, 0.25, 0.2), true)
		Type.HOSTAGE:
			_build_body(Color(0.25, 0.75, 0.35), Color(0.95, 0.95, 0.9), false)
			_build_face()
		Type.GOLD:
			_build_body(Color(1.0, 0.75, 0.1), Color(1.0, 0.85, 0.15), true)
			_shrink_head(0.65)
			base_points = ScoreKeeper.GOLD_POINTS


func _build_body(torso_color: Color, head_color: Color, with_dot: bool) -> void:
	var torso := MeshInstance3D.new()
	var torso_mesh := CylinderMesh.new()
	torso_mesh.top_radius = 0.4
	torso_mesh.bottom_radius = 0.5
	torso_mesh.height = 1.4
	torso.mesh = torso_mesh
	torso.position.y = -0.8
	torso.material_override = _flat(torso_color)
	add_child(torso)
	_outline(torso)

	_bullseye = Node3D.new()
	_bullseye.name = "Bullseye"
	add_child(_bullseye)

	var head := MeshInstance3D.new()
	var head_mesh := SphereMesh.new()
	head_mesh.radius = 0.5
	head_mesh.height = 1.0
	head_mesh.radial_segments = 12  # 低多边形感
	head_mesh.rings = 6
	head.mesh = head_mesh
	head.material_override = _flat(head_color)
	_bullseye.add_child(head)
	_outline(head)

	if with_dot:
		var dot := MeshInstance3D.new()
		var dot_mesh := SphereMesh.new()
		dot_mesh.radius = 0.2
		dot_mesh.height = 0.4
		dot.mesh = dot_mesh
		dot.position.z = 0.4
		dot.material_override = _flat(Color.WHITE)
		_bullseye.add_child(dot)

	# 物理体：raycast 验遮挡用
	var body := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var sphere := SphereShape3D.new()
	sphere.radius = 0.55
	shape.shape = sphere
	body.add_child(shape)
	_bullseye.add_child(body)


## 人质的"脸"：两点黑眼睛（无素材时代的表情占位，Week 3 换贴图）
func _build_face() -> void:
	for x in [-0.16, 0.16]:
		var eye := MeshInstance3D.new()
		var eye_mesh := SphereMesh.new()
		eye_mesh.radius = 0.07
		eye_mesh.height = 0.14
		eye.mesh = eye_mesh
		eye.position = Vector3(x, 0.08, 0.44)
		eye.material_override = _flat(Color(0.1, 0.1, 0.15))
		_bullseye.add_child(eye)


## 金靶整体缩小（小目标 = 高难度高分）
func _shrink_head(ratio: float) -> void:
	_bullseye.scale = Vector3.ONE * ratio


func _flat(color: Color) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.roughness = 1.0  # 哑光卡通感
	return mat


## 卡通描边（inverted-hull，与 main.gd 同一风格约定）
func _outline(mi: MeshInstance3D, thickness := 0.035) -> void:
	var shell := MeshInstance3D.new()
	shell.mesh = mi.mesh
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.08, 0.08, 0.12)
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.cull_mode = BaseMaterial3D.CULL_FRONT
	mat.grow_enabled = true
	mat.grow_amount = thickness
	shell.material_override = mat
	mi.add_child(shell)
