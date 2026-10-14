class_name LevelDefs
extends RefCounted

## 微关卡波次数据表（纯数据，方便后续 AI 量产新关卡）
##
## 关卡字段：
##   title           关卡标题闪现文字
##   duration        关卡时长（秒，20-30）
##   goal            过关所需的本关得分
##   speed_mult      靶速倍率（波次里的 speed 会乘上它）
##   escape_penalty  逃靶是否扣分断连击（L1-L3 学习期豁免，L4 起引入）
##   waves           波次数组，按时间升序：
##     t     出场时刻（秒，从关卡开始计）
##     type  靶型：normal（100 底分）/ hostage（误伤 -200）/ gold（300 底分，小快）
##     lane  水平出生位（米，靶道坐标系，约 -4..+4）
##     speed 水平移动速度（0 = 固定靶；负值向左）
##     dwell 停留时长（秒，超时逃靶）
##
## 难度递进设计：一局 ≈ 2.8 分钟（147s 开打 + 21s 过场）
##   L1 教学：全固定普通靶，无任何惩罚
##   L2 移动靶入门：全移动普通靶
##   L3 人质混杂：误伤 -200
##   L4 金靶限时：引入逃靶惩罚（-50 断连击），金靶高分但停留短
##   L5 加速混合：靶速 ×1.3，人质占比更高
##   L6 最终混战：靶速 ×1.5，金靶成对双出，全靶型

static func all() -> Array:
	return [_level1(), _level2(), _level3(), _level4(), _level5(), _level6()]


## attract 演示模式的波次（自动弹出缩回，无惩罚，循环播放）
static func demo() -> Dictionary:
	var waves := []
	var lanes := [-3.0, 0.0, 3.0, -1.5, 1.5]
	var types := ["normal", "normal", "hostage", "gold", "normal"]
	for i in 10:
		waves.append({"t": 0.5 + i * 1.6, "type": types[i % 5],
			"lane": lanes[i % 5], "speed": 1.5 if i % 2 == 0 else 0.0, "dwell": 2.2})
	return {"title": "DEMO", "duration": 16.5, "goal": 0,
		"speed_mult": 1.0, "escape_penalty": false, "waves": waves}


static func _level1() -> Dictionary:
	var waves := []
	var lanes := [-3.0, 0.0, 3.0]
	for i in 8:
		waves.append({"t": 1.0 + i * 2.5, "type": "normal",
			"lane": lanes[i % 3], "speed": 0.0, "dwell": 4.0})
	return {"title": "LEVEL 1  固定靶练习", "duration": 22.0, "goal": 1200,
		"speed_mult": 1.0, "escape_penalty": false, "waves": waves}


static func _level2() -> Dictionary:
	var waves := []
	var lanes := [-3.0, 2.0, 0.0, -2.0, 3.0]
	var speeds := [2.0, -2.5, 3.0, 2.5, -3.0]
	for i in 10:
		waves.append({"t": 1.0 + i * 2.2, "type": "normal",
			"lane": lanes[i % 5], "speed": speeds[i % 5], "dwell": 4.0})
	return {"title": "LEVEL 2  移动靶", "duration": 25.0, "goal": 2200,
		"speed_mult": 1.0, "escape_penalty": false, "waves": waves}


static func _level3() -> Dictionary:
	return {"title": "LEVEL 3  小心人质", "duration": 25.0, "goal": 3000,
		"speed_mult": 1.0, "escape_penalty": false, "waves": [
		{"t": 1.0, "type": "normal", "lane": -3.0, "speed": 2.0, "dwell": 4.0},
		{"t": 2.5, "type": "hostage", "lane": 1.5, "speed": 1.5, "dwell": 4.0},
		{"t": 4.0, "type": "normal", "lane": 3.0, "speed": -2.5, "dwell": 4.0},
		{"t": 6.0, "type": "normal", "lane": 0.0, "speed": 3.0, "dwell": 4.0},
		{"t": 7.5, "type": "hostage", "lane": -2.0, "speed": 2.0, "dwell": 4.0},
		{"t": 9.0, "type": "normal", "lane": 2.5, "speed": -3.0, "dwell": 4.0},
		{"t": 11.5, "type": "normal", "lane": -3.5, "speed": 2.5, "dwell": 4.0},
		{"t": 13.0, "type": "hostage", "lane": 0.5, "speed": -2.0, "dwell": 3.5},
		{"t": 14.5, "type": "normal", "lane": 3.5, "speed": -2.5, "dwell": 4.0},
		{"t": 17.0, "type": "hostage", "lane": -1.0, "speed": 2.5, "dwell": 3.5},
		{"t": 18.5, "type": "normal", "lane": 1.5, "speed": -3.0, "dwell": 3.5},
		{"t": 20.5, "type": "normal", "lane": -2.5, "speed": 3.0, "dwell": 3.5},
	]}


static func _level4() -> Dictionary:
	return {"title": "LEVEL 4  金靶限时", "duration": 22.0, "goal": 3400,
		"speed_mult": 1.0, "escape_penalty": true, "waves": [
		{"t": 1.0, "type": "normal", "lane": -2.0, "speed": 2.0, "dwell": 4.0},
		{"t": 2.0, "type": "gold", "lane": 2.0, "speed": -5.0, "dwell": 2.5},
		{"t": 4.5, "type": "gold", "lane": -3.0, "speed": 5.5, "dwell": 2.5},
		{"t": 6.5, "type": "normal", "lane": 0.5, "speed": 3.0, "dwell": 3.5},
		{"t": 7.5, "type": "gold", "lane": 3.5, "speed": -6.0, "dwell": 2.2},
		{"t": 10.0, "type": "normal", "lane": -1.5, "speed": -2.5, "dwell": 3.5},
		{"t": 11.0, "type": "gold", "lane": 0.0, "speed": 6.0, "dwell": 2.2},
		{"t": 13.5, "type": "gold", "lane": 2.5, "speed": -6.5, "dwell": 2.0},
		{"t": 16.0, "type": "normal", "lane": -3.0, "speed": 2.5, "dwell": 3.5},
		{"t": 17.0, "type": "gold", "lane": -0.5, "speed": 6.5, "dwell": 2.0},
		{"t": 19.5, "type": "gold", "lane": 1.5, "speed": -6.0, "dwell": 2.0},
	]}


static func _level5() -> Dictionary:
	return {"title": "LEVEL 5  加速混合", "duration": 25.0, "goal": 4200,
		"speed_mult": 1.3, "escape_penalty": true, "waves": [
		{"t": 1.0, "type": "normal", "lane": -3.0, "speed": 2.5, "dwell": 3.5},
		{"t": 2.0, "type": "hostage", "lane": 0.5, "speed": -2.0, "dwell": 3.5},
		{"t": 3.0, "type": "normal", "lane": 2.5, "speed": -3.0, "dwell": 3.5},
		{"t": 5.0, "type": "gold", "lane": -1.5, "speed": 5.0, "dwell": 2.2},
		{"t": 6.5, "type": "hostage", "lane": 3.0, "speed": 2.0, "dwell": 3.0},
		{"t": 7.5, "type": "normal", "lane": 0.0, "speed": -3.5, "dwell": 3.5},
		{"t": 9.5, "type": "normal", "lane": -2.5, "speed": 3.0, "dwell": 3.0},
		{"t": 10.5, "type": "hostage", "lane": 1.5, "speed": -2.5, "dwell": 3.0},
		{"t": 12.0, "type": "normal", "lane": 3.5, "speed": -3.0, "dwell": 3.0},
		{"t": 14.0, "type": "gold", "lane": -0.5, "speed": -5.5, "dwell": 2.0},
		{"t": 15.5, "type": "hostage", "lane": -3.5, "speed": 2.5, "dwell": 3.0},
		{"t": 16.5, "type": "normal", "lane": 1.0, "speed": 3.5, "dwell": 3.0},
		{"t": 18.5, "type": "normal", "lane": -1.0, "speed": -3.5, "dwell": 3.0},
		{"t": 20.0, "type": "hostage", "lane": 2.0, "speed": 2.0, "dwell": 2.8},
		{"t": 21.5, "type": "normal", "lane": -3.0, "speed": 3.0, "dwell": 3.0},
	]}


static func _level6() -> Dictionary:
	return {"title": "LEVEL 6  最终混战", "duration": 28.0, "goal": 5200,
		"speed_mult": 1.5, "escape_penalty": true, "waves": [
		{"t": 1.0, "type": "normal", "lane": -3.0, "speed": 2.5, "dwell": 3.5},
		{"t": 1.5, "type": "gold", "lane": 2.5, "speed": -5.0, "dwell": 2.2},
		{"t": 1.5, "type": "gold", "lane": 0.5, "speed": 5.0, "dwell": 2.2},   # 金靶双出
		{"t": 4.0, "type": "hostage", "lane": -1.0, "speed": 2.0, "dwell": 3.0},
		{"t": 5.0, "type": "normal", "lane": 3.5, "speed": -3.0, "dwell": 3.2},
		{"t": 7.0, "type": "gold", "lane": -2.5, "speed": 5.5, "dwell": 2.0},
		{"t": 7.0, "type": "gold", "lane": 1.5, "speed": -5.5, "dwell": 2.0},   # 双出
		{"t": 9.5, "type": "normal", "lane": 0.0, "speed": 3.5, "dwell": 3.0},
		{"t": 10.5, "type": "hostage", "lane": -3.5, "speed": -2.0, "dwell": 3.0},
		{"t": 12.0, "type": "normal", "lane": 2.0, "speed": -3.5, "dwell": 3.0},
		{"t": 14.0, "type": "gold", "lane": -0.5, "speed": 6.0, "dwell": 2.0},
		{"t": 14.0, "type": "gold", "lane": 3.0, "speed": -6.0, "dwell": 2.0},  # 双出
		{"t": 16.5, "type": "hostage", "lane": 1.0, "speed": 2.5, "dwell": 2.8},
		{"t": 17.5, "type": "normal", "lane": -2.5, "speed": 3.0, "dwell": 3.0},
		{"t": 19.5, "type": "gold", "lane": -3.5, "speed": 6.5, "dwell": 1.8},
		{"t": 19.5, "type": "gold", "lane": 0.5, "speed": -6.5, "dwell": 1.8},  # 双出
		{"t": 22.0, "type": "normal", "lane": 3.0, "speed": -3.0, "dwell": 2.8},
		{"t": 23.5, "type": "hostage", "lane": -1.5, "speed": 2.0, "dwell": 2.5},
		{"t": 24.5, "type": "gold", "lane": 1.5, "speed": 6.5, "dwell": 1.8},
		{"t": 24.5, "type": "gold", "lane": -2.0, "speed": -6.5, "dwell": 1.8}, # 双出收尾
	]}
