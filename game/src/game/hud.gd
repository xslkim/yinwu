class_name Hud
extends CanvasLayer

## HUD：得分 / 弹药 / 连击，全部代码搭建，无外部素材

var _score_label: Label
var _ammo_label: Label
var _combo_label: Label
var _ammo_box: HBoxContainer  ## ui_ammo.png 存在时用图标排，否则回退圆点文字


func _ready() -> void:
	_score_label = _make_label(Vector2(40, 30), 48, HORIZONTAL_ALIGNMENT_LEFT)
	_combo_label = _make_label(Vector2(-260, 30), 40, HORIZONTAL_ALIGNMENT_RIGHT)
	_combo_label.anchor_left = 1.0
	_combo_label.anchor_right = 1.0
	_combo_label.size.x = 240
	# 弹药：贴图优先，fallback 圆点
	var ammo_tex := AssetLib.texture_or_null("res://assets/textures/ui_ammo.png")
	if ammo_tex:
		_ammo_box = HBoxContainer.new()
		_ammo_box.position = Vector2(30, -120)
		_ammo_box.anchor_top = 1.0
		_ammo_box.anchor_bottom = 1.0
		for i in 6:
			var icon := TextureRect.new()
			icon.texture = ammo_tex
			icon.custom_minimum_size = Vector2(48, 48)
			icon.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
			icon.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
			_ammo_box.add_child(icon)
		add_child(_ammo_box)
	else:
		_ammo_label = _make_label(Vector2(40, -120), 64, HORIZONTAL_ALIGNMENT_LEFT)
		_ammo_label.anchor_top = 1.0
		_ammo_label.anchor_bottom = 1.0


func set_score(score: int) -> void:
	_score_label.text = "SCORE %06d" % score


func set_ammo(current: int, capacity: int) -> void:
	if _ammo_box:
		# 图标排：打掉的子弹半透明
		for i in _ammo_box.get_child_count():
			_ammo_box.get_child(i).modulate.a = 1.0 if i < current else 0.2
	else:
		# 弹药用圆点图示，比数字更"街机"
		var pips := ""
		for i in capacity:
			pips += "●" if i < current else "○"
		_ammo_label.text = pips
		_ammo_label.modulate = Color(1, 0.3, 0.2) if current == 0 else Color.WHITE


func set_combo(combo: int) -> void:
	_combo_label.text = "COMBO ×%d" % combo if combo >= 2 else ""


func _make_label(offset: Vector2, font_size: int, align: HorizontalAlignment) -> Label:
	var label := Label.new()
	label.position = offset
	label.size = Vector2(400, font_size + 20)
	label.horizontal_alignment = align
	label.add_theme_font_size_override("font_size", font_size)
	# 深色投影，保证亮底色上也可读
	label.add_theme_color_override("font_shadow_color", Color(0.08, 0.08, 0.12))
	label.add_theme_constant_override("shadow_offset_x", 3)
	label.add_theme_constant_override("shadow_offset_y", 3)
	add_child(label)
	return label
