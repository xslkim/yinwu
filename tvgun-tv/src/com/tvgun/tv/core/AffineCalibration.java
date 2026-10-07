package com.tvgun.tv.core;

import java.util.Locale;

/**
 * 两点仿射校准：cx = a·nx + b, cy = c·ny + d（平移 + 各向异性缩放），在 tvgun 规范
 * 坐标系（1920×1080）内表达。用户对准两个已知规范坐标的靶点各开一枪，用上报的原始
 * 坐标解算修正量，一次性吸收边框外缘基准偏差 + 枪管安装偏差 + 广角镜头偏差。
 * 无校准数据时为恒等变换。语义对齐 PC 版 TvgunBridge.Core/Coordinates/AffineCalibration.cs。
 */
public final class AffineCalibration {

    /** 两个校准点在任一轴上的最小跨度（规范坐标单位），小于此拒绝解算。 */
    public static final double MIN_SPAN = 100.0;

    public final double a;
    public final double b;
    public final double c;
    public final double d;

    public AffineCalibration(double a, double b, double c, double d) {
        this.a = a;
        this.b = b;
        this.c = c;
        this.d = d;
    }

    /** 恒等变换（不修正）。 */
    public static AffineCalibration identity() {
        return new AffineCalibration(1.0, 0.0, 1.0, 0.0);
    }

    /**
     * 由两组 原始→目标 对应点解算仿射系数。
     * @return 校准结果；两点在任一轴跨度 &lt; MIN_SPAN（退化输入）时返回 null。
     */
    public static AffineCalibration solve(double raw1X, double raw1Y,
                                          double target1X, double target1Y,
                                          double raw2X, double raw2Y,
                                          double target2X, double target2Y) {
        double spanX = raw2X - raw1X;
        double spanY = raw2Y - raw1Y;
        if (Math.abs(spanX) < MIN_SPAN || Math.abs(spanY) < MIN_SPAN) {
            return null;
        }
        double a = (target2X - target1X) / spanX;
        double b = target1X - a * raw1X;
        double c = (target2Y - target1Y) / spanY;
        double d = target1Y - c * raw1Y;
        return new AffineCalibration(a, b, c, d);
    }

    /** 应用变换：{a·nx + b, c·ny + d}。 */
    public double[] transform(double nx, double ny) {
        return new double[]{a * nx + b, c * ny + d};
    }

    public String toJson() {
        return String.format(Locale.US,
                "{\"a\":%.12g,\"b\":%.12g,\"c\":%.12g,\"d\":%.12g}", a, b, c, d);
    }

    /** 从 JSON 恢复；字段缺失或畸形返回 null。 */
    public static AffineCalibration fromJson(String json) {
        if (json == null) {
            return null;
        }
        Double a = Protocol.jsonNumber(json, "a");
        Double b = Protocol.jsonNumber(json, "b");
        Double c = Protocol.jsonNumber(json, "c");
        Double d = Protocol.jsonNumber(json, "d");
        if (a == null || b == null || c == null || d == null) {
            return null;
        }
        return new AffineCalibration(a, b, c, d);
    }
}
