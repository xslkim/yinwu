class_name SettingsManager
extends RefCounted

## 设置管理：音量（主/音乐/音效）持久化到 user://settings.cfg
## 上架前的完整设置页不在本期范围，F3 循环主音量档位

const STEPS := [1.0, 0.7, 0.4, 0.0]  ## F3 循环的音量档

var path := "user://settings.cfg"    ## 可注入（测试用临时路径）
var volumes := {"master": 1.0, "music": 0.8, "sfx": 1.0}


func load_settings() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(path) != OK:
		return  # 首次运行没有文件，用默认值
	for key in volumes:
		volumes[key] = clampf(cfg.get_value("audio", key, volumes[key]), 0.0, 1.0)


func save_settings() -> void:
	var cfg := ConfigFile.new()
	for key in volumes:
		cfg.set_value("audio", key, volumes[key])
	cfg.save(path)


## F3：主音量循环降档（到底回满），返回新档位
func cycle_master() -> float:
	var idx := STEPS.find(volumes.master)
	idx = (idx + 1) % STEPS.size()
	volumes.master = STEPS[idx]
	save_settings()
	return volumes.master
