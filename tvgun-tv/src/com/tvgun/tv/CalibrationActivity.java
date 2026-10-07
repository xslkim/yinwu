package com.tvgun.tv;

import android.app.Activity;
import android.content.SharedPreferences;
import android.graphics.Canvas;
import android.graphics.Color;
import android.graphics.Paint;
import android.os.Bundle;
import android.view.View;
import android.view.WindowManager;
import android.widget.FrameLayout;

import com.tvgun.tv.core.AffineCalibration;
import com.tvgun.tv.core.CoordinateMapper;
import com.tvgun.tv.core.TvBridgeServer;

/**
 * 两点校准：全屏依次显示 2 个靶点（15%,15% 与 85%,85%），用户用手机瞄准各开一枪。
 * 通过 RawShotListener 拿到手机上报的原始规范坐标，两点齐后 Solve 仿射修正并持久化到
 * SharedPreferences，立即应用到桥接坐标引擎。
 */
public final class CalibrationActivity extends Activity {

    /** 靶点在规范坐标系中的位置（与屏幕 15%/85% 对应）。 */
    private static final double[][] TARGETS_NORM = {
            {0.15 * CoordinateMapper.NORM_W, 0.15 * CoordinateMapper.NORM_H},
            {0.85 * CoordinateMapper.NORM_W, 0.85 * CoordinateMapper.NORM_H},
    };

    private final double[][] rawPoints = new double[2][2];
    private int step; // 0/1 采点中，2 完成
    private String message = "";
    private CalibView view;

    private final TvBridgeServer.RawShotListener rawListener = new TvBridgeServer.RawShotListener() {
        @Override
        public void onRawShot(final double nx, final double ny) {
            runOnUiThread(new Runnable() {
                @Override
                public void run() {
                    onPoint(nx, ny);
                }
            });
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);
        FrameLayout root = new FrameLayout(this);
        view = new CalibView(this);
        root.addView(view, new FrameLayout.LayoutParams(
                FrameLayout.LayoutParams.MATCH_PARENT, FrameLayout.LayoutParams.MATCH_PARENT));
        setContentView(root);
    }

    @Override
    protected void onResume() {
        super.onResume();
        TvBridgeServer server = Bridge.get().server();
        if (server != null) {
            server.setRawShotListener(rawListener);
        }
    }

    @Override
    protected void onPause() {
        TvBridgeServer server = Bridge.get().server();
        if (server != null) {
            server.setRawShotListener(null);
        }
        super.onPause();
    }

    private void onPoint(double nx, double ny) {
        if (step >= 2) {
            return;
        }
        rawPoints[step][0] = nx;
        rawPoints[step][1] = ny;
        step++;
        if (step == 2) {
            finishCalibration();
        }
        view.invalidate();
    }

    private void finishCalibration() {
        AffineCalibration cal = AffineCalibration.solve(
                rawPoints[0][0], rawPoints[0][1], TARGETS_NORM[0][0], TARGETS_NORM[0][1],
                rawPoints[1][0], rawPoints[1][1], TARGETS_NORM[1][0], TARGETS_NORM[1][1]);
        if (cal == null) {
            message = "校准失败：两枪太近（任一轴跨度需 ≥100 规范单位），按返回键退出后重试";
            step = 0;
            return;
        }
        getSharedPreferences(MainActivity.PREFS, MODE_PRIVATE).edit()
                .putBoolean(MainActivity.KEY_HAS_CAL, true)
                .putLong("cal_a", Double.doubleToRawLongBits(cal.a))
                .putLong("cal_b", Double.doubleToRawLongBits(cal.b))
                .putLong("cal_c", Double.doubleToRawLongBits(cal.c))
                .putLong("cal_d", Double.doubleToRawLongBits(cal.d))
                .apply();
        Bridge.get().applyCalibration(cal);
        message = String.format(java.util.Locale.US,
                "校准完成并已保存：a=%.4f b=%.1f c=%.4f d=%.1f（按返回键退出）",
                cal.a, cal.b, cal.c, cal.d);
    }

    private final class CalibView extends View {

        private final Paint targetPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint ringPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint textPaint = new Paint(Paint.ANTI_ALIAS_FLAG);
        private final Paint subPaint = new Paint(Paint.ANTI_ALIAS_FLAG);

        CalibView(android.content.Context context) {
            super(context);
            targetPaint.setColor(0xFFFF5252);
            targetPaint.setStyle(Paint.Style.FILL);
            ringPaint.setColor(Color.WHITE);
            ringPaint.setStyle(Paint.Style.STROKE);
            ringPaint.setStrokeWidth(6);
            textPaint.setColor(Color.WHITE);
            textPaint.setTextSize(56);
            textPaint.setTextAlign(Paint.Align.CENTER);
            subPaint.setColor(0xFFAAAAAA);
            subPaint.setTextSize(36);
            subPaint.setTextAlign(Paint.Align.CENTER);
        }

        @Override
        protected void onDraw(Canvas canvas) {
            canvas.drawColor(Color.BLACK);
            float cx = getWidth() / 2f;

            if (step < 2) {
                double fx = step == 0 ? 0.15 : 0.85;
                double fy = step == 0 ? 0.15 : 0.85;
                float tx = (float) (getWidth() * fx);
                float ty = (float) (getHeight() * fy);
                canvas.drawCircle(tx, ty, 60, ringPaint);
                canvas.drawCircle(tx, ty, 24, targetPaint);
                canvas.drawText("两点校准  第 " + (step + 1) + "/2 点",
                        cx, getHeight() * 0.45f, textPaint);
                canvas.drawText("用手机准星对准红色靶心，扣扳机（点手机屏幕）",
                        cx, getHeight() * 0.45f + 64, subPaint);
            } else {
                canvas.drawText("校准结果", cx, getHeight() * 0.4f, textPaint);
                canvas.drawText(message, cx, getHeight() * 0.4f + 72, subPaint);
            }
        }
    }
}
