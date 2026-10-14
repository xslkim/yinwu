extends SceneTree

## ArcGun 极简测试运行器（不依赖任何插件）
## 运行：godot --headless --path game --script res://tests/run_tests.gd
## 断言失败时退出码非零，可进 CI / smoke 脚本

const Parser = preload("res://src/core/protocol_parser.gd")
const OneEuro = preload("res://src/core/one_euro_filter.gd")
const Score = preload("res://src/core/score_keeper.gd")
const Ammo = preload("res://src/core/ammo_manager.gd")
const Levels = preload("res://src/core/level_defs.gd")
const LevelMgr = preload("res://src/core/level_manager.gd")
const Scheduler = preload("res://src/core/wave_scheduler.gd")
const Assets = preload("res://src/core/asset_lib.gd")
const Settings = preload("res://src/core/settings_manager.gd")
const AudioMgr = preload("res://src/game/audio_manager.gd")

var _pass := 0
var _fail := 0


func _init() -> void:
	print("== ArcGun 测试 ==")
	_test_protocol()
	_test_one_euro()
	_test_score()
	_test_ammo()
	_test_level_defs()
	_test_escape_and_combo()
	_test_level_flow()
	_test_wave_scheduler()
	_test_asset_lib()
	_test_audio_fallback()
	_test_settings()
	print("== 结果: 通过 %d / 失败 %d ==" % [_pass, _fail])
	quit(1 if _fail > 0 else 0)


func _assert(cond: bool, msg: String) -> void:
	if cond:
		_pass += 1
	else:
		_fail += 1
		printerr("[FAIL] %s" % msg)


# ---------- 协议解析 ----------

func _test_protocol() -> void:
	# 合法坐标包
	var r := Parser.parse("960.0,540.0")
	_assert(r.ok and r.type == "aim", "合法坐标包应解析为 aim")
	_assert(is_equal_approx(r.x, 960.0) and is_equal_approx(r.y, 540.0), "坐标值应正确")
	r = Parser.parse("0,0")
	_assert(r.ok, "原点坐标合法")
	r = Parser.parse("1920,1080")
	_assert(r.ok, "边界坐标合法")
	r = Parser.parse(" 960.5 , 540.5 \n")  # 允许空白（UDP 包常带换行）
	_assert(r.ok and is_equal_approx(r.x, 960.5), "带空白的坐标包应解析")

	# 合法事件包
	for e in ["SHOT", "RELOAD", "COIN", "START", "EXIT"]:
		r = Parser.parse(e)
		_assert(r.ok and r.type == "event", "事件包 %s 应解析" % e)
	r = Parser.parse("shot")  # 大小写不敏感
	_assert(r.ok and r.event == "shot", "事件包应大小写不敏感")
	r = Parser.parse("exit")
	_assert(r.ok and r.event == "exit", "EXIT 事件应解析为 exit")

	# 畸形包
	for bad in ["", "abc", "1,2,3", "960,abc", "SHOTX", "2000,540", "-5,100", "960,2000", "1,,2"]:
		r = Parser.parse(bad)
		_assert(not r.ok, "畸形包 [%s] 应判定 invalid" % bad)


# ---------- One Euro Filter ----------

func _test_one_euro() -> void:
	# 首包直通（不能有初始化跳变延迟）
	var f := OneEuro.new()
	_assert(is_equal_approx(f.filter(500.0, 1.0 / 60.0), 500.0), "首采样应直通")

	# 收敛：恒定输入 60 帧后应贴近目标值
	f = OneEuro.new()
	var out := 0.0
	for i in 60:
		out = f.filter(100.0, 1.0 / 60.0)
	_assert(absf(out - 100.0) < 0.5, "恒定输入应收敛到目标值（实际 %f）" % out)

	# 抗抖：叠加 ±20 交替噪声。beta 调低（0.1）压制"速度自适应放大"，
	# 滤波输出方差应显著小于原始 —— 这正是 1€ 参数可调的意义
	f = OneEuro.new()
	f.beta = 0.1
	var raw := []
	var smooth := []
	for i in 120:
		var x := 500.0 + (20.0 if i % 2 == 0 else -20.0)
		raw.append(x)
		smooth.append(f.filter(x, 1.0 / 60.0))
	# 丢掉前 30 帧的收敛段再比
	var var_raw := _variance(raw.slice(30))
	var var_smooth := _variance(smooth.slice(30))
	_assert(var_smooth < var_raw * 0.25, "滤波后方差应大幅低于原始（%f vs %f）" % [var_smooth, var_raw])

	# 跟手：默认参数下追踪 600px/秒的匀速移动，1 秒后滞后应 < 60px
	f = OneEuro.new()
	for i in 60:
		out = f.filter(float(i) * 10.0, 1.0 / 60.0)
	_assert(absf(out - 590.0) < 60.0, "匀速追踪滞后应小于 60px（实际滞后 %f）" % absf(out - 590.0))

	# 阶跃响应（出厂参数 min_cutoff=1.0 beta=0.7 的定量依据）：
	# 100Hz 采样下 500px 阶跃应在 2 帧（20ms）内进入 ±5px —— 光枪"指哪打哪"的底线
	f = OneEuro.new()
	var dt := 0.01
	for i in 50:
		f.filter(500.0, dt)
	var converge_frame := -1
	for i in range(1, 60):
		out = f.filter(1000.0, dt)
		if absf(out - 1000.0) < 5.0 and converge_frame < 0:
			converge_frame = i
	_assert(converge_frame > 0 and converge_frame <= 2,
		"阶跃收敛应在 2 帧内（实际 %d 帧）" % converge_frame)


func _variance(values: Array) -> float:
	var mean := 0.0
	for v in values:
		mean += v
	mean /= values.size()
	var acc := 0.0
	for v in values:
		acc += (v - mean) ** 2
	return acc / values.size()


# ---------- 计分 ----------

func _test_score() -> void:
	var s := Score.new()
	_assert(s.register_hit() == 100, "第 1 连击命中得 100")
	_assert(s.register_hit() == 200, "第 2 连击命中得 200")
	_assert(s.score == 300 and s.combo == 2, "累计得分/连击正确")
	s.register_miss()
	_assert(s.combo == 0 and s.score == 300, "脱靶清连击不清分")
	_assert(s.register_hit() == 100, "连击清零后重新从 100 起")
	_assert(s.hits == 3 and s.misses == 1, "命中/脱靶计数正确")
	_assert(absf(s.accuracy() - 0.75) < 0.001, "命中率 3/4=0.75")
	s.reset()
	_assert(s.score == 0 and s.combo == 0, "reset 归零")

	# 连击峰值
	s = Score.new()
	s.register_hit(); s.register_hit(); s.register_hit()
	s.register_miss()
	s.register_hit()
	_assert(s.peak_combo == 3, "连击峰值应为 3（实际 %d）" % s.peak_combo)
	s.reset()
	_assert(s.peak_combo == 0, "reset 清连击峰值")

	# 金靶底分：300 × 连击
	s = Score.new()
	_assert(s.register_hit(Score.GOLD_POINTS) == 300, "金靶首中 300")
	_assert(s.register_hit(Score.GOLD_POINTS) == 600, "金靶二连 600")

	# 人质误伤：扣分 + 清连击 + 不下负
	s = Score.new()
	s.register_hit(); s.register_hit()  # 300 分 2 连击
	s.register_hostage_hit()
	_assert(s.score == 100 and s.combo == 0 and s.hostage_hits == 1,
		"误伤人质扣 200 且清连击")
	s.register_hostage_hit()
	_assert(s.score == 0, "扣分下限为 0")
	_assert(s.register_hit() == 100, "误伤后重新起连击")


# ---------- 弹药 ----------

func _test_ammo() -> void:
	var a := Ammo.new(6)
	_assert(a.capacity == 6 and a.current == 6, "初始满弹 6 发")
	_assert(a.can_shoot(), "满弹可射")
	for i in 6:
		_assert(a.consume(), "第 %d 发应可消耗" % (i + 1))
	_assert(a.is_empty() and not a.can_shoot(), "6 发打空后不可射")
	_assert(not a.consume(), "弹空 consume 返回 false")
	a.reload()
	_assert(a.current == 6 and a.can_shoot(), "换弹后满 6 发")


# ---------- 关卡数据表 ----------

func _test_level_defs() -> void:
	var levels := Levels.all()
	_assert(levels.size() == 6, "应有 6 个微关卡")
	var total := 0.0
	var prev_goal := 0
	var prev_mult := 0.0
	for i in levels.size():
		var lv: Dictionary = levels[i]
		_assert(lv.duration >= 20.0 and lv.duration <= 30.0, "每关 20-30 秒: %s" % lv.title)
		_assert(lv.goal > prev_goal, "目标分应递增: %s（%d <= 上一关 %d）" % [lv.title, lv.goal, prev_goal])
		prev_goal = lv.goal
		_assert(lv.get("speed_mult", 1.0) >= prev_mult, "靶速倍率应不减: %s" % lv.title)
		prev_mult = lv.get("speed_mult", 1.0)
		total += lv.duration
		var prev_t := -1.0
		for w in lv.waves:
			_assert(w.t >= prev_t, "波次按时间不减序（金靶允许同刻双出）: %s" % lv.title)
			prev_t = w.t
			_assert(w.type in ["normal", "hostage", "gold"], "合法靶型: %s" % w.type)
			_assert(w.t < lv.duration, "波次要出在关卡时限内: %s t=%.1f" % [lv.title, w.t])
	_assert(total >= 140.0 and total <= 200.0, "一局总时长 3-4 分钟量级（开打 %.0fs + 过场）" % total)
	# 递进结构断言
	_assert(levels[0].waves.all(func(w): return w.speed == 0.0), "L1 全部固定靶（教学关）")
	_assert(not levels[0].escape_penalty and not levels[1].escape_penalty and not levels[2].escape_penalty,
		"L1-L3 学习期无逃靶惩罚")
	_assert(levels[3].escape_penalty, "L4 引入逃靶惩罚")
	_assert(levels[2].waves.any(func(w): return w.type == "hostage"), "L3 要有人质")
	_assert(levels[3].waves.any(func(w): return w.type == "gold"), "L4 要有金靶")
	_assert(is_equal_approx(levels[4].speed_mult, 1.3), "L5 靶速 ×1.3")
	_assert(is_equal_approx(levels[5].speed_mult, 1.5), "L6 靶速 ×1.5")
	# L6 金靶双出：存在同一时刻两个金靶
	var l6: Dictionary = levels[5]
	var gold_times := {}
	for w in l6.waves:
		if w.type == "gold":
			gold_times[w.t] = gold_times.get(w.t, 0) + 1
	_assert(gold_times.values().any(func(n): return n >= 2), "L6 金靶要成对双出")
	# demo 波次表合法性
	var d := Levels.demo()
	_assert(d.waves.size() >= 8 and not d.escape_penalty, "demo 波次循环播放且无惩罚")


# ---------- 逃靶 / 连击里程碑 ----------

func _test_escape_and_combo() -> void:
	var s := Score.new()
	s.register_hit(); s.register_hit()  # 300 分 2 连击
	s.register_escape()
	_assert(s.score == 250 and s.combo == 0 and s.escapes == 1, "逃靶 -50 断连击")
	s.register_escape()
	s.register_escape()
	s.register_escape()
	s.register_escape()
	s.register_escape()  # 再逃 5 次共 -250，触底 0
	_assert(s.score == 0 and s.escapes == 6, "逃靶扣分下限 0")
	s.reset()
	_assert(s.escapes == 0, "reset 清逃靶计数")

	# 连击里程碑：5/10/15 触发，其余不触发
	for c in [5, 10, 15, 20]:
		_assert(Score.is_combo_milestone(c), "×%d 应是里程碑" % c)
	for c in [0, 1, 4, 6, 9, 11, 14]:
		_assert(not Score.is_combo_milestone(c), "×%d 不应是里程碑" % c)


# ---------- 关卡流转 ----------

## 用极短关卡定义驱动状态机，避免测试真等 25 秒
func _mini_levels() -> Array:
	return [
		{"title": "L1", "duration": 10.0, "goal": 100, "waves": []},
		{"title": "L2", "duration": 10.0, "goal": 100, "waves": []},
		{"title": "L3", "duration": 10.0, "goal": 100, "waves": []},
	]


func _test_level_flow() -> void:
	var s := Score.new()
	var m := LevelMgr.new(_mini_levels(), s)
	_assert(m.state == "attract", "初始待机")
	_assert(not m.press_start(), "没投币不能开始")
	_assert(m.state == "attract", "开始失败保持待机")
	m.insert_coin()
	_assert(m.can_start(), "投币后可开始")
	_assert(m.press_start(), "投币+开始成功")
	_assert(m.state == "title" and m.level_idx == 0, "进第 1 关标题")

	# 标题闪现结束 → 开打
	m.tick(LevelMgr.TITLE_TIME + 0.01)
	_assert(m.state == "playing", "标题后进入 playing")
	_assert(absf(m.time_ratio() - 1.0) < 0.01, "开打时计时满格")

	# 打够目标分，时间耗尽 → 过关
	s.register_hit()  # 100 分 = goal
	m.tick(10.1)
	_assert(m.state == "result" and m.last_passed, "达标判过关")
	_assert(m.level_score == 100, "本关得分独立计算")

	# 结算闪现完 → 下一关标题（状态机同步推进，无场景加载，重开延迟 <2s 由此保证）
	m.tick(LevelMgr.RESULT_TIME + 0.01)
	_assert(m.state == "title" and m.level_idx == 1, "过关后进第 2 关")

	# 第 2 关故意不达标 → 失败直接总结算
	m.tick(LevelMgr.TITLE_TIME + 0.01)
	m.tick(10.1)
	_assert(m.state == "result" and not m.last_passed, "不达标判失败")
	var over := [false]
	m.game_finished.connect(func(win): over[0] = true; _assert(not win, "中途失败 win=false"))
	m.tick(LevelMgr.RESULT_TIME + 0.01)
	_assert(m.state == "gameover", "失败后进总结算")
	_assert(over[0], "game_finished 信号应发出")

	# 再来一局：免投币，同步重置，立刻回标题（断言无延迟 —— 这是 <2s 重开的逻辑保证）
	_assert(m.press_start(), "总结算页可免投币再来一局")
	_assert(m.state == "title" and m.level_idx == 0 and s.score == 0, "重开归零回第 1 关")

	# 全通关路径：3 关都达标 → win=true
	s = Score.new()
	m = LevelMgr.new(_mini_levels(), s)
	var win_result := [false]
	m.game_finished.connect(func(win): win_result[0] = win)
	m.insert_coin()
	m.press_start()
	for i in 3:
		m.tick(LevelMgr.TITLE_TIME + 0.01)
		s.register_hit()  # 每关 100 分达标
		m.tick(10.1)
		_assert(m.last_passed, "第 %d 关应过关" % (i + 1))
		if m.state == "result":
			m.tick(LevelMgr.RESULT_TIME + 0.01)
	_assert(m.state == "gameover", "3 关过完进总结算")
	_assert(win_result[0], "全通关 win=true")

	# attract 演示模式：30s 无投币自动进 demo，投币打断回待机页
	s = Score.new()
	m = LevelMgr.new(_mini_levels(), s)
	m.tick(LevelMgr.DEMO_AFTER * 0.5)
	_assert(m.state == "attract", "未到 30s 保持待机")
	m.tick(LevelMgr.DEMO_AFTER * 0.6)
	_assert(m.state == "demo", "30s 无投币进演示")
	_assert(not m.press_start(), "demo 中无币不能开打")
	m.insert_coin()
	_assert(m.state == "attract", "投币打断演示回待机页")
	_assert(m.press_start(), "打断后可正常开打")

	# EXIT：进行中退出 → 回待机页；待机页退出 → false（由场景层退出进程）
	s = Score.new()
	m = LevelMgr.new(_mini_levels(), s)
	_assert(not m.abort(), "待机页 abort 应返回 false")
	m.insert_coin()
	m.press_start()
	m.tick(LevelMgr.TITLE_TIME + 0.01)
	_assert(m.state == "playing", "进入开打状态")
	_assert(m.abort(), "开打中 abort 应成功")
	_assert(m.state == "attract", "abort 后回待机页")
	_assert(m.credits == 0, "abort 不退币")
	_assert(not m.can_start(), "回待机后无币不能再开")


# ---------- 波次调度 ----------

func _test_wave_scheduler() -> void:
	var sc := Scheduler.new()
	sc.reset({"waves": [
		{"t": 1.0, "type": "normal"}, {"t": 2.0, "type": "gold"}, {"t": 2.0, "type": "hostage"},
	]})
	_assert(sc.poll(0.5).is_empty(), "没到点不出靶")
	_assert(sc.poll(1.0).size() == 1, "到点出 1 个")
	var due := sc.poll(2.0)
	_assert(due.size() == 2, "同一时刻出 2 个（实际 %d）" % due.size())
	_assert(due[0].type == "gold" and due[1].type == "hostage", "同时刻保持表内顺序")
	_assert(sc.exhausted(), "全部派完")
	_assert(sc.poll(99.0).is_empty(), "派完后不再出")


# ---------- AssetLib fallback ----------

func _test_asset_lib() -> void:
	# 用 user:// 下的临时目录模拟素材目录（不碰 res://assets/）
	var dir := "user://test_assets"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(dir))

	# 空目录 → 无变体，走 fallback
	_assert(Assets.variants(dir, "shot", ["wav"]).is_empty(), "空目录无变体")
	_assert(Assets.pick_variant(dir, "shot", ["wav"]) == "", "空目录 pick 返回空串")
	_assert(Assets.texture_or_null(dir + "/nope.png") == null, "缺失贴图返回 null")

	# 有本体 + 编号变体 → 全部找到
	for f in ["shot.wav", "shot_2.wav", "shot_3.wav"]:
		var h := FileAccess.open("%s/%s" % [dir, f], FileAccess.WRITE)
		h.store_string("fake")
		h.close()
	_assert(Assets.variants(dir, "shot", ["wav"]).size() == 3, "本体+2变体=3（实际 %d）" % Assets.variants(dir, "shot", ["wav"]).size())
	_assert(Assets.pick_variant(dir, "shot", ["wav"]) != "", "有变体时 pick 非空")
	# 编号断档不影响已存在的变体；不存在的名字仍空
	_assert(Assets.variants(dir, "reload", ["wav"]).is_empty(), "别的名字不受影响")

	# 贴图：生成一张真 PNG 能加载，垃圾文件返回 null
	var img := Image.create(4, 4, false, Image.FORMAT_RGBA8)
	img.fill(Color.RED)
	img.save_png(dir + "/red.png")
	_assert(Assets.texture_or_null(dir + "/red.png") != null, "真 PNG 应能加载")
	var h := FileAccess.open(dir + "/bad.png", FileAccess.WRITE)
	h.store_string("not a png")
	h.close()
	_assert(Assets.texture_or_null(dir + "/bad.png") == null, "垃圾文件返回 null")

	for f in DirAccess.get_files_at(dir):
		DirAccess.remove_absolute(dir + "/" + f)
	DirAccess.remove_absolute(dir)


# ---------- AudioManager fallback ----------

func _test_audio_fallback() -> void:
	var am := AudioMgr.new()  # 不进树：_ready 不跑，纯测路径解析
	am.audio_dir = "user://definitely_not_exists"
	_assert(am.resolve_clip("shot") == "", "素材缺失时音效解析为空（走合成 fallback）")
	_assert(am.resolve_bgm("menu") == "", "素材缺失时 BGM 解析为空（静音）")
	_assert(AudioMgr.SYNTH.has("shot") and AudioMgr.SYNTH.has("escape")
		and AudioMgr.SYNTH.has("combo"), "fallback 合成表覆盖关键音效")
	# headless 下 play/play_bgm/blip 必须安全无操作不崩溃
	am.play("shot")
	am.play_bgm("play")
	am.blip()
	am.free()
	# 有素材时解析出变体
	var dir := "user://test_audio"
	DirAccess.make_dir_recursive_absolute(ProjectSettings.globalize_path(dir))
	var h := FileAccess.open(dir + "/shot.wav", FileAccess.WRITE)
	h.store_string("fake wav")
	h.close()
	var am2 := AudioMgr.new()
	am2.audio_dir = dir
	_assert(am2.resolve_clip("shot").ends_with("shot.wav"), "素材存在时解析到 WAV")
	am2.free()
	DirAccess.remove_absolute(dir + "/shot.wav")
	DirAccess.remove_absolute(dir)


# ---------- 设置持久化 ----------

func _test_settings() -> void:
	var path := "user://test_settings.cfg"
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
	var s := Settings.new()
	s.path = path
	s.load_settings()  # 无文件 → 默认值
	_assert(is_equal_approx(s.volumes.master, 1.0), "默认主音量 100%")
	_assert(is_equal_approx(s.volumes.music, 0.8), "默认音乐 80%")
	# F3 循环：100→70→40→0→100
	_assert(is_equal_approx(s.cycle_master(), 0.7), "第 1 档 70%")
	_assert(is_equal_approx(s.cycle_master(), 0.4), "第 2 档 40%")
	_assert(is_equal_approx(s.cycle_master(), 0.0), "第 3 档静音")
	_assert(is_equal_approx(s.cycle_master(), 1.0), "循环回 100%")
	# 持久化往返
	s.volumes.music = 0.33
	s.save_settings()
	var s2 := Settings.new()
	s2.path = path
	s2.load_settings()
	_assert(is_equal_approx(s2.volumes.master, 1.0) and absf(s2.volumes.music - 0.33) < 0.001,
		"设置存盘读回一致")
	DirAccess.remove_absolute(ProjectSettings.globalize_path(path))
