package com.tvgun.tv.core;

/**
 * 规范坐标（0..1920 × 0..1080，原点为白边框外缘）→ 目标 view 像素矩形的线性映射。
 * 先应用可选的 AffineCalibration，再 px = rect.x + cx/1920·rect.w。
 * 不做钳制：框外坐标映射到矩形外，用于支持屏外换弹。
 */
public final class CoordinateMapper {

    public static final double NORM_W = 1920.0;
    public static final double NORM_H = 1080.0;

    private volatile AffineCalibration calibration = AffineCalibration.identity();

    public AffineCalibration getCalibration() {
        return calibration;
    }

    public void setCalibration(AffineCalibration calibration) {
        this.calibration = calibration != null ? calibration : AffineCalibration.identity();
    }

    /**
     * 规范坐标 → 目标矩形像素坐标。不钳制。
     * @return {px, py}
     */
    public double[] map(double nx, double ny,
                        double rectX, double rectY, double rectW, double rectH) {
        double[] corrected = calibration.transform(nx, ny);
        return new double[]{
                rectX + corrected[0] / NORM_W * rectW,
                rectY + corrected[1] / NORM_H * rectH
        };
    }

    /**
     * map 的逆：像素坐标 → 原始规范坐标（逆转校准）。
     * 校为准模式下校准为恒等时，返回值即手机上报的原始坐标。
     * @return {nx, ny}
     */
    public double[] mapInverse(double px, double py,
                               double rectX, double rectY, double rectW, double rectH) {
        double nx = (px - rectX) / rectW * NORM_W;
        double ny = (py - rectY) / rectH * NORM_H;
        AffineCalibration cal = calibration;
        return new double[]{
                cal.a != 0 ? (nx - cal.b) / cal.a : nx,
                cal.c != 0 ? (ny - cal.d) / cal.c : ny
        };
    }
}
