class_name UdpGunProvider
extends InputProvider

## UDP 手机光枪源（生产环境用）：
##   手机 App 通过局域网 UDP 发 ASCII 包到本机 9000 端口（可配置）
##   坐标包 "x,y"（1920×1080 规范坐标，约 60-120Hz）→ One Euro Filter 平滑
##   事件包 SHOT / RELOAD / COIN / START / EXIT → 对应信号
##   0.6 秒无坐标包则准星停移（保持最后位置）并标记过期

@export var port := 9000
@export var stale_timeout_msec := 600

# --- 调试统计（F1 浮层 / 联调日志用） ---
var packet_count := 0       ## 累计收包数
var aim_packet_count := 0   ## 累计坐标包数
var event_count := 0        ## 累计事件包数
var invalid_count := 0      ## 畸形包数
var last_packet_msec := -1  ## 最后收包时刻（-1 = 从未收到）

var _udp := PacketPeerUDP.new()
var _aim := Vector2(960, 540)
var _last_aim_msec := -1
var _filter_x := OneEuroFilter.new()
var _filter_y := OneEuroFilter.new()


func _ready() -> void:
	var err := _udp.bind(port)
	if err != OK:
		push_error("[UdpGun] 绑定端口 %d 失败: %s" % [port, error_string(err)])
	else:
		print("[UdpGun] 监听 UDP 端口 %d" % port)


func _exit_tree() -> void:
	_udp.close()


func _process(_delta: float) -> void:
	while _udp.get_available_packet_count() > 0:
		var text := _udp.get_packet().get_string_from_utf8()
		packet_count += 1
		last_packet_msec = Time.get_ticks_msec()
		var res := ProtocolParser.parse(text)
		match res.type:
			"aim":
				_handle_aim(res.x, res.y)
			"event":
				event_count += 1
				print("[UdpGun] 事件 %s（累计 %d）" % [res.event, event_count])
				match res.event:
					"shot": shot.emit()
					"reload": reloaded.emit()
					"coin": coin.emit()
					"start": started.emit()
					"exit": exited.emit()
			_:
				invalid_count += 1
		# 每 30 个坐标包打一次日志，手工联调时确认链路通
		if packet_count % 30 == 0:
			print("[UdpGun] 已收 %d 包（坐标 %d / 事件 %d / 畸形 %d），最后准星 %s" % [
				packet_count, aim_packet_count, event_count, invalid_count, _aim])


func _handle_aim(nx: float, ny: float) -> void:
	aim_packet_count += 1
	var now := Time.get_ticks_msec()
	# 采样间隔（滤波器需要）；首包按 60Hz 算
	var dt := 1.0 / 60.0
	if _last_aim_msec >= 0:
		dt = maxf(now - _last_aim_msec, 1) / 1000.0
	_last_aim_msec = now
	# 规范坐标（1920×1080）→ 实际视口像素
	var vp := get_viewport().get_visible_rect().size
	var px := nx / ProtocolParser.AIM_MAX_X * vp.x
	var py := ny / ProtocolParser.AIM_MAX_Y * vp.y
	_aim = Vector2(_filter_x.filter(px, dt), _filter_y.filter(py, dt))


func get_aim() -> Vector2:
	# 0.6s 无新坐标 → 准星停移：_aim 只在收到坐标包时更新，天然成立
	return _aim


func is_aim_fresh() -> bool:
	if _last_aim_msec < 0:
		return false
	return Time.get_ticks_msec() - _last_aim_msec <= stale_timeout_msec


func provider_name() -> String:
	return "UDP:%d" % port
