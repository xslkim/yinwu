class_name LevelUi
extends CanvasLayer

## 关卡流程 UI：待机页 / 关卡标题闪现 / 计时条 / 单关结算闪现 / 总结算页
## 全部代码搭建，零素材

var _center_label: Label      ## 大标题（待机/标题闪现/过关失败共用）
var _sub_label: Label         ## 副标题（提示语/单关得分）
var _timer_bar: ProgressBar
var _goal_label: Label        ## 计时条旁的目标分提示
var _stats_holder: CenterContainer  ## 总结算页居中容器
var _stats_panel: PanelContainer
var _stats_label: Label


func _ready() -> void:
	layer = 20
	# 居中文字用绝对布局（视口固定 1920×1080，canvas_items 拉伸下最可靠）
	_center_label = _make_label(72, HORIZONTAL_ALIGNMENT_CENTER)
	_center_label.position = Vector2(0, 380)
	_center_label.size = Vector2(1920, 220)

	_sub_label = _make_label(36, HORIZONTAL_ALIGNMENT_CENTER)
	_sub_label.position = Vector2(0, 590)
	_sub_label.size = Vector2(1920, 160)

	# 顶部计时条（开打才显示）
	_timer_bar = ProgressBar.new()
	_timer_bar.min_value = 0.0
	_timer_bar.max_value = 1.0
	_timer_bar.size = Vector2(600, 22)
	_timer_bar.position = Vector2(660, 24)
	_timer_bar.show_percentage = false
	var bar_bg := StyleBoxFlat.new()
	bar_bg.bg_color = Color(0.05, 0.05, 0.1, 0.6)
	bar_bg.set_corner_radius_all(6)
	_timer_bar.add_theme_stylebox_override("background", bar_bg)
	var bar_fill := StyleBoxFlat.new()
	bar_fill.bg_color = Color(0.4, 0.9, 0.4)
	bar_fill.set_corner_radius_all(6)
	_timer_bar.add_theme_stylebox_override("fill", bar_fill)
	add_child(_timer_bar)
	_goal_label = _make_label(26, HORIZONTAL_ALIGNMENT_CENTER)
	_goal_label.position = Vector2(660, 50)
	_goal_label.size = Vector2(600, 40)
	_hide_playing_ui()

	# 总结算面板（半透明黑底卡片，CenterContainer 保证真居中）
	_stats_holder = CenterContainer.new()
	_stats_holder.position = Vector2(0, 0)
	_stats_holder.size = Vector2(1920, 1080)
	add_child(_stats_holder)
	_stats_panel = PanelContainer.new()
	var style := StyleBoxFlat.new()
	style.bg_color = Color(0.05, 0.05, 0.1, 0.85)
	style.set_corner_radius_all(16)
	style.content_margin_left = 60
	style.content_margin_right = 60
	style.content_margin_top = 40
	style.content_margin_bottom = 40
	_stats_panel.add_theme_stylebox_override("panel", style)
	_stats_label = Label.new()
	_stats_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	_stats_label.add_theme_font_size_override("font_size", 44)
	_stats_panel.add_child(_stats_label)
	_stats_holder.add_child(_stats_panel)
	_stats_holder.visible = false


# ---------- 各状态展示 ----------

## 待机页：等投币
func show_attract(credits: int) -> void:
	_hide_playing_ui()
	_center_label.position.y = 380.0
	_stats_holder.visible = false
	_center_label.modulate = Color.WHITE
	_center_label.text = "ARC GUN"
	_sub_label.text = "投币开始（手机发 COIN / 键盘 C），然后 START / 回车开打\n当前币数: %d" % credits


func show_title(def: Dictionary) -> void:
	_stats_holder.visible = false
	_center_label.position.y = 380.0
	_center_label.modulate = Color.WHITE
	_center_label.text = def.title
	_sub_label.text = "目标 %d 分 / %.0f 秒" % [def.goal, def.duration]


func show_playing(def: Dictionary) -> void:
	_center_label.text = ""
	_sub_label.text = ""
	_timer_bar.visible = true
	_goal_label.visible = true
	_goal_label.text = "目标 %d" % def.goal


func update_timer(ratio: float) -> void:
	_timer_bar.value = ratio


func show_level_result(passed: bool, level_score: int, goal: int) -> void:
	_hide_playing_ui()
	_center_label.position.y = 380.0
	_stats_holder.visible = false
	_center_label.text = "过关！" if passed else "失败…"
	_center_label.modulate = Color(0.4, 1.0, 0.5) if passed else Color(1.0, 0.35, 0.3)
	_sub_label.text = "本关 %d 分 / 目标 %d" % [level_score, goal]


## 总结算页：得分 / 命中率 / 连击峰值 / 人质误伤
func show_gameover(win: bool, score: ScoreKeeper) -> void:
	_hide_playing_ui()
	_center_label.position.y = 110.0  # 挪到结算面板上方
	_center_label.text = "全部通关！" if win else "GAME OVER"
	_center_label.modulate = Color(1.0, 0.85, 0.2) if win else Color(1.0, 0.35, 0.3)
	_sub_label.text = ""
	_stats_label.text = "得分  %06d\n命中率  %d%%（%d 中 / %d 发）\n连击峰值  ×%d\n人质误伤  %d 次    逃靶  %d 次\n\n—— START / 回车 再来一局 ——" % [
		score.score, int(score.accuracy() * 100), score.hits, score.hits + score.misses,
		score.peak_combo, score.hostage_hits, score.escapes]
	_stats_holder.visible = true


func _hide_playing_ui() -> void:
	_timer_bar.visible = false
	_goal_label.visible = false


func _make_label(font_size: int, align: HorizontalAlignment) -> Label:
	var label := Label.new()
	label.horizontal_alignment = align
	label.add_theme_font_size_override("font_size", font_size)
	label.add_theme_color_override("font_shadow_color", Color(0.08, 0.08, 0.12))
	label.add_theme_constant_override("shadow_offset_x", 3)
	label.add_theme_constant_override("shadow_offset_y", 3)
	add_child(label)
	return label
