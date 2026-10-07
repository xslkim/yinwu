package com.tvgun.tv.core;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.ServerSocket;
import java.net.Socket;
import java.net.InetSocketAddress;
import java.nio.charset.StandardCharsets;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.ThreadFactory;

/**
 * 手写最小 HTTP/1.1 服务（ServerSocket + 线程池，零第三方依赖）。
 * 路由：POST /shot /coin /start /reload、GET /health；其余 404。
 * 每个请求处理完即关闭连接（Connection: close），无 keep-alive。
 */
public final class MiniHttpServer {

    /** 协议事件回调（线程池线程上触发）。 */
    public interface Listener {
        void onShot(double x, double y);

        void onCoin();

        void onStart();

        void onReload();
    }

    /** GET /health 的 aimAge 数据源；返回 -1 表示槽空。 */
    public interface AimAgeSupplier {
        long aimAgeMs();
    }

    private static final int MAX_LINE = 8192;
    private static final int MAX_BODY = 64 * 1024;
    private static final int SO_TIMEOUT_MS = 10_000;

    private final int port;

    private volatile Listener listener;
    private volatile AimAgeSupplier aimAgeSupplier;

    private ServerSocket server;
    private ExecutorService pool;
    private Thread acceptThread;
    private volatile boolean running;

    public MiniHttpServer(int port) {
        this.port = port;
    }

    public void setListener(Listener listener) {
        this.listener = listener;
    }

    public void setAimAgeSupplier(AimAgeSupplier supplier) {
        this.aimAgeSupplier = supplier;
    }

    public synchronized void start() throws IOException {
        if (server != null) {
            throw new IllegalStateException("server already started");
        }
        ServerSocket s = new ServerSocket();
        s.setReuseAddress(true);
        s.bind(new InetSocketAddress("0.0.0.0", port));
        server = s;
        pool = Executors.newFixedThreadPool(8, new ThreadFactory() {
            private int n;

            @Override
            public synchronized Thread newThread(Runnable r) {
                Thread t = new Thread(r, "tvgun-http-io-" + (++n));
                t.setDaemon(true);
                return t;
            }
        });
        running = true;
        acceptThread = new Thread(new Runnable() {
            @Override
            public void run() {
                acceptLoop();
            }
        }, "tvgun-http-accept");
        acceptThread.setDaemon(true);
        acceptThread.start();
    }

    /** 实际绑定端口（构造端口为 0 时取系统分配的临时端口）。 */
    public int boundPort() {
        ServerSocket s = server;
        return s != null ? s.getLocalPort() : port;
    }

    private void acceptLoop() {
        while (running) {
            final Socket socket;
            try {
                socket = server.accept();
            } catch (IOException e) {
                return; // stop() 关闭 server socket
            }
            pool.execute(new Runnable() {
                @Override
                public void run() {
                    handle(socket);
                }
            });
        }
    }

    private void handle(Socket socket) {
        try {
            socket.setSoTimeout(SO_TIMEOUT_MS);
            InputStream in = socket.getInputStream();
            OutputStream out = socket.getOutputStream();

            String requestLine = readLine(in);
            if (requestLine == null) {
                return; // 对端直接断开
            }
            String[] parts = requestLine.split(" ");
            if (parts.length < 2) {
                writeResponse(out, 400, "Bad Request", "text/plain", "bad request");
                return;
            }
            String method = parts[0];
            String path = parts[1];
            int q = path.indexOf('?');
            if (q >= 0) {
                path = path.substring(0, q);
            }

            long contentLength = 0;
            String line;
            while ((line = readLine(in)) != null && !line.isEmpty()) {
                int colon = line.indexOf(':');
                if (colon > 0 && line.substring(0, colon).trim().equalsIgnoreCase("Content-Length")) {
                    try {
                        contentLength = Long.parseLong(line.substring(colon + 1).trim());
                    } catch (NumberFormatException e) {
                        contentLength = 0;
                    }
                }
            }
            if (line == null) {
                return; // 头部未读完连接已断
            }
            String body = "";
            if (contentLength > 0) {
                if (contentLength > MAX_BODY) {
                    writeResponse(out, 400, "Bad Request", "application/json",
                            Protocol.errorResponse("body too large"));
                    return;
                }
                body = new String(readFully(in, (int) contentLength), StandardCharsets.UTF_8);
            }

            route(out, method, path, body);
        } catch (IOException e) {
            // 对端中途断开等，静默
        } finally {
            try {
                socket.close();
            } catch (IOException e) {
                // 已关闭
            }
        }
    }

    private void route(OutputStream out, String method, String path, String body) throws IOException {
        if ("GET".equals(method) && "/health".equals(path)) {
            AimAgeSupplier supplier = aimAgeSupplier;
            long age = supplier != null ? supplier.aimAgeMs() : -1;
            writeResponse(out, 200, "OK", "application/json", Protocol.healthResponse(age));
            return;
        }
        if (!"POST".equals(method)) {
            writeResponse(out, 404, "Not Found", "text/plain", "not found");
            return;
        }
        Listener l = listener;
        switch (path) {
            case "/shot": {
                double[] shot = Protocol.parseShotJson(body);
                if (shot == null) {
                    writeResponse(out, 400, "Bad Request", "application/json",
                            Protocol.errorResponse("invalid JSON body, expected {\"x\": float, \"y\": float}"));
                    return;
                }
                if (l != null) {
                    l.onShot(shot[0], shot[1]);
                }
                writeResponse(out, 200, "OK", "application/json", Protocol.shotResponse());
                return;
            }
            case "/coin":
                if (l != null) {
                    l.onCoin();
                }
                writeResponse(out, 200, "OK", "application/json", Protocol.okResponse());
                return;
            case "/start":
                if (l != null) {
                    l.onStart();
                }
                writeResponse(out, 200, "OK", "application/json", Protocol.okResponse());
                return;
            case "/reload":
                if (l != null) {
                    l.onReload();
                }
                writeResponse(out, 200, "OK", "application/json", Protocol.okResponse());
                return;
            default:
                writeResponse(out, 404, "Not Found", "text/plain", "not found");
        }
    }

    /** 读一行（以 \n 结尾，剥离 \r）。EOF 时返回 null；超长截断返回 null 并视为断线。 */
    private static String readLine(InputStream in) throws IOException {
        StringBuilder sb = new StringBuilder();
        int c;
        boolean any = false;
        while ((c = in.read()) != -1) {
            any = true;
            if (c == '\n') {
                int len = sb.length();
                if (len > 0 && sb.charAt(len - 1) == '\r') {
                    sb.setLength(len - 1);
                }
                return sb.toString();
            }
            if (sb.length() >= MAX_LINE) {
                return null;
            }
            sb.append((char) c);
        }
        return any ? sb.toString() : null;
    }

    private static byte[] readFully(InputStream in, int n) throws IOException {
        byte[] buf = new byte[n];
        int off = 0;
        while (off < n) {
            int r = in.read(buf, off, n - off);
            if (r < 0) {
                break;
            }
            off += r;
        }
        if (off < n) {
            byte[] truncated = new byte[off];
            System.arraycopy(buf, 0, truncated, 0, off);
            return truncated;
        }
        return buf;
    }

    private static void writeResponse(OutputStream out, int status, String reason,
                                      String contentType, String body) throws IOException {
        byte[] payload = body.getBytes(StandardCharsets.UTF_8);
        String head = "HTTP/1.1 " + status + " " + reason + "\r\n"
                + "Content-Type: " + contentType + "\r\n"
                + "Content-Length: " + payload.length + "\r\n"
                + "Connection: close\r\n"
                + "\r\n";
        out.write(head.getBytes(StandardCharsets.US_ASCII));
        out.write(payload);
        out.flush();
    }

    public synchronized void stop() {
        running = false;
        if (server != null) {
            try {
                server.close();
            } catch (IOException e) {
                // 已关闭
            }
            server = null;
        }
        if (acceptThread != null) {
            try {
                acceptThread.join(1000);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
            }
            acceptThread = null;
        }
        if (pool != null) {
            pool.shutdownNow();
            pool = null;
        }
    }
}
