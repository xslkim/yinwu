class_name ScoreKeeper
extends RefCounted

## 计分器（纯逻辑）：命中累分 + 连击加成，脱靶清空连击
## 规则：第 N 连击命中得 100 × N 分（连击越高单发越值钱）

const BASE_POINTS := 100

var score := 0
var combo := 0
var hits := 0
var misses := 0


## 命中：连击 +1，返回本次得分
func register_hit() -> int:
	combo += 1
	hits += 1
	var gained := BASE_POINTS * combo
	score += gained
	return gained


## 脱靶：连击清零
func register_miss() -> void:
	combo = 0
	misses += 1


func reset() -> void:
	score = 0
	combo = 0
	hits = 0
	misses = 0


## 命中率（0-1），无射击时返回 0
func accuracy() -> float:
	var total := hits + misses
	return float(hits) / total if total > 0 else 0.0
