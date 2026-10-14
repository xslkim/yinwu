extends Node3D

## ArcGun 主场景：Week 2 垂直切片
##   程序化低多边形场景（地面/背景板/几何装饰，纯色卡通 + inverted-hull 描边）
##   微关卡串烧：attract → 标题闪现 → 倒计时开打 → 单关结算 → 总结算页
##   多靶系统：波次表驱动生成器，普通/人质/金靶三型
## 输入纪律：本脚本只通过 InputProvider 接口拿输入，绝不直接摸 Input 单例
##   唯一例外：F1/F2/F9/F11 调试热键属于"系统层"，不算游戏逻辑
## 启动参数：`-- --udp` UDP 光枪源；`--freeze-target` 定量联调（钉死单靶）；
##   `--auto-shots` 每 60 帧自截屏到 D:/yinwu/temp/arcgun-shots/auto/（锁屏也能验收画面）

@export var hit_tolerance := 60.0  ## 命中宽容度（准星到靶心的屏幕像素距离）

var _camera: Camera3D
var _crosshair: Crosshair
var _hud: Hud
var _level_ui: LevelUi
var _overlay: DebugOverlay
var _audio: PlaceholderAudio
var _flash_light: OmniLight3D
var _particles: GPUParticles3D
var _spawner: TargetSpawner

var _provider: InputProvider
var _score := ScoreKeeper.new()
var _ammo := AmmoManager.new()
var _levels: LevelManager
var _show_hit_ring := false
var _hitstop_active := false  ## 顿帧防重入
var _freeze_target: Target    ## 定量联调模式下的钉死靶
var _freeze_report := false
var _auto_shots := false    ## 自截屏验收模式
var _shot_frame := 0


func _ready() -> void:
	_build_world()
	_build_effects()
	_build_ui()
	var args := OS.get_cmdline_user_args()
	if "--freeze-target" in args:
		# 定量联调：不走关卡流程，场上只有一个永不缩回的固定靶
		_freeze_target = Target.new()
		_freeze_target.speed = 0.0
		_freeze_target.dwell = 9999.0
		add_child(_freeze_target)
		_freeze_target.spawn_at(Vector3(0, 2.0, -6))
		_freeze_report = true
		_level_ui.show_title({"title": "FREEZE TEST", "goal": 0, "duration": 0.0})
	else:
		_build_level_flow()
	# 命令行 `--udp` 以手机光枪源启动，否则默认鼠标源
	_switch_provider("--udp" in args)
	_auto_shots = "--auto-shots" in args
	if _auto_shots:
		DirAccess.make_dir_recursive_absolute("D:/yinwu/temp/arcgun-shots/auto")
	_hud.set_score(0)
	_hud.set_ammo(_ammo.current, _ammo.capacity)


func _exit_tree() -> void:
	# 定量联调：退出时打一行统计（headless 跑命中率用）
	var total := _score.hits + _score.misses
	print("[Stats] shots=%d hits=%d accuracy=%.2f score=%d" % [
		total, _score.hits, _score.accuracy(), _score.score])


func _process(delta: float) -> void:
	if _freeze_report:
		_freeze_report = false
		var p := _camera.unproject_position(_freeze_target.bullseye_position())
		print("[Test] 靶心屏幕坐标=(%.1f,%.1f)" % [p.x, p.y])
	if _levels:
		_levels.tick(delta)
		if _levels.state == "playing":
			_spawner.tick(delta)
			_level_ui.update_timer(_levels.time_ratio())
	_crosshair.aim = _provider.get_aim()
	_crosshair.fresh = _provider.is_aim_fresh()
	_crosshair.show_hit_ring = _show_hit_ring
	if _show_hit_ring:
		var nearest := _nearest_target(_provider.get_aim())
		if nearest:
			_crosshair.hit_ring_center = _camera.unproject_position(nearest.bullseye_position())
			_crosshair.hit_ring_radius = hit_tolerance
		else:
			_crosshair.show_hit_ring = false
	_overlay.update_info(_provider)
	if _auto_shots:
		_shot_frame += 1
		if _shot_frame % 60 == 0:
			# 自截屏：引擎内读回视口，锁屏/遮挡环境下也能验收画面
			var img := get_viewport().get_texture().get_image()
			img.save_png("D:/yinwu/temp/arcgun-shots/auto/shot_%04d.png" % (_shot_frame / 60))


func _unhandled_input(event: InputEvent) -> void:
	# 系统/调试热键（游戏逻辑的输入全部走 InputProvider，此处是允许的例外）
	if event is InputEventKey and event.pressed and not event.echo:
		match event.keycode:
			KEY_F1: _overlay.visible = not _overlay.visible
			KEY_F2: _show_hit_ring = not _show_hit_ring
			KEY_F9: _switch_provider(_provider is MouseGunProvider)
			KEY_F11: _toggle_fullscreen()


# ---------- 关卡流程接线 ----------

func _build_level_flow() -> void:
	_spawner = TargetSpawner.new()
	add_child(_spawner)
	_levels = LevelManager.new(LevelDefs.all(), _score)
	# 调试跳关：`--level 2` 直接从中途开打（验收人质/金靶用）
	for a in OS.get_cmdline_user_args():
		if a.begins_with("--level="):
			_levels.start_at = clampi(int(a.trim_prefix("--level=")) - 1, 0, 2)
	_levels.level_title_shown.connect(func(_i, def): _level_ui.show_title(def))
	_levels.level_started.connect(_on_level_started)
	_levels.level_finished.connect(_on_level_finished)
	_levels.game_finished.connect(_on_game_finished)
	_level_ui.show_attract(_levels.credits)


func _on_level_started(_idx: int, def: Dictionary) -> void:
	_level_ui.show_playing(def)
	_spawner.start_level(def)
	_ammo.reload()  # 街机惯例：每关开始满弹
	_hud.set_ammo(_ammo.current, _ammo.capacity)


func _on_level_finished(_idx: int, passed: bool, level_score: int) -> void:
	_spawner.stop()
	_level_ui.show_level_result(passed, level_score, _levels.current_level().goal)
	_audio.blip(660.0 if passed else 180.0, 0.2, 0.3)


func _on_game_finished(win: bool) -> void:
	_level_ui.show_gameover(win, _score)


# ---------- 输入源管理 ----------

func _switch_provider(use_udp: bool) -> void:
	if _provider:
		_provider.queue_free()
	if use_udp:
		_provider = UdpGunProvider.new()
	else:
		_provider = MouseGunProvider.new()
	add_child(_provider)
	_provider.shot.connect(_on_shot)
	_provider.reloaded.connect(_on_reload)
	_provider.coin.connect(_on_coin)
	_provider.started.connect(_on_start)
	_provider.exited.connect(_on_exit)
	print("[Main] 输入源切换为 %s" % _provider.provider_name())


func _on_coin() -> void:
	if _levels == null:
		return
	_levels.insert_coin()
	_audio.blip(990.0, 0.1, 0.3)
	if _levels.state == "attract":
		_level_ui.show_attract(_levels.credits)


func _on_start() -> void:
	if _levels == null:
		return
	if _levels.press_start():
		_audio.blip(1320.0, 0.12, 0.3)
	else:
		_audio.blip(160.0, 0.09, 0.3)  # 没币/没到时候的低哑提示


func _on_exit() -> void:
	if _levels == null:
		return
	if _levels.state == "attract":
		get_tree().quit()  # 待机页再按退出 = 退出进程（电视端遥控器场景）
		return
	if _levels.abort():
		_spawner.stop()
		_level_ui.show_attract(_levels.credits)
		_audio.blip(160.0, 0.09, 0.3)


# ---------- 核心射击循环 ----------

func _on_shot() -> void:
	# 只在开打状态响应射击（待机/结算页开枪不耗弹不计分）
	var in_play := _levels == null or _levels.state == "playing"
	if not in_play:
		return
	if not _ammo.consume():
		_audio.blip(160.0, 0.09, 0.3)  # 空仓低哑声
		_hud.set_ammo(0, _ammo.capacity)
		return
	_hud.set_ammo(_ammo.current, _ammo.capacity)
	_audio.blip(880.0, 0.06, 0.3)
	_flash_muzzle()

	var aim := _provider.get_aim()
	var target := _nearest_target(aim)
	if target and not _is_occluded(aim, target):
		if target.target_type == Target.Type.HOSTAGE:
			_score.register_hostage_hit()
			_hud.set_score(_score.score)
			_hud.set_combo(0)
			_spawn_score_label(aim, "-%d" % ScoreKeeper.HOSTAGE_PENALTY, Color(1.0, 0.3, 0.25))
			_audio.blip(140.0, 0.15, 0.35)  # 误伤低沉闷响
		else:
			var gained := _score.register_hit(target.base_points)
			_hud.set_score(_score.score)
			_hud.set_combo(_score.combo)
			_spawn_hit_feedback(aim, target, gained)
			_hitstop()
		# 联调钉死靶打不烂（要反复测命中率）；正常靶命中即销毁
		if target != _freeze_target:
			target.destroy_with_hit()
	else:
		_score.register_miss()
		_hud.set_combo(0)


func _on_reload() -> void:
	_ammo.reload()
	_hud.set_ammo(_ammo.current, _ammo.capacity)
	_audio.blip(440.0, 0.05, 0.25)
	_audio.blip(660.0, 0.05, 0.25)


## 屏幕上离准星最近且在宽容度内的可打标靶；没有则 null
func _nearest_target(aim: Vector2) -> Target:
	var best: Target = null
	var best_dist := hit_tolerance
	for t in _all_targets():
		if not t.is_hittable():
			continue
		var d := aim.distance_to(_camera.unproject_position(t.bullseye_position()))
		if d <= best_dist:
			best_dist = d
			best = t
	return best


func _all_targets() -> Array:
	if _freeze_target:
		return [_freeze_target]
	return _spawner.alive_targets() if _spawner else []


## 从准星位置打一条射线：只有"比标靶更近的遮挡物"才算挡住
## （宽容度圈大于靶心碰撞球，射线常会擦过球体打中后面的墙，不能算遮挡）
func _is_occluded(aim: Vector2, target: Target) -> bool:
	var from := _camera.project_ray_origin(aim)
	var to := from + _camera.project_ray_normal(aim) * 100.0
	var query := PhysicsRayQueryParameters3D.create(from, to)
	var hit := get_world_3d().direct_space_state.intersect_ray(query)
	if hit.is_empty():
		return false
	var target_dist := from.distance_to(target.bullseye_position())
	# 命中点在靶心前方 0.8m 以内才算真遮挡（0.8 ≈ 碰撞球半径 + 余量）
	return from.distance_to(hit.position) < target_dist - 0.8


# ---------- 命中反馈三件套 ----------

## ① 顿帧 60ms（真实时间，不受 time_scale 影响；连续命中不叠加）
func _hitstop() -> void:
	if _hitstop_active:
		return
	_hitstop_active = true
	Engine.time_scale = 0.05
	await get_tree().create_timer(0.06, true, false, true).timeout
	Engine.time_scale = 1.0
	_hitstop_active = false


## ② 枪口火光：动态光源闪一下
func _flash_muzzle() -> void:
	_flash_light.light_energy = 8.0
	await get_tree().create_timer(0.05, true, false, true).timeout
	_flash_light.light_energy = 0.0


## ③ 命中粒子 + 跳分数字
func _spawn_hit_feedback(aim: Vector2, target: Target, gained: int) -> void:
	# 粒子打在准星对应的 3D 射线与靶面交点处（近似取靶心深度）
	var depth := _camera.global_position.distance_to(target.bullseye_position())
	var pos := _camera.project_ray_origin(aim) + _camera.project_ray_normal(aim) * depth
	_particles.global_position = pos
	_particles.restart()
	_spawn_score_label(aim, "+%d" % gained, Color(1.0, 0.9, 0.2), target)


func _spawn_score_label(aim: Vector2, text: String, color: Color, target: Target = null) -> void:
	var depth := _camera.global_position.distance_to(
		target.bullseye_position() if target else Vector3(0, 2.0, -6))
	var pos := _camera.project_ray_origin(aim) + _camera.project_ray_normal(aim) * depth
	var label := Label3D.new()
	label.text = text
	label.font_size = 200
	label.outline_size = 24
	label.modulate = color
	label.outline_modulate = Color(0.08, 0.08, 0.12)
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.global_position = pos + Vector3(0, 0.3, 0)
	add_child(label)
	var tween := create_tween()
	tween.set_parallel(true)
	tween.tween_property(label, "global_position:y", pos.y + 1.5, 0.6)
	tween.tween_property(label, "modulate:a", 0.0, 0.6)
	tween.chain().tween_callback(label.queue_free)


func _toggle_fullscreen() -> void:
	var win := get_window()
	win.mode = Window.MODE_FULLSCREEN \
		if win.mode != Window.MODE_FULLSCREEN else Window.MODE_WINDOWED


# ---------- 场景搭建（程序化，零素材） ----------

func _build_world() -> void:
	# 环境光 + 高饱和天蓝背景（卡通基调）
	var env := WorldEnvironment.new()
	var e := Environment.new()
	e.background_mode = Environment.BG_COLOR
	e.background_color = Color(0.45, 0.78, 0.95)
	e.ambient_light_source = Environment.AMBIENT_SOURCE_COLOR
	e.ambient_light_color = Color.WHITE
	e.ambient_light_energy = 0.45
	env.environment = e
	add_child(env)

	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-50, -30, 0)
	sun.light_energy = 0.9
	sun.shadow_enabled = true  # 有阴影才有立体感
	add_child(sun)

	# 固定机位：压低视角贴近靶场，减少空旷地面占比
	_camera = Camera3D.new()
	_camera.position = Vector3(0, 2.4, 7.5)
	_camera.rotation_degrees = Vector3(-11, 0, 0)
	add_child(_camera)

	# 地面（深卡通绿）
	var ground := MeshInstance3D.new()
	var ground_mesh := PlaneMesh.new()
	ground_mesh.size = Vector2(40, 30)
	ground.mesh = ground_mesh
	ground.material_override = _flat(Color(0.18, 0.5, 0.22))
	add_child(ground)
	_add_collider(ground, Vector3(40, 0.2, 30), Vector3(0, -0.1, 0))

	# 靶道色带：浅绿条带标示射击区，把视线引向标靶
	var lane := MeshInstance3D.new()
	var lane_mesh := PlaneMesh.new()
	lane_mesh.size = Vector2(16, 30)
	lane.mesh = lane_mesh
	lane.position = Vector3(0, 0.02, 0)
	lane.material_override = _flat(Color(0.38, 0.68, 0.28))
	add_child(lane)

	# 背景板（矮墙不抢戏：暖橙墙 + 顶部深红压边）
	var wall := MeshInstance3D.new()
	var wall_mesh := BoxMesh.new()
	wall_mesh.size = Vector3(40, 6, 0.5)
	wall.mesh = wall_mesh
	wall.position = Vector3(0, 3, -10)
	wall.material_override = _flat(Color(0.9, 0.45, 0.15))
	add_child(wall)
	_add_collider(wall, Vector3(40, 6, 0.5), Vector3.ZERO)
	var trim := MeshInstance3D.new()
	var trim_mesh := BoxMesh.new()
	trim_mesh.size = Vector3(40, 0.6, 0.7)
	trim.mesh = trim_mesh
	trim.position = Vector3(0, 6.1, -10)
	trim.material_override = _flat(Color(0.6, 0.18, 0.12))
	add_child(trim)

	# 远山剪影：两侧大圆锥，深青色压出层次
	_add_deco_cone(Vector3(-14, 0, -9), 5.0, 6.0, Color(0.12, 0.32, 0.38))
	_add_deco_cone(Vector3(14, 0, -9), 6.0, 7.5, Color(0.1, 0.28, 0.35))

	# 几何装饰：两侧低多边形盒子/圆柱，全部带描边
	_add_deco_box(Vector3(-7, 0.8, -4), Color(0.85, 0.3, 0.5))
	_add_deco_box(Vector3(7.5, 0.6, -5), Color(0.3, 0.6, 0.95))
	_add_deco_cylinder(Vector3(-5.5, 1.2, -8), Color(0.95, 0.75, 0.2))
	_add_deco_cylinder(Vector3(6, 1.0, -8.5), Color(0.55, 0.35, 0.75))


func _build_effects() -> void:
	# 枪口光源（平时熄灭，开火瞬间点亮）
	_flash_light = OmniLight3D.new()
	_flash_light.position = Vector3(0, 1.5, 7)
	_flash_light.light_color = Color(1.0, 0.8, 0.4)
	_flash_light.light_energy = 0.0
	_flash_light.omni_range = 12.0
	add_child(_flash_light)

	# 命中粒子：一次性迸发的大颗粒亮块，远机位也要看得见
	_particles = GPUParticles3D.new()
	_particles.amount = 40
	_particles.lifetime = 0.7
	_particles.one_shot = true
	_particles.explosiveness = 1.0
	_particles.emitting = false
	var pm := ParticleProcessMaterial.new()
	pm.direction = Vector3(0, 1, 0)
	pm.spread = 70.0
	pm.initial_velocity_min = 4.0
	pm.initial_velocity_max = 9.0
	pm.gravity = Vector3(0, -9.8, 0)
	pm.scale_min = 0.12
	pm.scale_max = 0.28
	pm.color = Color(1.0, 0.85, 0.2)
	_particles.process_material = pm
	var quad := BoxMesh.new()
	quad.size = Vector3(0.16, 0.16, 0.16)
	var quad_mat := StandardMaterial3D.new()
	quad_mat.albedo_color = Color(1.0, 0.85, 0.2)
	quad_mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED  # 自发光感
	quad.material = quad_mat
	_particles.draw_pass_1 = quad
	add_child(_particles)

	_audio = PlaceholderAudio.new()
	add_child(_audio)


func _build_ui() -> void:
	_hud = Hud.new()
	add_child(_hud)
	_crosshair = Crosshair.new()
	_crosshair.set_anchors_preset(Control.PRESET_FULL_RECT)
	_crosshair.mouse_filter = Control.MOUSE_FILTER_IGNORE
	var ui := CanvasLayer.new()
	ui.layer = 10
	ui.add_child(_crosshair)
	add_child(ui)
	_level_ui = LevelUi.new()
	add_child(_level_ui)
	_overlay = DebugOverlay.new()
	add_child(_overlay)


# ---------- 小工具 ----------

func _flat(color: Color) -> StandardMaterial3D:
	var mat := StandardMaterial3D.new()
	mat.albedo_color = color
	mat.roughness = 1.0
	return mat


func _add_collider(visual: MeshInstance3D, size: Vector3, offset: Vector3) -> void:
	var body := StaticBody3D.new()
	var shape := CollisionShape3D.new()
	var box := BoxShape3D.new()
	box.size = size
	shape.shape = box
	shape.position = offset
	body.add_child(shape)
	visual.add_child(body)


func _add_deco_box(pos: Vector3, color: Color) -> void:
	var m := MeshInstance3D.new()
	var mesh := BoxMesh.new()
	mesh.size = Vector3(1.2, 1.6, 1.2)
	m.mesh = mesh
	m.position = pos
	m.rotation_degrees.y = randf_range(0, 45)
	m.material_override = _flat(color)
	add_child(m)
	_add_outline(m)


func _add_deco_cylinder(pos: Vector3, color: Color) -> void:
	var m := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.5
	mesh.bottom_radius = 0.7
	mesh.height = 2.4
	mesh.radial_segments = 8  # 低多边形感
	m.mesh = mesh
	m.position = pos
	m.material_override = _flat(color)
	add_child(m)
	_add_outline(m)


func _add_deco_cone(pos: Vector3, radius: float, height: float, color: Color) -> void:
	var m := MeshInstance3D.new()
	var mesh := CylinderMesh.new()
	mesh.top_radius = 0.0
	mesh.bottom_radius = radius
	mesh.height = height
	mesh.radial_segments = 6  # 大棱锥剪影
	m.mesh = mesh
	m.position = pos + Vector3(0, height * 0.5, 0)
	m.material_override = _flat(color)
	add_child(m)


## 卡通描边：inverted-hull 法（同一 mesh 背面剔除 + 沿法线外扩 + 黑色无光照）
func _add_outline(mi: MeshInstance3D, thickness := 0.04) -> void:
	var shell := MeshInstance3D.new()
	shell.mesh = mi.mesh
	var mat := StandardMaterial3D.new()
	mat.albedo_color = Color(0.08, 0.08, 0.12)  # 近黑的深蓝，比纯黑柔和
	mat.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	mat.cull_mode = BaseMaterial3D.CULL_FRONT
	mat.grow = true  # Godot 4 属性名（Godot 3 的 grow_enabled 已改名，旧名只剩告警不生效）
	mat.grow_amount = thickness
	shell.material_override = mat
	mi.add_child(shell)
