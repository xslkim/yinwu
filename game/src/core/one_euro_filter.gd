class_name OneEuroFilter
extends RefCounted

## One Euro Filter（一欧元滤波器）：低延迟平滑滤波
## 专为"静止时抖得要死、快速移动时又不能拖"的人手输入设计，正适合手机光枪坐标流
## 参考：Casiez et al. "1€ Filter" —— 几十行即可实现
##
## 用法：
##   var f := OneEuroFilter.new()
##   var smooth := f.filter(raw_x, dt)  # dt 为距上次采样的秒数

@export var min_cutoff := 1.0  ## 静止截止频率 Hz：越小越稳（抖动压制强）
@export var beta := 0.7        ## 速度系数：越大快速移动越跟手（延迟低）
@export var d_cutoff := 1.0    ## 导数截止频率 Hz

var _x_filter := LowPass.new()
var _dx_filter := LowPass.new()


class LowPass:
	var y := 0.0
	var has_value := false

	func filter(x: float, alpha: float) -> float:
		if not has_value:
			y = x
			has_value = true
		else:
			y = alpha * x + (1.0 - alpha) * y
		return y


static func _alpha(cutoff: float, dt: float) -> float:
	var tau := 1.0 / (2.0 * PI * cutoff)
	return 1.0 / (1.0 + tau / dt)


## 输入一个新采样值与距上次采样的间隔秒数，返回滤波后的值
func filter(x: float, dt: float) -> float:
	if dt <= 0.0:
		dt = 1.0 / 60.0  # 防御：时间戳重复时按 60Hz 算
	# 估计速度（一阶导数），并对速度本身也做低通
	var dx := 0.0
	if _x_filter.has_value:
		dx = (x - _x_filter.y) / dt
	var edx := _dx_filter.filter(dx, _alpha(d_cutoff, dt))
	# 速度越快截止频率越高 → 慢时稳、快时跟手
	var cutoff := min_cutoff + beta * absf(edx)
	return _x_filter.filter(x, _alpha(cutoff, dt))


func reset() -> void:
	_x_filter = LowPass.new()
	_dx_filter = LowPass.new()
