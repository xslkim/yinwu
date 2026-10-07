package com.tvgun.tv.core;

/**
 * 桥接输入事件的出口。坐标均为目标 view 像素（已经 CoordinateMapper 映射，未钳制，
 * 框外坐标表示屏外指向，用于换弹等语义）。实现方：自检（SelfTestSink）、
 * 容器注入（ContainerSink，容器就绪前为桩）。
 */
public interface InputSink {

    /** 瞄准流（手机 120Hz 推流，每个合法 UDP 包一次）。 */
    void onAim(double px, double py);

    /** 扳机（只有按下事件，无松开）。 */
    void onShot(double px, double py);

    /** 投币（扩展端点）。 */
    void onCoin();

    /** 开始（扩展端点）。 */
    void onStart();

    /** 换弹（扩展端点）。 */
    void onReload();
}
