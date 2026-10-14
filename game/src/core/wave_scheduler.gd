class_name WaveScheduler
extends RefCounted

## 波次调度器（纯逻辑）：给定关卡定义与经过时间，吐出到期的出场事件
## 场景层每帧 poll(elapsed)，把返回的波次条目实例化成标靶

var _waves: Array = []
var _cursor := 0


func reset(level_def: Dictionary) -> void:
	_waves = level_def.waves
	_cursor = 0


## 返回 elapsed 时刻前应出场但还没出场的所有波次（按表内顺序）
func poll(elapsed: float) -> Array:
	var due := []
	while _cursor < _waves.size() and _waves[_cursor].t <= elapsed:
		due.append(_waves[_cursor])
		_cursor += 1
	return due


## 所有波次是否都已派出
func exhausted() -> bool:
	return _cursor >= _waves.size()
