class_name LevelManager
extends RefCounted

## 微关卡串烧状态机（纯逻辑，不碰场景树）
##
## 状态流转：
##   attract（待机，等投币）
##     └─ insert_coin + press_start → title（关卡标题闪现 1.5s）
##   title → playing（倒计时 duration 秒）
##   playing → result（结算闪现 2s，判定过关/失败）
##   result → 下一关 title；全部过关或中途失败 → gameover（总结算页）
##   gameover --press_start--> title（再来一局，同步完成，重开延迟 <2s）

signal level_title_shown(idx: int, def: Dictionary)
signal level_started(idx: int, def: Dictionary)
signal level_finished(idx: int, passed: bool, level_score: int)
signal game_finished(win: bool)
signal state_changed(state: String)

const TITLE_TIME := 1.5
const RESULT_TIME := 2.0

var levels: Array = []
var score_keeper: ScoreKeeper

var credits := 0
var state := "attract"
var level_idx := -1
var time_left := 0.0
var level_score := 0       ## 本关得分（过关判定的依据）
var last_passed := false

var _phase_time := 0.0
var _level_start_score := 0


func _init(p_levels: Array, p_score_keeper: ScoreKeeper) -> void:
	levels = p_levels
	score_keeper = p_score_keeper


func insert_coin() -> void:
	credits += 1


func can_start() -> bool:
	return credits > 0 and state in ["attract", "gameover"]


## 开始/再来一局。成功返回 true；无币或未到时返回 false
func press_start() -> bool:
	if not can_start():
		return false
	credits -= 1
	score_keeper.reset()
	level_idx = -1
	_advance_to_title()
	return true


## 每帧推进状态机（由场景层在 _process 调用）
func tick(dt: float) -> void:
	match state:
		"title":
			_phase_time -= dt
			if _phase_time <= 0.0:
				state = "playing"
				time_left = current_level().duration
				_level_start_score = score_keeper.score
				state_changed.emit(state)
				level_started.emit(level_idx, current_level())
		"playing":
			time_left = maxf(0.0, time_left - dt)
			if time_left <= 0.0:
				_finish_level()
		"result":
			_phase_time -= dt
			if _phase_time <= 0.0:
				if last_passed and level_idx < levels.size() - 1:
					_advance_to_title()
				else:
					_finish_game()


func current_level() -> Dictionary:
	return levels[level_idx]


## 剩余时间比例（0-1，HUD 计时条用）
func time_ratio() -> float:
	if state != "playing":
		return 0.0
	return time_left / current_level().duration


func _advance_to_title() -> void:
	level_idx += 1
	state = "title"
	_phase_time = TITLE_TIME
	state_changed.emit(state)
	level_title_shown.emit(level_idx, current_level())


func _finish_level() -> void:
	level_score = score_keeper.score - _level_start_score
	last_passed = level_score >= current_level().goal
	state = "result"
	_phase_time = RESULT_TIME
	state_changed.emit(state)
	level_finished.emit(level_idx, last_passed, level_score)


func _finish_game() -> void:
	state = "gameover"
	state_changed.emit(state)
	game_finished.emit(last_passed and level_idx == levels.size() - 1)
