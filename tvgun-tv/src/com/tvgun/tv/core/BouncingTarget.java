package com.tvgun.tv.core;

import java.util.Random;

/**
 * 自检靶子的匀速弹跳物理（打鸭子模式）：恒定速度、边界镜面反弹、命中判定、随机换位。
 * 纯逻辑无 UI 依赖，坐标单位由调用方给定。语义对齐 PC 版 SelfTest/BouncingTarget.cs。
 */
public final class BouncingTarget {

    private final double radius;
    private final double speed;

    private double x;
    private double y;
    private double vx;
    private double vy;

    /** 在随机位置以随机朝向创建靶子。 */
    public BouncingTarget(double radius, double speed, Random random,
                          double width, double height, double inset) {
        this.radius = radius;
        this.speed = speed;
        relocate(random, width, height, inset);
        double angle = random.nextDouble() * Math.PI * 2.0;
        vx = Math.cos(angle) * speed;
        vy = Math.sin(angle) * speed;
    }

    public double getRadius() {
        return radius;
    }

    public double getSpeed() {
        return speed;
    }

    public double getX() {
        return x;
    }

    public double getY() {
        return y;
    }

    /** 显式放置（确定性测试用），速度保持构造值。 */
    public void place(double x, double y) {
        this.x = x;
        this.y = y;
    }

    /**
     * 推进 dt 秒，在 [inset, width-inset] × [inset, height-inset]（含半径余量）内反弹。
     */
    public void advance(double dtSeconds, double width, double height, double inset) {
        double minX = inset + radius;
        double maxX = Math.max(minX, width - inset - radius);
        double minY = inset + radius;
        double maxY = Math.max(minY, height - inset - radius);

        double nx = x + vx * dtSeconds;
        double ny = y + vy * dtSeconds;

        if (nx < minX) {
            nx = 2 * minX - nx;
            vx = Math.abs(vx);
        } else if (nx > maxX) {
            nx = 2 * maxX - nx;
            vx = -Math.abs(vx);
        }
        if (ny < minY) {
            ny = 2 * minY - ny;
            vy = Math.abs(vy);
        } else if (ny > maxY) {
            ny = 2 * maxY - ny;
            vy = -Math.abs(vy);
        }

        x = clamp(nx, minX, maxX);
        y = clamp(ny, minY, maxY);
    }

    /** 把靶子移到游戏区内随机位置。 */
    public void relocate(Random random, double width, double height, double inset) {
        double minX = inset + radius;
        double maxX = Math.max(minX, width - inset - radius);
        double minY = inset + radius;
        double maxY = Math.max(minY, height - inset - radius);
        x = minX + random.nextDouble() * (maxX - minX);
        y = minY + random.nextDouble() * (maxY - minY);
    }

    /** 点是否落在命中圆内。 */
    public boolean isHit(double px, double py) {
        double dx = px - x;
        double dy = py - y;
        return Math.sqrt(dx * dx + dy * dy) < radius;
    }

    private static double clamp(double v, double lo, double hi) {
        return v < lo ? lo : (v > hi ? hi : v);
    }
}
