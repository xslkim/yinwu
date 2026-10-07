package com.tvgun.tv;

import android.graphics.Canvas;
import android.graphics.Paint;
import android.view.View;

import com.tvgun.tv.core.FrameGeometry;

/**
 * 可复用的白边框叠加 View：24px 实心四边 + 四角 L 形角标（3×边厚），
 * 几何与 PC 版 FrameGeometry 一致。自检 Activity 使用；以后容器 Activity 直接叠加复用。
 */
public final class BorderOverlayView extends View {

    private final Paint paint = new Paint();

    public BorderOverlayView(android.content.Context context) {
        super(context);
        paint.setColor(0xFFFFFFFF);
        paint.setStyle(Paint.Style.FILL);
    }

    @Override
    protected void onDraw(Canvas canvas) {
        double[][] rects = FrameGeometry.compute(getWidth(), getHeight(),
                FrameGeometry.BORDER_THICK_PX);
        for (double[] r : rects) {
            canvas.drawRect((float) r[0], (float) r[1],
                    (float) (r[0] + r[2]), (float) (r[1] + r[3]), paint);
        }
    }
}
