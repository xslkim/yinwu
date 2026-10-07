package com.tvgun.tv.core;

/**
 * 白边框几何：4 条实心边条 + 每角两条 L 形角标，共 12 个矩形。
 * 参数沿用 tvgun 参考：BORDER_THICK = 24px，角标长度 = 3 × 边厚。
 * 与 PC 版 Rendering/FrameGeometry.cs 一致；纯逻辑，Android 层据此绘制。
 */
public final class FrameGeometry {

    /** 边框厚度（物理像素）。 */
    public static final double BORDER_THICK_PX = 24.0;

    /** 角标长度相对边厚的倍数。 */
    public static final double CORNER_LEN_FACTOR = 3.0;

    private FrameGeometry() {
    }

    /**
     * 计算边框矩形组。
     * @return 12 个 {x, y, w, h}：0-3 为上/下/左/右边条，4-11 为四角 L 标
     *         （左上/右上/左下/右下，各先横后竖）。
     */
    public static double[][] compute(double width, double height, double thickness) {
        double t = thickness;
        double corner = t * CORNER_LEN_FACTOR;
        return new double[][]{
                {0, 0, width, t},                       // 上边
                {0, height - t, width, t},              // 下边
                {0, 0, t, height},                      // 左边
                {width - t, 0, t, height},              // 右边
                {0, 0, corner, t},                      // 左上-横
                {0, 0, t, corner},                      // 左上-竖
                {width - corner, 0, corner, t},         // 右上-横
                {width - t, 0, t, corner},              // 右上-竖
                {0, height - t, corner, t},             // 左下-横
                {0, height - corner, t, corner},        // 左下-竖
                {width - corner, height - t, corner, t},// 右下-横
                {width - t, height - corner, t, corner},// 右下-竖
        };
    }
}
