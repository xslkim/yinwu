class_name TargetSpawner
extends Node3D

## 标靶生成器：按关卡波次表在靶道上派靶
## 只在 LevelManager 处于 playing 时推进时钟（由 main 每帧驱动 tick）

@export var lane_y := 2.0   ## 靶道高度
@export var lane_z := -6.0  ## 靶道纵深

var _scheduler := WaveScheduler.new()
var _active := false
var _elapsed := 0.0
var _alive: Array[Target] = []


## 关卡开始：装波次表、清零时钟
func start_level(def: Dictionary) -> void:
	clear()
	_scheduler.reset(def)
	_elapsed = 0.0
	_active = true


## 关卡结束/中断：停止派靶并收回场上所有靶
func stop() -> void:
	_active = false
	clear()


func clear() -> void:
	for t in _alive.duplicate():
		t.retract()
	_alive.clear()


## 由 main._process 调用（命中顿帧时 delta 会变小，靶子也一起慢动作 —— 这正是要的效果）
func tick(delta: float) -> void:
	if not _active:
		return
	_elapsed += delta
	for wave in _scheduler.poll(_elapsed):
		_spawn(wave)


func alive_targets() -> Array[Target]:
	return _alive


func _spawn(wave: Dictionary) -> void:
	var t := Target.new()
	t.target_type = {"normal": Target.Type.NORMAL, "hostage": Target.Type.HOSTAGE,
		"gold": Target.Type.GOLD}[wave.type]
	t.speed = wave.speed
	t.dwell = wave.dwell
	add_child(t)
	t.spawn_at(Vector3(wave.lane, lane_y, lane_z))
	t.expired.connect(_on_expired)
	t.tree_exited.connect(func(): _alive.erase(t))
	_alive.append(t)


func _on_expired(_t: Target) -> void:
	pass  # 超时逃走目前不惩罚；逃靶惩罚留给 Week 3 难度系统
