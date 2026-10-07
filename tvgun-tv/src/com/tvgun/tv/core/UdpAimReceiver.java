package com.tvgun.tv.core;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;

/**
 * UDP 瞄准接收器：在 0.0.0.0:port 上循环收 "x,y" 文本包，写入 AimSlot 并回调监听。
 * 畸形包静默丢弃；手机端 120Hz 推流，丢包无需处理。
 */
public final class UdpAimReceiver {

    /** 每收到一个合法瞄准包回调一次（接收线程上）。 */
    public interface AimListener {
        void onAim(double x, double y);
    }

    private static final int BUF_SIZE = 256;

    private final int port;
    private final AimSlot slot;
    private final AimListener listener;

    private DatagramSocket socket;
    private Thread thread;
    private volatile boolean running;

    public UdpAimReceiver(int port, AimSlot slot, AimListener listener) {
        this.port = port;
        this.slot = slot;
        this.listener = listener;
    }

    public synchronized void start() throws IOException {
        if (socket != null) {
            throw new IllegalStateException("receiver already started");
        }
        DatagramSocket s = new DatagramSocket(null);
        s.setReuseAddress(true);
        s.bind(new InetSocketAddress("0.0.0.0", port));
        socket = s;
        running = true;
        thread = new Thread(new Runnable() {
            @Override
            public void run() {
                loop();
            }
        }, "tvgun-aim-rx");
        thread.setDaemon(true);
        thread.start();
    }

    /** 实际绑定端口（构造端口为 0 时取系统分配的临时端口）。 */
    public int boundPort() {
        DatagramSocket s = socket;
        return s != null ? s.getLocalPort() : port;
    }

    private void loop() {
        byte[] buf = new byte[BUF_SIZE];
        while (running) {
            DatagramPacket packet = new DatagramPacket(buf, buf.length);
            try {
                socket.receive(packet);
            } catch (IOException e) {
                // stop() 关闭 socket 唤醒 receive；运行中异常直接退出循环。
                return;
            }
            String text = new String(packet.getData(), packet.getOffset(), packet.getLength(),
                    StandardCharsets.US_ASCII);
            double[] aim = Protocol.parseAim(text);
            if (aim == null) {
                continue; // 畸形包静默丢弃
            }
            slot.put(aim[0], aim[1]);
            if (listener != null) {
                listener.onAim(aim[0], aim[1]);
            }
        }
    }

    public synchronized void stop() {
        running = false;
        if (socket != null) {
            socket.close();
            socket = null;
        }
        if (thread != null) {
            try {
                thread.join(1000);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            thread = null;
        }
    }
}
