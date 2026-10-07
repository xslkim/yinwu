package com.tvgun.tv;

import android.app.Activity;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.os.Bundle;
import android.os.Handler;
import android.view.View;
import android.view.WindowManager;
import android.widget.FrameLayout;

import com.tvgun.tv.core.CoordinateMapper;
import com.tvgun.tv.core.FrameGeometry;
import com.tvgun.tv.core.SelfTestSink;
import com.tvgun.tv.core.TvBridgeServer;

import java.util.Locale;
import java.util.Random;

/**
 * 自检模式（打鸭子）：全屏黑底 + 24px 白边框（BorderOverlayView）+ 弹跳白圆靶 +
 * 青色准星（aim 经 CoordinateMapper 映射）+ 命中计分。
 * 验证"手机 → TV → 坐标映射 → 命中"全链路。
 */
public final class SelfTestActivity extends Activity {

    private SelfTestSink sink;
    private GameView gameView;
    private final Handler handler = new Handler();
    private long lastTickNanos;
    private boolean running;

    private final Runnable ticker = new Runnable() {
        @Override
        public void run() {
            if (!running) {
                return;
            }
            long now = System.nanoTime();
            double dt = (now - lastTickNanos) / 1e9;
            lastTickNanos = now;
            if (sink != null && dt > 0 && dt < 0.2) {
                sink.advance(dt);
            }
            if (gameView != null) {
                gameView.invalidate();
            }
            handler.postDelayed(this, 16); // ~60fps
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        FrameLayout root = new FrameLayout(this);
        gameView = new GameView(this);
        root.addView(gameView, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        root.addView(new BorderOverlayView(this), new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        setContentView(root);
    }

    @Override
    protected void onResume() {
        super.onResume();
        running = true;
        lastTickNanos = System.nanoTime();
        handler.post(ticker);
    }

    @Override
    protected void onPause() {
        running = false;
        handler.removeCallbacks(ticker);
        Bridge.get().attachDefault();
        super.onPause();
    }

    /** view 尺寸确定后创建 sink 并挂到桥接上（目标矩形 = 全 view）。 */
    private void attachSink(int w, int h) {
        sink = new SelfTestSink(w, h, FrameGeometry.BORDER_THICK_PX,
                Math.max(24, h * 0.04), h * 0.35, new Random());
        Bridge.get().attach(sink, new TvBridgeServer.RectProvider() {
            @Override
            public double[] getRect() {
                return new double[]{0, 0, gameView.getWidth(), gameView.getHeight()};
            }
        });
    }

    private final class GameView extends View {

        private final Paint targetPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint crossPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint textPaint = new Paint(Paint.ANTI_ALIAS_FLAG);

        GameView(android.content.Context context) {
            super(context);
            targetPaint.setColor(Color.WHITE);
            targetPaint.setStyle(Paint.Style.FILL);
            crossPaint.setColor(0xFF00E5FF);
            crossPaint.setStyle(Paint.Style.STROKE);
            crossPaint.setStrokeWidth(4);
            textPaint.setColor(Color.WHITE);
            textPaint.setTextSize(48);
            textPaint.setTextAlign(Paint.Align.CENTER);
        }

        @Override
        protected void onSizeChanged(int w, int h, int oldw, int oldh) {
            if (w > 0 && h > 0 && (sink == null || w != oldw || h != oldh)) {
                attachSink(w, h);
            }
        }

        @Override
        protected void onDraw(Canvas canvas) {
            canvas.drawColor(Color.BLACK);
            if (sink == null) {
                return;
            }

            // 靶子
            canvas.drawCircle((float) sink.getTargetX(), (float) sink.getTargetY(),
                    (float) sink.getTargetRadius(), targetPaint);

            // 准星：aim 槽过期（>0.6s）时隐藏
            long age = Bridge.get().server() != null
                    ? Bridge.get().server().getAimSlot().ageMs() : -1;
            double ax = sink.getAimX();
            double ay = sink.getAimY();
            if (age >= 0 && age <= 600 && !Double.isNaN(ax)) {
                float x = (float) ax;
                float y = (float) ay;
                canvas.drawCircle(x, y, 36, crossPaint);
                canvas.drawLine(x - 56, y, x - 16, y, crossPaint);
                canvas.drawLine(x + 16, y, x + 56, y, crossPaint);
                canvas.drawLine(x, y - 56, x, y - 16, crossPaint);
                canvas.drawLine(x, y + 16, x, y + 56, crossPaint);
            }

            canvas.drawText(String.format(Locale.US, "得分 %d   命中 %d/%d",
                    sink.getScore(), sink.getHits(), sink.getShots()),
                    getWidth() / 2f, 96, textPaint);
        }
    }
}
