package com.tvgun.tv;

import android.app.Activity;
import android.app.AlertDialog;
import android.content.DialogInterface;
import android.content.SharedPreferences;
import android.graphics.Color;
import android.os.Bundle;
import android.os.Handler;
import android.text.InputType;
import android.view.Gravity;
import android.view.WindowManager;
import android.widget.Button;
import android.widget.EditText;
import android.widget.LinearLayout;
import android.widget.TextView;
import android.view.View;

import com.tvgun.tv.core.AffineCalibration;

import java.net.Inet4Address;
import java.net.NetworkInterface;
import java.util.Collections;
import java.util.Locale;

/**
 * 主界面：服务状态面板（10Hz 刷新）+ 自检模式 / 两点校准 / 端口设置入口。
 * D-pad 可导航（Button 默认可聚焦），大字体，横屏常亮。
 */
public final class MainActivity extends Activity {

    static final String PREFS = "tvgun";
    static final String KEY_PORT = "port";
    static final String KEY_HAS_CAL = "has_cal";
    static final int DEFAULT_PORT = 8000;

    private final Handler handler = new Handler();
    private TextView statusView;
    private TextView aimView;

    private final Runnable refresher = new Runnable() {
        @Override
        public void run() {
            refreshStatus();
            handler.postDelayed(this, 100); // 10Hz
        }
    };

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        getWindow().addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON);

        LinearLayout root = new LinearLayout(this);
        root.setOrientation(LinearLayout.VERTICAL);
        root.setGravity(Gravity.CENTER_HORIZONTAL);
        int pad = dp(40);
        root.setPadding(pad, pad, pad, pad);
        root.setBackgroundColor(Color.BLACK);

        TextView title = new TextView(this);
        title.setText("tvgun-tv 手机光枪接收端");
        title.setTextSize(30);
        title.setTextColor(Color.WHITE);
        title.setGravity(Gravity.CENTER);
        root.addView(title);

        statusView = new TextView(this);
        statusView.setTextSize(20);
        statusView.setTextColor(0xFFCCCCCC);
        statusView.setPadding(0, dp(20), 0, 0);
        root.addView(statusView);

        aimView = new TextView(this);
        aimView.setTextSize(24);
        aimView.setTextColor(0xFF00E5FF);
        aimView.setPadding(0, dp(10), 0, dp(20));
        root.addView(aimView);

        LinearLayout buttons = new LinearLayout(this);
        buttons.setOrientation(LinearLayout.HORIZONTAL);
        buttons.setGravity(Gravity.CENTER);
        root.addView(buttons);

        Button selfTest = makeButton("自检模式（打鸭子）");
        selfTest.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                startActivity(new android.content.Intent(MainActivity.this, SelfTestActivity.class));
            }
        });
        buttons.addView(selfTest);

        Button calib = makeButton("两点校准");
        calib.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                startActivity(new android.content.Intent(MainActivity.this, CalibrationActivity.class));
            }
        });
        buttons.addView(calib);

        Button settings = makeButton("端口设置");
        settings.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                showPortDialog();
            }
        });
        buttons.addView(settings);

        setContentView(root);
    }

    private Button makeButton(String text) {
        Button b = new Button(this);
        b.setText(text);
        b.setTextSize(20);
        LinearLayout.LayoutParams lp = new LinearLayout.LayoutParams(
                LinearLayout.LayoutParams.WRAP_CONTENT, LinearLayout.LayoutParams.WRAP_CONTENT);
        lp.setMargins(dp(12), 0, dp(12), 0);
        b.setLayoutParams(lp);
        b.setPadding(dp(24), dp(8), dp(24), dp(8));
        return b;
    }

    private int dp(int v) {
        return (int) (v * getResources().getDisplayMetrics().density + 0.5f);
    }

    @Override
    protected void onResume() {
        super.onResume();
        ensureServer();
        handler.post(refresher);
    }

    @Override
    protected void onPause() {
        handler.removeCallbacks(refresher);
        super.onPause();
    }

    private void ensureServer() {
        Bridge bridge = Bridge.get();
        if (bridge.isRunning()) {
            bridge.attachDefault();
            return;
        }
        int port = getSharedPreferences(PREFS, MODE_PRIVATE).getInt(KEY_PORT, DEFAULT_PORT);
        try {
            bridge.start(port);
            loadCalibration();
        } catch (Exception e) {
            statusView.setText("服务启动失败: " + e.getMessage());
        }
    }

    private void loadCalibration() {
        SharedPreferences p = getSharedPreferences(PREFS, MODE_PRIVATE);
        if (p.getBoolean(KEY_HAS_CAL, false)) {
            Bridge.get().applyCalibration(new AffineCalibration(
                    Double.longBitsToDouble(p.getLong("cal_a", Double.doubleToRawLongBits(1))),
                    Double.longBitsToDouble(p.getLong("cal_b", 0)),
                    Double.longBitsToDouble(p.getLong("cal_c", Double.doubleToRawLongBits(1))),
                    Double.longBitsToDouble(p.getLong("cal_d", 0))));
        }
    }

    private void refreshStatus() {
        Bridge bridge = Bridge.get();
        StringBuilder sb = new StringBuilder();
        if (bridge.isRunning()) {
            sb.append("服务状态：运行中\n");
            sb.append("端口：").append(bridge.boundPort())
                    .append("（发现端口 ").append(bridge.boundPort() + 1).append("）\n");
            sb.append("本机 IP：").append(localIp());
        } else {
            sb.append("服务状态：未运行");
        }
        statusView.setText(sb.toString());

        if (bridge.isRunning()) {
            double[] aim = bridge.server().getAimSlot().tryGet();
            long age = bridge.server().getAimSlot().ageMs();
            if (aim != null) {
                aimView.setText(String.format(Locale.US,
                        "aim: %.1f, %.1f   信号年龄: %d ms   连接: 在线", aim[0], aim[1], (long) aim[2]));
                aimView.setTextColor(0xFF00E5FF);
            } else if (age >= 0) {
                aimView.setText(String.format(Locale.US, "aim: 信号丢失（%d ms 前）  连接: 离线", age));
                aimView.setTextColor(0xFFFF5252);
            } else {
                aimView.setText("aim: 等待手机连接…（手机 App 选择本机或自动发现）");
                aimView.setTextColor(0xFF888888);
            }
        } else {
            aimView.setText("");
        }
    }

    private String localIp() {
        try {
            for (NetworkInterface ni : Collections.list(NetworkInterface.getNetworkInterfaces())) {
                for (java.net.InetAddress addr : Collections.list(ni.getInetAddresses())) {
                    if (!addr.isLoopbackAddress() && addr instanceof Inet4Address) {
                        return addr.getHostAddress();
                    }
                }
            }
        } catch (Exception e) {
            // 忽略
        }
        return "未知";
    }

    private void showPortDialog() {
        final EditText input = new EditText(this);
        input.setInputType(InputType.TYPE_CLASS_NUMBER);
        int cur = getSharedPreferences(PREFS, MODE_PRIVATE).getInt(KEY_PORT, DEFAULT_PORT);
        input.setText(String.valueOf(cur));
        new AlertDialog.Builder(this)
                .setTitle("服务端口（UDP/TCP 同端口，发现 = 端口+1）")
                .setView(input)
                .setPositiveButton("保存并重启服务", new DialogInterface.OnClickListener() {
                    @Override
                    public void onClick(DialogInterface dialog, int which) {
                        int port;
                        try {
                            port = Integer.parseInt(input.getText().toString().trim());
                        } catch (NumberFormatException e) {
                            return;
                        }
                        if (port < 1024 || port > 65534) {
                            return;
                        }
                        getSharedPreferences(PREFS, MODE_PRIVATE).edit()
                                .putInt(KEY_PORT, port).apply();
                        Bridge.get().stop();
                        ensureServer();
                    }
                })
                .setNegativeButton("取消", null)
                .show();
    }
}
