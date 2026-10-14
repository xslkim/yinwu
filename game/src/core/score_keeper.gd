class_name ScoreKeeper
extends RefCounted

## 计分器（纯逻辑）：命中累分 + 连击加成，脱靶清空连击
## 规则：第 N 连击命中得 base × N 分（base 普通靶 100 / 金靶 300）
## 人质误伤：扣分（不低于 0）且清空连击

const BASE_POINTS := 100
const GOLD_POINTS := 300
const HOSTAGE_PENALTY := 200

var score := 0
var combo := 0
var peak_combo := 0  ## 连击峰值（结算页展示）
var hits := 0
var misses := 0
var hostage_hits := 0  ## 人质误伤次数


## 命中：连击 +1，返回本次得分；base 由靶型决定
func register_hit(base := BASE_POINTS) -> int:
	combo += 1
	hits += 1
	peak_combo = maxi(peak_combo, combo)
	var gained := base * combo
	score += gained
	return gained


## 脱靶：连击清零
func register_miss() -> void:
	combo = 0
	misses += 1


## 人质误伤：扣分（下限 0）+ 清连击
func register_hostage_hit(penalty := HOSTAGE_PENALTY) -> void:
	hostage_hits += 1
	combo = 0
	score = maxi(0, score - penalty)


func reset() -> void:
	score = 0
	combo = 0
	peak_combo = 0
	hits = 0
	misses = 0
	hostage_hits = 0


## 命中率（0-1），无射击时返回 0
func accuracy() -> float:
	var total := hits + misses
	return float(hits) / total if total > 0 else 0.0
