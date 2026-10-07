import com.tvgun.tv.core.AffineCalibration;
import com.tvgun.tv.core.AimSlot;
import com.tvgun.tv.core.BouncingTarget;
import com.tvgun.tv.core.CoordinateMapper;
import com.tvgun.tv.core.DiscoveryResponder;
import com.tvgun.tv.core.FrameGeometry;
import com.tvgun.tv.core.MiniHttpServer;
import com.tvgun.tv.core.Protocol;
import com.tvgun.tv.core.UdpAimReceiver;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.net.DatagramPacket;
import java.net.DatagramSocket;
import java.net.InetAddress;
import java.net.InetSocketAddress;
import java.net.Socket;
import java.nio.charset.StandardCharsets;
import java.util.Random;

/**
 * core 层桌面 JVM 单测（纯 Java，无 Android 依赖）。main 方法 + 断言计数 + 非零退出码，
 * 风格复刻 tvgun TrackerTest。覆盖：UDP 解析（正常/畸形/多包）、AimSlot 覆盖与超时、
 * /shot JSON 解析与响应格式、MiniHttpServer 回环（404/400/health）、发现应答、
 * AffineCalibration（恒等/往返/退化拒绝）、CoordinateMapper 四角与逆映射、
 * BouncingTarget 命中与边界、FrameGeometry 几何。
 */
public class CoreTest {

    static int pass;
    static int fail;

    static void check(boolean ok, String msg) {
        if (ok) {
            pass++;
            System.out.println("PASS " + msg);
        } else {
            fail++;
            System.out.println("FAIL " + msg);
        }
    }

    public static void main(String[] args) throws Exception {
        testParseAim();
        testFormatAim();
        testParseShotJson();
        testResponses();
        testAimSlot();
        testUdpReceiver();
        testDiscovery();
        testHttpServer();
        testAffineCalibration();
        testCoordinateMapper();
        testBouncingTarget();
        testFrameGeometry();

        System.out.println();
        System.out.println("passed=" + pass + " failed=" + fail);
        System.exit(fail == 0 ? 0 : 1);
    }

    // ---------- Protocol: UDP "x,y" ----------

    static void testParseAim() {
        double[] a = Protocol.parseAim("111.5,222.5");
        check(a != null && a[0] == 111.5 && a[1] == 222.5, "parseAim 正常包");

        a = Protocol.parseAim(" 960,540 \n");
        check(a != null && a[0] == 960.0 && a[1] == 540.0, "parseAim 带空白");

        a = Protocol.parseAim("-10.5,2000.25");
        check(a != null && a[0] == -10.5 && a[1] == 2000.25, "parseAim 框外坐标（不钳制）");

        check(Protocol.parseAim("") == null, "parseAim 空串丢弃");
        check(Protocol.parseAim(null) == null, "parseAim null 丢弃");
        check(Protocol.parseAim("garbage") == null, "parseAim 无逗号丢弃");
        check(Protocol.parseAim("1,2,3") == null, "parseAim 多逗号丢弃");
        check(Protocol.parseAim("1,") == null, "parseAim 缺 y 丢弃");
        check(Protocol.parseAim(",2") == null, "parseAim 缺 x 丢弃");
        check(Protocol.parseAim("abc,def") == null, "parseAim 非数字丢弃");
        check(Protocol.parseAim("NaN,1") == null, "parseAim NaN 丢弃");
    }

    static void testFormatAim() {
        check("111.5,222.5".equals(Protocol.formatAim(111.5, 222.5)), "formatAim 1 位小数");
        check("960.0,540.0".equals(Protocol.formatAim(960, 540)), "formatAim 整数补 .0");
        double[] rt = Protocol.parseAim(Protocol.formatAim(123.456, 789.012));
        check(rt != null && Math.abs(rt[0] - 123.5) < 1e-9 && Math.abs(rt[1] - 789.0) < 1e-9,
                "format/parse 往返");
    }

    // ---------- Protocol: /shot JSON ----------

    static void testParseShotJson() {
        double[] s = Protocol.parseShotJson("{\"x\":123.4,\"y\":567.8}");
        check(s != null && s[0] == 123.4 && s[1] == 567.8, "shotJson 紧凑");

        s = Protocol.parseShotJson("{ \"y\" : 540 , \"x\" : 960 }");
        check(s != null && s[0] == 960.0 && s[1] == 540.0, "shotJson 空白+键乱序");

        s = Protocol.parseShotJson("{\"x\":-12.5e1,\"y\":0}");
        check(s != null && s[0] == -125.0 && s[1] == 0.0, "shotJson 科学计数法/负值");

        check(Protocol.parseShotJson("") == null, "shotJson 空体拒绝");
        check(Protocol.parseShotJson(null) == null, "shotJson null 拒绝");
        check(Protocol.parseShotJson("{}") == null, "shotJson 缺键拒绝");
        check(Protocol.parseShotJson("{\"x\":\"960\",\"y\":540}") == null, "shotJson 字符串值拒绝");
        check(Protocol.parseShotJson("{\"x\":960}") == null, "shotJson 只有 x 拒绝");
        check(Protocol.parseShotJson("not json") == null, "shotJson 非 JSON 拒绝");

        s = Protocol.parseShotJson("{\"ax\":1,\"x\":2,\"y\":3}");
        check(s != null && s[0] == 2.0 && s[1] == 3.0, "shotJson 相似键不误配");
    }

    static void testResponses() {
        check("{\"hit\":true,\"score\":0}".equals(Protocol.shotResponse()), "shot 响应格式");
        check("{\"ok\":true}".equals(Protocol.okResponse()), "ok 响应格式");
        check("{\"ok\":true,\"aimAge\":123}".equals(Protocol.healthResponse(123)), "health 有样本");
        check("{\"ok\":true,\"aimAge\":null}".equals(Protocol.healthResponse(-1)), "health 空槽");
        check(Protocol.errorResponse("bad").contains("\"error\":\"bad\""), "error 响应格式");
    }

    // ---------- AimSlot ----------

    static void testAimSlot() {
        final long[] now = {1000};
        AimSlot.Clock clock = new AimSlot.Clock() {
            @Override
            public long nowMs() {
                return now[0];
            }
        };
        AimSlot slot = new AimSlot(600, clock);

        check(slot.tryGet() == null && slot.ageMs() == -1, "slot 初始为空");

        slot.put(100, 200);
        double[] v = slot.tryGet();
        check(v != null && v[0] == 100 && v[1] == 200 && v[2] == 0, "slot 写入可读");

        now[0] += 100;
        slot.put(300, 400);
        v = slot.tryGet();
        check(v != null && v[0] == 300 && v[1] == 400 && v[2] == 0, "slot 最新覆盖");

        now[0] += 599;
        check(slot.tryGet() != null, "slot 超时边界内有效");
        now[0] += 2; // 年龄 601 > 600
        check(slot.tryGet() == null, "slot 超时过期");
        check(slot.ageMs() == 601, "slot 过期仍报告真实年龄");

        slot.put(1, 2);
        slot.clear();
        check(slot.tryGet() == null && slot.ageMs() == -1, "slot clear 后为空");
    }

    // ---------- UdpAimReceiver（真实回环） ----------

    static void testUdpReceiver() throws Exception {
        AimSlot slot = new AimSlot();
        final int[] events = {0};
        UdpAimReceiver rx = new UdpAimReceiver(0, slot, new UdpAimReceiver.AimListener() {
            @Override
            public void onAim(double x, double y) {
                synchronized (events) {
                    events[0]++;
                }
            }
        });
        rx.start();
        int port = rx.boundPort();
        check(port > 0, "udp 绑定临时端口 " + port);

        DatagramSocket sender = new DatagramSocket();
        InetAddress loop = InetAddress.getByName("127.0.0.1");
        send(sender, loop, port, "100.0,200.0");
        send(sender, loop, port, "garbage");      // 畸形，静默丢弃
        send(sender, loop, port, "1,2,3");        // 畸形
        send(sender, loop, port, "300.5,400.5");  // 最后一条合法包生效

        double[] v = null;
        for (int i = 0; i < 100; i++) {
            v = slot.tryGet();
            if (v != null && v[0] == 300.5) {
                break;
            }
            Thread.sleep(10);
        }
        check(v != null && v[0] == 300.5 && v[1] == 400.5, "udp 多包最新覆盖、畸形丢弃");
        Thread.sleep(50);
        synchronized (events) {
            check(events[0] == 2, "udp 监听回调仅合法包（2 次，实际 " + events[0] + "）");
        }
        sender.close();
        rx.stop();
    }

    static void send(DatagramSocket s, InetAddress addr, int port, String text) throws IOException {
        byte[] b = text.getBytes(StandardCharsets.US_ASCII);
        s.send(new DatagramPacket(b, b.length, addr, port));
    }

    // ---------- DiscoveryResponder（真实回环） ----------

    static void testDiscovery() throws Exception {
        DiscoveryResponder resp = new DiscoveryResponder(18000, 0); // 监听 18001
        // 用临时端口避免与真实服务冲突：另起一个任意端口
        resp = new DiscoveryResponder(18000, pickUdpPort());
        resp.start();
        int port = resp.boundPort();

        DatagramSocket probe = new DatagramSocket();
        probe.setSoTimeout(2000);
        send(probe, InetAddress.getByName("127.0.0.1"), port, Protocol.DISCOVERY_REQUEST);
        byte[] buf = new byte[256];
        DatagramPacket reply = new DatagramPacket(buf, buf.length);
        probe.receive(reply);
        String text = new String(reply.getData(), 0, reply.getLength(), StandardCharsets.US_ASCII);
        check(("TVGUN_HERE 18000").equals(text), "discovery 应答 TVGUN_HERE <port>，实际: " + text);

        send(probe, InetAddress.getByName("127.0.0.1"), port, "SOMETHING_ELSE");
        boolean silent = false;
        try {
            probe.setSoTimeout(300);
            probe.receive(reply);
        } catch (java.net.SocketTimeoutException e) {
            silent = true;
        }
        check(silent, "discovery 非请求报文不应答");

        probe.close();
        resp.stop();
    }

    static int pickUdpPort() throws IOException {
        DatagramSocket s = new DatagramSocket(0);
        int p = s.getLocalPort();
        s.close();
        return p;
    }

    // ---------- MiniHttpServer（真实回环） ----------

    static final class HttpResult {
        int status;
        String body;
    }

    static HttpResult http(int port, String request) throws IOException {
        Socket s = new Socket();
        s.connect(new InetSocketAddress("127.0.0.1", port), 2000);
        s.setSoTimeout(2000);
        OutputStream out = s.getOutputStream();
        out.write(request.getBytes(StandardCharsets.US_ASCII));
        out.flush();
        InputStream in = s.getInputStream();
        String raw = readAll(in);
        s.close();
        HttpResult r = new HttpResult();
        int sp1 = raw.indexOf(' ');
        int sp2 = sp1 >= 0 ? raw.indexOf(' ', sp1 + 1) : -1;
        r.status = sp2 > sp1 ? Integer.parseInt(raw.substring(sp1 + 1, sp2).trim()) : -1;
        int sep = raw.indexOf("\r\n\r\n");
        r.body = sep >= 0 ? raw.substring(sep + 4) : "";
        return r;
    }

    static String readAll(InputStream in) throws IOException {
        StringBuilder sb = new StringBuilder();
        byte[] buf = new byte[4096];
        int n;
        try {
            while ((n = in.read(buf)) != -1) {
                sb.append(new String(buf, 0, n, StandardCharsets.UTF_8));
            }
        } catch (java.net.SocketTimeoutException e) {
            // 超时按已有数据返回
        }
        return sb.toString();
    }

    static String post(String path, String body) {
        return "POST " + path + " HTTP/1.1\r\n"
                + "Host: 127.0.0.1\r\n"
                + "Content-Type: application/json\r\n"
                + "Content-Length: " + body.getBytes(StandardCharsets.UTF_8).length + "\r\n"
                + "\r\n" + body;
    }

    static void testHttpServer() throws Exception {
        final AimSlot slot = new AimSlot();
        final double[] lastShot = {Double.NaN, Double.NaN};
        final int[] coinStartReload = {0, 0, 0};
        MiniHttpServer server = new MiniHttpServer(0);
        server.setAimAgeSupplier(new MiniHttpServer.AimAgeSupplier() {
            @Override
            public long aimAgeMs() {
                return slot.ageMs();
            }
        });
        server.setListener(new MiniHttpServer.Listener() {
            @Override
            public void onShot(double x, double y) {
                lastShot[0] = x;
                lastShot[1] = y;
            }

            @Override
            public void onCoin() {
                coinStartReload[0]++;
            }

            @Override
            public void onStart() {
                coinStartReload[1]++;
            }

            @Override
            public void onReload() {
                coinStartReload[2]++;
            }
        });
        server.start();
        int port = server.boundPort();

        HttpResult r = http(port, post("/shot", "{\"x\":123.4,\"y\":567.8}"));
        check(r.status == 200 && "{\"hit\":true,\"score\":0}".equals(r.body),
                "POST /shot 200 固定响应，实际 " + r.status + " " + r.body);
        check(lastShot[0] == 123.4 && lastShot[1] == 567.8, "POST /shot 事件坐标");

        r = http(port, post("/shot", "not json"));
        check(r.status == 400 && r.body.contains("\"error\""), "POST /shot 畸形 JSON 400");

        r = http(port, post("/shot", "{\"x\":1}"));
        check(r.status == 400, "POST /shot 缺 y 400");

        r = http(port, post("/coin", ""));
        check(r.status == 200 && "{\"ok\":true}".equals(r.body),
                "POST /coin 200，实际 " + r.status + " " + r.body);
        r = http(port, post("/start", ""));
        check(r.status == 200, "POST /start 200");
        r = http(port, post("/reload", ""));
        check(r.status == 200, "POST /reload 200");
        check(coinStartReload[0] == 1 && coinStartReload[1] == 1 && coinStartReload[2] == 1,
                "扩展端点事件各触发一次");

        r = http(port, "GET /health HTTP/1.1\r\nHost: x\r\n\r\n");
        check(r.status == 200 && r.body.contains("\"ok\":true") && r.body.contains("\"aimAge\":null"),
                "GET /health 空槽 aimAge:null，实际 " + r.body);

        slot.put(1, 1);
        r = http(port, "GET /health HTTP/1.1\r\nHost: x\r\n\r\n");
        check(r.status == 200 && r.body.contains("\"aimAge\":") && !r.body.contains("null"),
                "GET /health 有样本 aimAge:数字，实际 " + r.body);

        r = http(port, "GET /nope HTTP/1.1\r\nHost: x\r\n\r\n");
        check(r.status == 404, "GET 未知路径 404");

        r = http(port, post("/nope", "{}"));
        check(r.status == 404, "POST 未知路径 404");

        r = http(port, "PUT /shot HTTP/1.1\r\nHost: x\r\nContent-Length: 0\r\n\r\n");
        check(r.status == 404, "非 POST/GET-health 方法 404");

        server.stop();
    }

    // ---------- AffineCalibration ----------

    static void testAffineCalibration() {
        AffineCalibration id = AffineCalibration.identity();
        double[] t = id.transform(123.4, 567.8);
        check(t[0] == 123.4 && t[1] == 567.8, "cal 恒等变换");

        // 两点：raw(100,100)->target(113,113)，raw(1700,900)->target(1713,913)（+13px 平移）
        AffineCalibration cal = AffineCalibration.solve(
                100, 100, 113, 113,
                1700, 900, 1713, 913);
        check(cal != null, "cal 解算成功");
        double[] p = cal.transform(100, 100);
        check(Math.abs(p[0] - 113) < 1e-9 && Math.abs(p[1] - 113) < 1e-9, "cal 往返点1");
        p = cal.transform(1700, 900);
        check(Math.abs(p[0] - 1713) < 1e-9 && Math.abs(p[1] - 913) < 1e-9, "cal 往返点2");

        // 各向异性缩放 + 平移
        cal = AffineCalibration.solve(200, 200, 190, 180, 1800, 1000, 1790, 980);
        check(cal != null && Math.abs(cal.a - 1.0) < 1e-9 && Math.abs(cal.b + 10) < 1e-9
                && Math.abs(cal.c - 1.0) < 1e-9 && Math.abs(cal.d + 20) < 1e-9, "cal 系数正确");

        // 退化：x 跨度 < 100 → 拒绝
        check(AffineCalibration.solve(500, 100, 500, 100, 550, 900, 550, 900) == null,
                "cal x 跨度不足拒绝");
        check(AffineCalibration.solve(100, 500, 100, 500, 1700, 550, 1700, 550) == null,
                "cal y 跨度不足拒绝");

        // JSON 序列化往返
        AffineCalibration c2 = AffineCalibration.fromJson(
                AffineCalibration.solve(100, 100, 113, 113, 1700, 900, 1713, 913).toJson());
        check(c2 != null && Math.abs(c2.a - 1) < 1e-6 && Math.abs(c2.b - 13) < 1e-6,
                "cal JSON 往返");
        check(AffineCalibration.fromJson("{}") == null, "cal JSON 缺字段拒绝");
    }

    // ---------- CoordinateMapper ----------

    static void testCoordinateMapper() {
        CoordinateMapper m = new CoordinateMapper();
        // 目标矩形 (100,50) 960x540
        double[] p = m.map(0, 0, 100, 50, 960, 540);
        check(p[0] == 100 && p[1] == 50, "map 左上角");
        p = m.map(1920, 1080, 100, 50, 960, 540);
        check(p[0] == 1060 && p[1] == 590, "map 右下角");
        p = m.map(960, 540, 100, 50, 960, 540);
        check(p[0] == 580 && p[1] == 320, "map 中心");
        p = m.map(-48, 1080 + 24, 100, 50, 960, 540);
        check(p[0] == 100 - 24 && p[1] == 590 + 12, "map 框外不钳制（屏外换弹）");

        // 逆映射往返
        double[] raw = m.mapInverse(580, 320, 100, 50, 960, 540);
        check(Math.abs(raw[0] - 960) < 1e-9 && Math.abs(raw[1] - 540) < 1e-9, "mapInverse 往返");

        // 带校准
        m.setCalibration(AffineCalibration.solve(100, 100, 113, 113, 1700, 900, 1713, 913));
        p = m.map(100, 100, 0, 0, 1920, 1080);
        check(Math.abs(p[0] - 113) < 1e-9 && Math.abs(p[1] - 113) < 1e-9, "map 应用校准");
        raw = m.mapInverse(113, 113, 0, 0, 1920, 1080);
        check(Math.abs(raw[0] - 100) < 1e-6 && Math.abs(raw[1] - 100) < 1e-6, "mapInverse 逆转校准");
    }

    // ---------- BouncingTarget ----------

    static void testBouncingTarget() {
        Random rng = new Random(7);
        BouncingTarget t = new BouncingTarget(50, 300, rng, 1920, 1080, 24);
        check(t.getX() >= 74 && t.getX() <= 1920 - 74 && t.getY() >= 74 && t.getY() <= 1080 - 74,
                "target 初始位置在游戏区内");

        t.place(960, 540);
        check(t.isHit(960, 540), "isHit 圆心命中");
        check(t.isHit(960 + 49, 540), "isHit 半径内命中");
        check(!t.isHit(960 + 51, 540), "isHit 半径外不中");
        check(!t.isHit(0, 0), "isHit 远处不中");

        // 长时推进不出界
        boolean inBounds = true;
        for (int i = 0; i < 20000; i++) {
            t.advance(0.05, 1920, 1080, 24);
            if (t.getX() < 74 - 1e-6 || t.getX() > 1920 - 74 + 1e-6
                    || t.getY() < 74 - 1e-6 || t.getY() > 1080 - 74 + 1e-6) {
                inBounds = false;
                break;
            }
        }
        check(inBounds, "target 20000 步推进不出界");

        // 换位仍在界内
        t.relocate(rng, 1920, 1080, 24);
        check(t.getX() >= 74 && t.getY() >= 74, "target 换位在界内");
    }

    // ---------- FrameGeometry ----------

    static void testFrameGeometry() {
        double[][] rects = FrameGeometry.compute(1920, 1080, FrameGeometry.BORDER_THICK_PX);
        check(rects.length == 12, "frame 12 个矩形");
        check(rects[0][0] == 0 && rects[0][1] == 0 && rects[0][2] == 1920 && rects[0][3] == 24,
                "frame 上边条");
        check(rects[3][0] == 1920 - 24 && rects[3][2] == 24 && rects[3][3] == 1080,
                "frame 右边条");
        double corner = 24 * FrameGeometry.CORNER_LEN_FACTOR;
        check(corner == 72 && rects[4][2] == corner && rects[5][3] == corner,
                "frame 角标长度 3×边厚");
    }
}
