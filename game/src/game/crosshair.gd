class_name Crosshair
extends Control

## 屏幕准星：跟随输入源的瞄准坐标；UDP 数据过期时变灰提示
## 黑边白心双层设计，保证在任何背景色上都醒目
## 调试模式可画出命中判定圈（靶心处的宽容度半径）

var aim := Vector2(960, 540)
var fresh := true
var show_hit_ring := false
var hit_ring_center := Vector2.ZERO
var hit_ring_radius := 60.0

var _texture: Texture2D  ## assets/textures/crosshair.png 存在则用贴图准星


func _ready() -> void:
	_texture = AssetLib.texture_or_null("res://assets/textures/ui_crosshair.png")


func _process(_delta: float) -> void:
	queue_redraw()


func _draw() -> void:
	var main_color := Color.WHITE if fresh else Color(1, 1, 1, 0.35)
	if _texture:
		# 贴图准星：72px 见方居中（draw_texture 不缩放，必须用 rect 版）
		draw_texture_rect(_texture, Rect2(aim - Vector2(36, 36), Vector2(72, 72)), false, main_color)
	else:
		_draw_procedural(main_color)
	if show_hit_ring:
		draw_circle(hit_ring_center, hit_ring_radius, Color(0.2, 1.0, 0.4, 0.5), false, 2.0)


## 程序化准星：黑边白心双层设计，保证在任何背景色上都醒目
func _draw_procedural(main_color: Color) -> void:
	var edge := Color(0.08, 0.08, 0.12, main_color.a)  # 与 3D 描边同色
	# 外圈：先画粗黑边再叠白圈
	draw_circle(aim, 14.0, edge, false, 5.0)
	draw_circle(aim, 14.0, main_color, false, 2.5)
	# 十字臂（黑边 + 白芯）
	for dir in [Vector2(-1, 0), Vector2(1, 0), Vector2(0, -1), Vector2(0, 1)]:
		draw_line(aim + dir * 8, aim + dir * 22, edge, 6.0)
		draw_line(aim + dir * 8, aim + dir * 22, main_color, 3.0)
	# 中心点
	draw_circle(aim, 2.5, Color(1.0, 0.3, 0.2, main_color.a))
