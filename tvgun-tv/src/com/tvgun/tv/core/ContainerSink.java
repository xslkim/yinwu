package com.tvgun.tv.core;

/**
 * 容器输入注入桩。游戏容器（Winlator 类）就绪前，所有事件仅记录日志。
 *
 * TODO(容器集成)：容器 Activity 就绪后把本类替换/扩展为真实注入实现，对接要点：
 *  1. 瞄准流：onAim 收到的是容器画面 view 像素绝对坐标（未钳制）。转换为容器内
 *     X Server 屏幕坐标后，走 Winlator 式 injectPointer（XInput2 绝对移动事件）
 *     注入，无需 root、无需 SendInput。参考 docs/teknoparrot-android/07 第 3 章。
 *  2. 扳机：onShot 只有按下事件，需自行合成完整 click（button down + 固定 ~30ms 后
 *     up），与 PC 版 SendInput 注入器同语义。
 *  3. 换弹：坐标落在画面矩形外（屏外开枪）时按游戏策略映射为右键或框外坐标；
 *     /reload 扩展端点为显式换弹，直接映射右键。
 *  4. 投币/开始：映射为容器内键盘事件（默认 5=投币、1=开始，可配）。
 *  5. 事件均在工作线程触发，注入前需切换到容器的输入线程/消息队列。
 */
public final class ContainerSink implements InputSink {

    /** 简单日志出口；Android 上 System.out 进 logcat，桌面 JVM 进 stdout。 */
    public interface Logger {
        void log(String message);
    }

    private final Logger logger;

    public ContainerSink() {
        this(new Logger() {
            @Override
            public void log(String message) {
                System.out.println(message);
            }
        });
    }

    public ContainerSink(Logger logger) {
        this.logger = logger;
    }

    @Override
    public void onAim(double px, double py) {
        // 120Hz 流，默认不逐条打日志；容器集成时在此转发 injectPointer 绝对移动。
    }

    @Override
    public void onShot(double px, double py) {
        logger.log(String.format(java.util.Locale.US,
                "[ContainerSink] shot at (%.1f, %.1f) -- TODO 容器 injectPointer click 合成", px, py));
    }

    @Override
    public void onCoin() {
        logger.log("[ContainerSink] coin -- TODO 容器键盘事件（投币）");
    }

    @Override
    public void onStart() {
        logger.log("[ContainerSink] start -- TODO 容器键盘事件（开始）");
    }

    @Override
    public void onReload() {
        logger.log("[ContainerSink] reload -- TODO 容器右键/换弹映射");
    }
}
