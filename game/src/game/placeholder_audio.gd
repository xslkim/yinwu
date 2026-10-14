class_name PlaceholderAudio
extends Node

## 占位音效：不引外部素材，用 AudioStreamGenerator 实时合成正弦短音
## 开火 = 短促高频，换弹 = 两连中频；后续换真素材时只需替换本节点

var _player: AudioStreamPlayer
var _playback: AudioStreamGeneratorPlayback


func _ready() -> void:
	var gen := AudioStreamGenerator.new()
	gen.mix_rate = 22050.0
	gen.buffer_length = 0.1
	_player = AudioStreamPlayer.new()
	_player.stream = gen
	add_child(_player)
	_player.play()
	_playback = _player.get_stream_playback()


func _exit_tree() -> void:
	# 释放 playback / stream 引用，否则退出时报 ObjectDB 泄漏
	_player.stop()
	_playback = null
	_player.stream = null


## 合成一段正弦音（freq Hz，dur 秒，带简单衰减包络防爆音）
func blip(freq := 880.0, dur := 0.07, volume := 0.25) -> void:
	if _playback == null:
		return  # headless / 无声卡环境下静默跳过
	var rate := 22050.0
	var frames := int(rate * dur)
	for i in frames:
		if not _playback.can_push_buffer(1):
			break
		var t := float(i) / rate
		var env := 1.0 - float(i) / frames  # 线性衰减
		var s := sin(t * freq * TAU) * volume * env
		_playback.push_frame(Vector2(s, s))
