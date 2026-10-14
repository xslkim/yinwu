extends SceneTree

## ArcGun 极简测试运行器（不依赖任何插件）
## 运行：godot --headless --path game --script res://tests/run_tests.gd
## 断言失败时退出码非零，可进 CI / smoke 脚本

const Parser = preload("res://src/core/protocol_parser.gd")
const OneEuro = preload("res://src/core/one_euro_filter.gd")
const Score = preload("res://src/core/score_keeper.gd")
const Ammo = preload("res://src/core/ammo_manager.gd")

var _pass := 0
var _fail := 0


func _init() -> void:
	print("== ArcGun 测试 ==")
	_test_protocol()
	_test_one_euro()
	_test_score()
	_test_ammo()
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
	for e in ["SHOT", "RELOAD", "COIN", "START"]:
		r = Parser.parse(e)
		_assert(r.ok and r.type == "event", "事件包 %s 应解析" % e)
	r = Parser.parse("shot")  # 大小写不敏感
	_assert(r.ok and r.event == "shot", "事件包应大小写不敏感")

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
