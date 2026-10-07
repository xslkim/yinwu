package com.tvgun.tv;

import com.tvgun.tv.core.AffineCalibration;
import com.tvgun.tv.core.ContainerSink;
import com.tvgun.tv.core.CoordinateMapper;
import com.tvgun.tv.core.InputSink;
import com.tvgun.tv.core.TvBridgeServer;

import java.io.IOException;

/**
 * 应用级桥接持有器：TvBridgeServer 单例的生命周期与当前输入出口切换。
 * 主界面/非游戏态挂 ContainerSink 桩；自检/校准 Activity 进入时换成自己的 sink，
 * 退出时恢复默认。
 */
public final class Bridge {

    private static Bridge instance;

    public static synchronized Bridge get() {
        if (instance == null) {
            instance = new Bridge();
        }
        return instance;
    }

    private final ContainerSink containerSink = new ContainerSink();
    private TvBridgeServer server;

    private Bridge() {
    }

    public synchronized void start(int port) throws IOException {
        stop();
        TvBridgeServer s = new TvBridgeServer(port);
        s.start();
        server = s;
        attachDefault();
    }

    public synchronized void stop() {
        if (server != null) {
            server.stop();
            server = null;
        }
    }

    public synchronized boolean isRunning() {
        return server != null && server.isStarted();
    }

    public synchronized int boundPort() {
        return server != null ? server.boundPort() : -1;
    }

    /** 直接访问底层服务（校准等场景注册原始坐标回调）。运行中返回非 null。 */
    public synchronized TvBridgeServer server() {
        return server;
    }

    /** 切换输入出口与目标 view 矩形。 */
    public synchronized void attach(InputSink sink, TvBridgeServer.RectProvider rectProvider) {
        if (server != null) {
            server.setSink(sink);
            server.setRectProvider(rectProvider);
        }
    }

    /** 恢复默认出口：容器桩 + 1920×1080 虚拟矩形（仅记日志）。 */
    public synchronized void attachDefault() {
        attach(containerSink, new TvBridgeServer.RectProvider() {
            @Override
            public double[] getRect() {
                return new double[]{0, 0, CoordinateMapper.NORM_W, CoordinateMapper.NORM_H};
            }
        });
    }

    public synchronized void applyCalibration(AffineCalibration calibration) {
        if (server != null) {
            server.getMapper().setCalibration(calibration);
        }
    }
}
