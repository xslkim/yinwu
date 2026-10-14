class_name Crosshair
extends Control

## 屏幕准星：跟随输入源的瞄准坐标；UDP 数据过期时变灰提示
## 调试模式可画出命中判定圈（靶心处的宽容度半径）

var aim := Vector2(960, 540)
var fresh := true
var show_hit_ring := false
var hit_ring_center := Vector2.ZERO
var hit_ring_radius := 60.0


func _process(_delta: float) -> void:
	queue_redraw()


func _draw() -> void:
	var color := Color.WHITE if fresh else Color(1, 1, 1, 0.3)
	# 十字 + 外圈，直播画面里要醒目
	draw_circle(aim, 10.0, color, false, 2.0)
	draw_line(aim + Vector2(-16, 0), aim + Vector2(-5, 0), color, 2.0)
	draw_line(aim + Vector2(5, 0), aim + Vector2(16, 0), color, 2.0)
	draw_line(aim + Vector2(0, -16), aim + Vector2(0, -5), color, 2.0)
	draw_line(aim + Vector2(0, 5), aim + Vector2(0, 16), color, 2.0)
	if show_hit_ring:
		draw_circle(hit_ring_center, hit_ring_radius, Color(0.2, 1.0, 0.4, 0.5), false, 2.0)
