package com.tvgun.tv.core;

import java.io.IOException;

/**
 * 桥接服务门面：组合 UDP 瞄准接收、HTTP 协议服务、局域网发现应答。
 * Start/Stop 可重复；端口可配。事件经 CoordinateMapper 映射到目标 view 像素后
 * 派发给当前 InputSink；原始规范坐标射击事件另经 RawShotListener 暴露（校准用）。
 */
public final class TvBridgeServer {

    /** 目标 view 像素矩形 {x, y, w, h} 提供者（运行时随界面尺寸变化）。 */
    public interface RectProvider {
        double[] getRect();
    }

    /** 原始规范坐标射击回调（两点校准用，未经校准/映射）。 */
    public interface RawShotListener {
        void onRawShot(double nx, double ny);
    }

    private final int port;
    private final AimSlot aimSlot = new AimSlot();
    private final CoordinateMapper mapper = new CoordinateMapper();

    private volatile InputSink sink;
    private volatile RectProvider rectProvider;
    private volatile RawShotListener rawShotListener;

    private UdpAimReceiver udp;
    private DiscoveryResponder discovery;
    private MiniHttpServer http;
    private boolean started;

    public TvBridgeServer(int port) {
        this.port = port;
    }

    public void setSink(InputSink sink) {
        this.sink = sink;
    }

    public void setRectProvider(RectProvider provider) {
        this.rectProvider = provider;
    }

    public void setRawShotListener(RawShotListener listener) {
        this.rawShotListener = listener;
    }

    public AimSlot getAimSlot() {
        return aimSlot;
    }

    public CoordinateMapper getMapper() {
        return mapper;
    }

    public synchronized void start() throws IOException {
        if (started) {
            throw new IllegalStateException("server already started");
        }
        udp = new UdpAimReceiver(port, aimSlot, new UdpAimReceiver.AimListener() {
            @Override
            public void onAim(double nx, double ny) {
                InputSink s = sink;
                RectProvider rp = rectProvider;
                if (s == null || rp == null) {
                    return;
                }
                double[] r = rp.getRect();
                if (r == null || r.length < 4 || r[2] <= 0 || r[3] <= 0) {
                    return;
                }
                double[] p = mapper.map(nx, ny, r[0], r[1], r[2], r[3]);
                s.onAim(p[0], p[1]);
            }
        });
        udp.start();
        int actualPort = udp.boundPort();

        http = new MiniHttpServer(actualPort);
        http.setAimAgeSupplier(new MiniHttpServer.AimAgeSupplier() {
            @Override
            public long aimAgeMs() {
                return aimSlot.ageMs();
            }
        });
        http.setListener(new MiniHttpServer.Listener() {
            @Override
            public void onShot(double nx, double ny) {
                RawShotListener raw = rawShotListener;
                if (raw != null) {
                    raw.onRawShot(nx, ny);
                }
                InputSink s = sink;
                RectProvider rp = rectProvider;
                if (s == null || rp == null) {
                    return;
                }
                double[] r = rp.getRect();
                if (r == null || r.length < 4 || r[2] <= 0 || r[3] <= 0) {
                    return;
                }
                double[] p = mapper.map(nx, ny, r[0], r[1], r[2], r[3]);
                s.onShot(p[0], p[1]);
            }

            @Override
            public void onCoin() {
                InputSink s = sink;
                if (s != null) {
                    s.onCoin();
                }
            }

            @Override
            public void onStart() {
                InputSink s = sink;
                if (s != null) {
                    s.onStart();
                }
            }

            @Override
            public void onReload() {
                InputSink s = sink;
                if (s != null) {
                    s.onReload();
                }
            }
        });
        try {
            http.start();
        } catch (IOException e) {
            udp.stop();
            udp = null;
            throw e;
        }

        discovery = new DiscoveryResponder(actualPort, 0);
        try {
            discovery.start();
        } catch (IOException e) {
            http.stop();
            http = null;
            udp.stop();
            udp = null;
            throw e;
        }
        started = true;
    }

    /** 实际服务端口（构造端口为 0 时取系统分配值；发现应答报告同一端口）。 */
    public synchronized int boundPort() {
        return udp != null ? udp.boundPort() : port;
    }

    public synchronized boolean isStarted() {
        return started;
    }

    public synchronized void stop() {
        if (discovery != null) {
            discovery.stop();
            discovery = null;
        }
        if (http != null) {
            http.stop();
            http = null;
        }
        if (udp != null) {
            udp.stop();
            udp = null;
        }
        started = false;
    }
}
