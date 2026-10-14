class_name ProtocolParser
extends RefCounted

## UDP 协议解析器（纯逻辑，不依赖场景树）
## 两种包：
##   坐标包  "x,y"   —— ASCII，浮点（一位小数），1920×1080 规范坐标系
##   事件包  SHOT / RELOAD / COIN / START / EXIT —— 纯文本
## 返回 Dictionary：
##   合法坐标 → {"ok": true, "type": "aim", "x": float, "y": float}
##   合法事件 → {"ok": true, "type": "event", "event": "shot"/"reload"/"coin"/"start"/"exit"}
##   畸形包   → {"ok": false, "type": "invalid"}

const AIM_MAX_X := 1920.0
const AIM_MAX_Y := 1080.0

const EVENTS := {
	"SHOT": "shot",
	"RELOAD": "reload",
	"COIN": "coin",
	"START": "start",
	"EXIT": "exit",
}


static func parse(raw: String) -> Dictionary:
	var text := raw.strip_edges()
	if text.is_empty():
		return {"ok": false, "type": "invalid"}

	# 事件包（大小写不敏感，手机端实现成本低）
	var upper := text.to_upper()
	if EVENTS.has(upper):
		return {"ok": true, "type": "event", "event": EVENTS[upper]}

	# 坐标包：恰好两段、都是合法浮点、且在规范坐标系范围内
	# 注意 split 保留空段："1,,2" 必须判畸形而不是吞掉空段
	var parts := text.split(",", true)
	if parts.size() == 2:
		var sx := parts[0].strip_edges()
		var sy := parts[1].strip_edges()
		if sx.is_valid_float() and sy.is_valid_float():
			var x := sx.to_float()
			var y := sy.to_float()
			if x >= 0.0 and x <= AIM_MAX_X and y >= 0.0 and y <= AIM_MAX_Y:
				return {"ok": true, "type": "aim", "x": x, "y": y}

	return {"ok": false, "type": "invalid"}
