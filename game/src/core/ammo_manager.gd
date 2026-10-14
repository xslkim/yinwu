class_name AmmoManager
extends RefCounted

## 弹药管理（纯逻辑）：弹匣 6 发，打空必须换弹

var capacity := 6
var current := 6


func _init(p_capacity := 6) -> void:
	capacity = p_capacity
	current = p_capacity


func can_shoot() -> bool:
	return current > 0


## 消耗一发；弹空返回 false 且不消耗
func consume() -> bool:
	if current <= 0:
		return false
	current -= 1
	return true


func reload() -> void:
	current = capacity


func is_empty() -> bool:
	return current <= 0
