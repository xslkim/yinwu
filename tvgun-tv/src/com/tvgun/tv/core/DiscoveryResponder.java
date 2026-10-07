package com.tvgun.tv.core;

import java.io.IOException;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;

/**
 * 局域网自动发现应答器。手机向服务端口 + 1 广播 "TVGUN_DISCOVER"，本应答器向发送方
 * 回 "TVGUN_HERE &lt;port&gt;"，手机从应答源地址学到电视端 IP，实现零配置配对。
 */
public final class DiscoveryResponder {

    private final int servicePort;
    private final int listenPort;

    private DatagramSocket socket;
    private Thread thread;
    private volatile boolean running;

    /**
     * @param servicePort 应答中报告的服务端口（tvgun 默认 8000）。
     * @param listenPort  监听端口；<= 0 时取 servicePort + 1。
     */
    public DiscoveryResponder(int servicePort, int listenPort) {
        this.servicePort = servicePort;
        this.listenPort = listenPort > 0 ? listenPort : servicePort + 1;
    }

    public synchronized void start() throws IOException {
        if (socket != null) {
            throw new IllegalStateException("responder already started");
        }
        DatagramSocket s = new DatagramSocket(null);
        s.setReuseAddress(true);
        s.setBroadcast(true);
        s.bind(new InetSocketAddress("0.0.0.0", listenPort));
        socket = s;
        running = true;
        thread = new Thread(new Runnable() {
            @Override
            public void run() {
                loop();
            }
        }, "tvgun-discovery");
        thread.setDaemon(true);
        thread.start();
    }

    public int boundPort() {
        DatagramSocket s = socket;
        return s != null ? s.getLocalPort() : listenPort;
    }

    private void loop() {
        byte[] reply = (Protocol.DISCOVERY_REPLY_PREFIX + " " + servicePort)
                .getBytes(StandardCharsets.US_ASCII);
        byte[] buf = new byte[256];
        while (running) {
            DatagramPacket packet = new DatagramPacket(buf, buf.length);
            try {
                socket.receive(packet);
            } catch (IOException e) {
                return; // stop() 关闭 socket
            }
            String text = new String(packet.getData(), packet.getOffset(), packet.getLength(),
                    StandardCharsets.US_ASCII).trim();
            if (!Protocol.DISCOVERY_REQUEST.equals(text)) {
                continue;
            }
            try {
                socket.send(new DatagramPacket(reply, reply.length,
                        packet.getAddress(), packet.getPort()));
            } catch (IOException e) {
                // 发送方不可达，继续监听
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
