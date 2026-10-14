class_name LevelDefs
extends RefCounted

## 微关卡波次数据表（纯数据，方便后续 AI 量产新关卡）
##
## 关卡字段：
##   title    关卡标题闪现文字
##   duration 关卡时长（秒）
##   goal     过关所需的本关得分
##   waves    波次数组，按时间排序：
##     t     出场时刻（秒，从关卡开始计）
##     type  靶型：normal（100 底分）/ hostage（误伤扣分）/ gold（小快，300 底分）
##     lane  水平出生位（米，靶道坐标系，约 -6..+6）
##     speed 水平移动速度（0 = 固定靶）
##     dwell 停留时长（秒，超时自动缩回）

static func all() -> Array:
	return [_level1(), _level2(), _level3()]


## 微关卡 1：固定靶练习 —— 只出静止普通靶，熟悉手感
static func _level1() -> Dictionary:
	var waves := []
	var lanes := [-3.0, 0.0, 3.0]
	for i in 8:
		waves.append({"t": 1.0 + i * 2.8, "type": "normal",
			"lane": lanes[i % 3], "speed": 0.0, "dwell": 4.0})
	return {"title": "LEVEL 1  固定靶练习", "duration": 25.0, "goal": 1500, "waves": waves}


## 微关卡 2：移动靶 + 人质混杂 —— 打错要扣分
static func _level2() -> Dictionary:
	return {"title": "LEVEL 2  小心人质", "duration": 25.0, "goal": 2500, "waves": [
		{"t": 1.0, "type": "normal", "lane": -3.0, "speed": 2.0, "dwell": 5.0},
		{"t": 2.5, "type": "hostage", "lane": 1.5, "speed": 1.5, "dwell": 4.0},
		{"t": 4.0, "type": "normal", "lane": 3.0, "speed": -2.5, "dwell": 5.0},
		{"t": 6.5, "type": "normal", "lane": 0.0, "speed": 3.0, "dwell": 4.0},
		{"t": 8.0, "type": "hostage", "lane": -2.0, "speed": 2.0, "dwell": 4.0},
		{"t": 9.5, "type": "normal", "lane": 2.5, "speed": -3.0, "dwell": 4.0},
		{"t": 12.0, "type": "normal", "lane": -3.5, "speed": 2.5, "dwell": 4.0},
		{"t": 13.5, "type": "hostage", "lane": 0.5, "speed": -2.0, "dwell": 3.5},
		{"t": 15.0, "type": "normal", "lane": 3.5, "speed": -2.5, "dwell": 4.0},
		{"t": 17.5, "type": "normal", "lane": -1.0, "speed": 3.0, "dwell": 3.5},
		{"t": 19.0, "type": "hostage", "lane": 2.0, "speed": 2.5, "dwell": 3.0},
		{"t": 20.5, "type": "normal", "lane": -2.5, "speed": -3.0, "dwell": 3.5},
	]}


## 微关卡 3：金靶限时挑战 —— 小、快、高分，穿插普通靶保底
static func _level3() -> Dictionary:
	return {"title": "LEVEL 3  金靶限时", "duration": 20.0, "goal": 3000, "waves": [
		{"t": 1.0, "type": "normal", "lane": -2.0, "speed": 2.0, "dwell": 4.0},
		{"t": 2.0, "type": "gold", "lane": 2.0, "speed": -5.0, "dwell": 2.5},
		{"t": 4.5, "type": "gold", "lane": -3.0, "speed": 5.5, "dwell": 2.5},
		{"t": 6.5, "type": "normal", "lane": 0.5, "speed": 3.0, "dwell": 3.5},
		{"t": 7.5, "type": "gold", "lane": 3.5, "speed": -6.0, "dwell": 2.2},
		{"t": 10.0, "type": "gold", "lane": 0.0, "speed": 6.0, "dwell": 2.2},
		{"t": 12.0, "type": "hostage", "lane": -1.5, "speed": 2.0, "dwell": 3.0},
		{"t": 13.0, "type": "gold", "lane": 2.5, "speed": -6.5, "dwell": 2.0},
		{"t": 15.5, "type": "gold", "lane": -3.5, "speed": 6.5, "dwell": 2.0},
		{"t": 17.0, "type": "normal", "lane": 1.0, "speed": -3.5, "dwell": 2.5},
	]}
