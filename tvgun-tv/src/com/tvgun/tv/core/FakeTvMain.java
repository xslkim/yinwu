package com.tvgun.tv.core;

import java.io.IOException;
import java.net.ServerSocket;
import java.util.Locale;
import java.util.Random;

/**
 * headless 假电视：在桌面 JVM 上起完整 TvBridgeServer + SelfTestSink（打鸭子逻辑），
 * 供 e2e.sh 用 ReplayClient 做无手机的真端到端闭环验证。
 *
 * 用法：
 *   java com.tvgun.tv.core.FakeTvMain [--port N] [--static-target] [--seed N]
 *
 * --port 0（默认）自动挑一对空闲端口（服务端口 p 与发现端口 p+1）。
 * --static-target 把靶子固定在 (960,540) 且速度为 0，使 e2e 命中断言确定性。
 * 输出：LISTEN port=&lt;p&gt; 就绪行；每次射击一行 "SHOT x,y HIT|MISS score=N"；
 * 收到 SIGTERM 时经 shutdown hook 打印 BYE 并干净退出。
 */
public final class FakeTvMain {

    private FakeTvMain() {
    }

    public static void main(String[] args) throws Exception {
        int port = 0;
        boolean staticTarget = false;
        long seed = 42;
        for (int i = 0; i < args.length; i++) {
            switch (args[i]) {
                case "--port":
                    port = Integer.parseInt(args[++i]);
                    break;
                case "--static-target":
                    staticTarget = true;
                    break;
                case "--seed":
                    seed = Long.parseLong(args[++i]);
                    break;
                default:
                    System.err.println("unknown argument: " + args[i]);
                    System.exit(1);
            }
        }

        TvBridgeServer server = null;
        IOException lastError = null;
        for (int attempt = 0; attempt < 20 && server == null; attempt++) {
            int candidate = port > 0 ? port : pickFreePort();
            TvBridgeServer s = new TvBridgeServer(candidate);
            try {
                s.start();
                server = s;
            } catch (IOException e) {
                lastError = e;
                if (port > 0) {
                    break; // 指定端口失败不重试
                }
            }
        }
        if (server == null) {
            System.err.println("failed to bind: " + lastError);
            System.exit(1);
        }

        final SelfTestSink sink = new SelfTestSink(
                CoordinateMapper.NORM_W, CoordinateMapper.NORM_H,
                FrameGeometry.BORDER_THICK_PX,
                60.0, staticTarget ? 0.0 : 400.0, new Random(seed));
        if (staticTarget) {
            sink.placeTarget(CoordinateMapper.NORM_W / 2.0, CoordinateMapper.NORM_H / 2.0);
        }
        sink.setShotListener(new SelfTestSink.ShotListener() {
            @Override
            public void onShotResult(boolean hit, int score, double px, double py) {
                System.out.println(String.format(Locale.US,
                        "SHOT %.1f,%.1f %s score=%d", px, py, hit ? "HIT" : "MISS", score));
            }
        });

        final TvBridgeServer running = server;
        running.setSink(sink);
        running.setRectProvider(new TvBridgeServer.RectProvider() {
            @Override
            public double[] getRect() {
                return new double[]{0, 0, CoordinateMapper.NORM_W, CoordinateMapper.NORM_H};
            }
        });
        System.out.println("LISTEN port=" + running.boundPort());

        Runtime.getRuntime().addShutdownHook(new Thread(new Runnable() {
            @Override
            public void run() {
                running.stop();
                System.out.println("BYE");
            }
        }));

        Thread.currentThread().join(); // 永久驻留，直到被 kill
    }

    /** 挑一个空闲 TCP 端口作为候选（UDP/发现端口冲突由上层重试兜底）。 */
    private static int pickFreePort() throws IOException {
        ServerSocket probe = new ServerSocket(0);
        try {
            return probe.getLocalPort();
        } finally {
            probe.close();
        }
    }
}
