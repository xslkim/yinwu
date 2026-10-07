package com.tvgun.tv.core;

import java.util.Random;

/**
 * 自检游戏逻辑（打鸭子）：持有一个 BouncingTarget，接收映射后的瞄准/射击事件，
 * 判定命中、计分、命中后随机换位。坐标空间为构造时给定的虚拟游戏区
 * （Android 端取 view 像素；headless 端可取 1920×1080 规范空间）。线程安全。
 */
public final class SelfTestSink implements InputSink {

    /** 每次射击的结果回调（命中与否、累计得分、射击点）。 */
    public interface ShotListener {
        void onShotResult(boolean hit, int score, double px, double py);
    }

    private final double width;
    private final double height;
    private final double inset;
    private final BouncingTarget target;
    private final Random random;

    private volatile ShotListener shotListener;

    private int shots;
    private int hits;
    private int score;
    private int coins;
    private int starts;
    private int reloads;
    private double aimX = Double.NaN;
    private double aimY = Double.NaN;

    /**
     * @param width/height 游戏区尺寸（sink 坐标单位）
     * @param inset        可活动区内缩（通常为边框厚度）
     * @param targetRadius 命中半径
     * @param targetSpeed  弹跳速度（单位/秒；0 = 静止）
     */
    public SelfTestSink(double width, double height, double inset,
                        double targetRadius, double targetSpeed, Random random) {
        this.width = width;
        this.height = height;
        this.inset = inset;
        this.random = random;
        this.target = new BouncingTarget(targetRadius, targetSpeed, random, width, height, inset);
    }

    public void setShotListener(ShotListener listener) {
        this.shotListener = listener;
    }

    /** 由外部节拍（UI 帧或测试）推进靶子。 */
    public synchronized void advance(double dtSeconds) {
        target.advance(dtSeconds, width, height, inset);
    }

    /** 显式放置靶子（确定性测试用）。 */
    public synchronized void placeTarget(double x, double y) {
        target.place(x, y);
    }

    @Override
    public synchronized void onAim(double px, double py) {
        aimX = px;
        aimY = py;
    }

    @Override
    public void onShot(double px, double py) {
        boolean hit;
        int newScore;
        synchronized (this) {
            shots++;
            hit = target.isHit(px, py);
            if (hit) {
                hits++;
                score += 10;
                target.relocate(random, width, height, inset);
            }
            newScore = score;
        }
        ShotListener l = shotListener;
        if (l != null) {
            l.onShotResult(hit, newScore, px, py);
        }
    }

    @Override
    public synchronized void onCoin() {
        coins++;
    }

    @Override
    public synchronized void onStart() {
        starts++;
    }

    @Override
    public synchronized void onReload() {
        reloads++;
    }

    public synchronized double getTargetX() {
        return target.getX();
    }

    public synchronized double getTargetY() {
        return target.getY();
    }

    public double getTargetRadius() {
        return target.getRadius();
    }

    public synchronized double getAimX() {
        return aimX;
    }

    public synchronized double getAimY() {
        return aimY;
    }

    public synchronized int getShots() {
        return shots;
    }

    public synchronized int getHits() {
        return hits;
    }

    public synchronized int getScore() {
        return score;
    }

    public synchronized int getCoins() {
        return coins;
    }

    public synchronized int getStarts() {
        return starts;
    }

    public synchronized int getReloads() {
        return reloads;
    }
}
