#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""ArcGun UDP 联调发送工具：模拟手机光枪发坐标流 + 事件包

用法（venv python，无需第三方依赖）:
  D:/tvgun/.venv/Scripts/python.exe game/tests/udp_sender.py [选项]

选项:
  --host 127.0.0.1   目标 IP
  --port 9000        目标端口
  --n 100            坐标包数量（约 100Hz 发送）
  --shot-every 30    每 N 个坐标包插一个 SHOT 事件（0 = 不发）
  --events           结束前补发 RELOAD / COIN / START 各一个
"""
import argparse
import math
import socket
import time


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument("--host", default="127.0.0.1")
    ap.add_argument("--port", type=int, default=9000)
    ap.add_argument("--n", type=int, default=100)
    ap.add_argument("--shot-every", type=int, default=30)
    ap.add_argument("--events", action="store_true")
    args = ap.parse_args()

    sock = socket.socket(socket.AF_INET, socket.SOCK_DGRAM)
    target = (args.host, args.port)

    # 坐标走圆形轨迹（1920x1080 规范坐标，一位小数），模拟人手画圈
    cx, cy, r = 960.0, 540.0, 300.0
    for i in range(args.n):
        t = i / 10.0
        x = cx + r * math.cos(t)
        y = cy + r * 0.6 * math.sin(t)
        sock.sendto(f"{x:.1f},{y:.1f}".encode(), target)
        if args.shot_every > 0 and (i + 1) % args.shot_every == 0:
            sock.sendto(b"SHOT", target)
        time.sleep(0.01)  # ~100Hz

    if args.events:
        for ev in (b"RELOAD", b"COIN", b"START"):
            sock.sendto(ev, target)
            time.sleep(0.05)

    print(f"已发送 {args.n} 个坐标包到 {args.host}:{args.port}")


if __name__ == "__main__":
    main()
