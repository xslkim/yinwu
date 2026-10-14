class_name MouseGunProvider
extends InputProvider

## 鼠标光枪源（开发调试用）：
##   隐藏系统光标，鼠标移动映射准星，左键开火，R 键换弹

var _aim := Vector2(960, 540)


func _ready() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_HIDDEN


func _exit_tree() -> void:
	Input.mouse_mode = Input.MOUSE_MODE_VISIBLE


func _unhandled_input(event: InputEvent) -> void:
	if event is InputEventMouseMotion:
		_aim = event.position
	elif event is InputEventMouseButton and event.pressed \
			and event.button_index == MOUSE_BUTTON_LEFT:
		shot.emit()
	elif event is InputEventKey and event.pressed and not event.echo:
		if event.keycode == KEY_R:
			reloaded.emit()


func get_aim() -> Vector2:
	# 夹到可视范围内，防止鼠标甩出窗口后准星丢失
	var vp := get_viewport().get_visible_rect().size
	return Vector2(clampf(_aim.x, 0.0, vp.x), clampf(_aim.y, 0.0, vp.y))


func provider_name() -> String:
	return "Mouse"
