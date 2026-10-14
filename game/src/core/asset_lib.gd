class_name AssetLib
extends RefCounted

## 素材库：统一的"有则用、无则 fallback"加载逻辑
## 素材 Agent 的产物放 res://assets/audio|textures/，本类只读不写
##
## 命名约定：
##   贴图：<name>.png / .jpg / .webp
##   音效：<name>.wav，同名变体 <name>_2.wav、<name>_3.wav …（最多 8 个）
##   BGM： bgm_<track>.ogg / .wav / .mp3

const MAX_VARIANTS := 8


## 返回 base 的所有现存变体路径（含无后缀的本体），按编号升序；一个都没有返回空
static func variants(dir: String, base: String, exts: PackedStringArray) -> PackedStringArray:
	var found := PackedStringArray()
	for ext in exts:
		var p := "%s/%s.%s" % [dir, base, ext]
		if ResourceLoader.exists(p) or FileAccess.file_exists(p):
			found.append(p)
		for i in range(2, MAX_VARIANTS + 1):
			p = "%s/%s_%d.%s" % [dir, base, i, ext]
			if ResourceLoader.exists(p) or FileAccess.file_exists(p):
				found.append(p)
	return found


## 随机挑一个变体；没有返回空串（调用方走 fallback）
static func pick_variant(dir: String, base: String, exts: PackedStringArray, rng := RandomNumberGenerator.new()) -> String:
	var all := variants(dir, base, exts)
	if all.is_empty():
		return ""
	return all[rng.randi_range(0, all.size() - 1)]


## 贴图加载：文件存在且能解码则返回 Texture2D，否则 null（调用方用纯色 fallback）
## 用 Image.load 而非 ResourceLoader：未 import 的新文件也能直接读，素材即拷即用
static func texture_or_null(path: String) -> Texture2D:
	if not FileAccess.file_exists(path) and not ResourceLoader.exists(path):
		return null
	var img := Image.new()
	if img.load(path) != OK:
		return null
	return ImageTexture.create_from_image(img)
