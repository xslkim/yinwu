class_name InputProvider
extends Node

## 输入抽象基类 —— 本项目的核心纪律：
##   游戏逻辑（命中判定、弹药、计分）只允许通过本接口拿输入，
##   全项目禁止在 game 逻辑里直接读 Input 单例。
##
## 为什么继承 Node 而不是 RefCounted：
##   鼠标源需要 _unhandled_input 回调、UDP 源需要每帧 _process 轮询，
##   Node 天然挂在场景树里能拿到这两个生命周期，省去手动桥接。

signal shot      ## 开火
signal reloaded  ## 换弹
signal coin      ## 投币
signal started   ## 开始


## 当前准星位置（屏幕像素坐标）
func get_aim() -> Vector2:
	return Vector2.ZERO


## 输入源名称（调试浮层显示用）
func provider_name() -> String:
	return "Base"


## 准星数据是否新鲜（UDP 源 0.6s 无包会过期，用于准星变灰提示）
func is_aim_fresh() -> bool:
	return true
