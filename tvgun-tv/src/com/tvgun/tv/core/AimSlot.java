package com.tvgun.tv.core;

/**
 * 线程安全的"最新覆盖"瞄准槽。样本带到达时间戳，超过超时（默认 0.6s，与 tvgun
 * Python 参考实现一致）后读到的为空槽，避免手机停流后准星停在旧位置。
 * 时钟可注入，便于桌面 JVM 单测。
 */
public final class AimSlot {

    /** 毫秒时钟。 */
    public interface Clock {
        long nowMs();
    }

    public static final long DEFAULT_TIMEOUT_MS = 600;

    private final Object lock = new Object();
    private final long timeoutMs;
    private final Clock clock;

    private double x;
    private double y;
    private long timestampMs;
    private boolean has;

    public AimSlot() {
        this(DEFAULT_TIMEOUT_MS, new Clock() {
            @Override
            public long nowMs() {
                return System.currentTimeMillis();
            }
        });
    }

    public AimSlot(long timeoutMs, Clock clock) {
        this.timeoutMs = timeoutMs;
        this.clock = clock;
    }

    /** 写入新样本，覆盖旧样本；时间戳取本地到达时刻。 */
    public void put(double x, double y) {
        synchronized (lock) {
            this.x = x;
            this.y = y;
            this.timestampMs = clock.nowMs();
            this.has = true;
        }
    }

    /** 清空槽。 */
    public void clear() {
        synchronized (lock) {
            has = false;
        }
    }

    /**
     * 读当前样本。
     * @return {x, y, ageMs}；槽空或样本已过期时返回 null。
     */
    public double[] tryGet() {
        synchronized (lock) {
            if (!has) {
                return null;
            }
            long age = clock.nowMs() - timestampMs;
            if (age > timeoutMs) {
                return null;
            }
            return new double[]{x, y, age};
        }
    }

    public boolean hasValue() {
        return tryGet() != null;
    }

    /** 最新样本的年龄（毫秒）；槽空时返回 -1。过期样本仍报告真实年龄。 */
    public long ageMs() {
        synchronized (lock) {
            return has ? clock.nowMs() - timestampMs : -1;
        }
    }
}
