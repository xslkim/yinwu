class_name DebugOverlay
extends CanvasLayer

## 调试浮层（F1 开关）：当前输入源、UDP 收包统计、热键提示
## 热键清单（全局）：
##   F1  调试浮层开关     F2  命中判定圈可视化
##   F9  切换输入源（鼠标 ↔ UDP）   F11 全屏切换

var _label: Label


func _ready() -> void:
	layer = 100  # 压在所有游戏 UI 之上
	_label = Label.new()
	_label.position = Vector2(20, 120)
	_label.add_theme_font_size_override("font_size", 22)
	_label.add_theme_color_override("font_color", Color(0.4, 1.0, 0.5))
	_label.add_theme_color_override("font_shadow_color", Color.BLACK)
	_label.add_theme_constant_override("shadow_offset_x", 2)
	_label.add_theme_constant_override("shadow_offset_y", 2)
	add_child(_label)
	visible = false


## 由主场景每帧喂数据
func update_info(provider: InputProvider) -> void:
	if not visible:
		return
	var lines := PackedStringArray()
	lines.append("输入源: %s（F9 切换）" % provider.provider_name())
	lines.append("准星: %s  新鲜: %s" % [provider.get_aim(), provider.is_aim_fresh()])
	if provider is UdpGunProvider:
		var age := -1
		if provider.last_packet_msec >= 0:
			age = Time.get_ticks_msec() - provider.last_packet_msec
		lines.append("UDP 收包: %d（坐标 %d / 事件 %d / 畸形 %d）" % [
			provider.packet_count, provider.aim_packet_count,
			provider.event_count, provider.invalid_count])
		lines.append("最后收包: %s ms 前" % (str(age) if age >= 0 else "从未"))
	lines.append("F1 浮层  F2 判定圈  F9 切输入源  F11 全屏")
	_label.text = "\n".join(lines)
